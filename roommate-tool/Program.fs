// Learn more about F# at http://fsharp.org

open System
open Roommate
open Argu
open SecretReader
open GoogleCalendarClient
open Roommate.RoommateConfig
open FakeBoard
//open Roommate.GoogleEventMapper
//open Roommate.ReservationMaker
//open Roommate.ReservationMaker

 (*
     todo
      - only require auth when the operation needs it
 *)

type AuthTypes =
    | ClientIdSecret
    | ServiceAccount

type AuthCreds =
    | ClientIdSecretCreds of clientId:string * clientSecret:string
    | ServiceAccountCreds of serviceAccountEmail:string * serviceAccountPrivKey:string * serviceAccountAppName:string

type CLIArguments =
    | Print_Ids
    | Fetch_Calendars
    | Fetch_Calendar of calendarName:string
    | Subscribe_Webhook of calendar:string * endpoint:string
    | Unsubscribe_Webhook of calendar:string * resourceId:string * endpoint:string
    | Subscribe_All_Webhooks of endpoint:string
    | Create_Event of attendee:string
    | Update_Event of eventId:string
    | Lookup_CalId of search:string
    | Mqtt_Publish of topic:string * message:string
    | Fake_Board of room:string
    | Push_Button of room:string
    | Reserve_Room of room:string

with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Print_Ids -> "print available Google calendar IDs"
            | Lookup_CalId _ -> "lookup calendar ID for name substring"
            | Fetch_Calendars -> "retrieve events from all calendars"
            | Fetch_Calendar _ -> "retrieve events for specific calendar"
            | Subscribe_Webhook _ -> "subscribe to webhook for calendar x and endpoint y"
            | Unsubscribe_Webhook _ -> "unsubscribe to webhook for calendar x and endpoint y"
            | Subscribe_All_Webhooks _ -> "subscribe to webhook for all configured calendars to given endpoint"
            | Create_Event _ -> "create event on calendar (by name substring)"
            | Update_Event _ -> "update event (extend it by 15min)"
            | Mqtt_Publish _ -> "publish message to MQTT topic"
            | Fake_Board _ -> "simulate board state for given room"
            | Push_Button _ -> "simulate pushing button on board assigned to given room (sends a reservation request message)"
            | Reserve_Room _ -> "reserve room (run backend logic locally)"


let CONFIG_FILENAME = "roommate.json"

let printUsageAndExamples (parser:ArgumentParser<CLIArguments>) results =
    printfn "Roommate Tool"
    printfn "%s" (parser.PrintUsage())
    printfn "EXAMPLES"
    printfn ""
    printfn "%s\t        //print all calendar IDs visible to the account" (parser.PrintCommandLineArgumentsFlat [Print_Ids])
    printfn "%s\t//fetch calendar events" (parser.PrintCommandLineArgumentsFlat [Fetch_Calendar "eniac"])
    printfn ""
    printfn "Authentication uses environment variables googleClientId/googleClientSecret or"
    printfn "serviceAccountEmail/serviceAccountAppName/serviceAccountPrivKey"

let inferAuthTypeFromEnvVars () =
    let envVars =
        [ "serviceAccountEmail"
          "serviceAccountPrivKey"
          "serviceAccountAppName"
          "googleClientId"
          "googleClientSecret"
        ] |> List.map readEnvVar
    match envVars with
    | [Some email; Some privKey; Some appName; None; None] -> ServiceAccountCreds (email, privKey, appName)
    | [None; None; None; Some clientId; Some clientSecret] -> ClientIdSecretCreds (clientId, clientSecret)
    | _ -> failwith "please specify either serviceAccountEmail/serviceAccountPrivKey/serviceAccountAppName or googleClientId/googleClientSecret"

let calendarServiceForAuthType authType =
    match authType with
    | ServiceAccountCreds (email, privKey, appName) ->
        GoogleCalendarClient.serviceAccountSignIn email privKey appName |> Async.RunSynchronously
    | ClientIdSecretCreds (clientId, clientSecret) ->
        GoogleCalendarClient.humanSignIn clientId clientSecret |> Async.RunSynchronously

[<EntryPoint>]
let main argv =

    if System.IO.File.Exists(CONFIG_FILENAME) |> not then
        let serialized = RoommateConfig.serializeConfig RoommateConfig.defaultConfig
        System.IO.File.WriteAllText(CONFIG_FILENAME, serialized)
        printfn "Please fill out roommate.json"
        exit(0)

    let json = System.IO.File.ReadAllText(CONFIG_FILENAME)
    let config = RoommateConfig.deserializeConfig json

    let errorHandler = ProcessExiter()
    let parser = ArgumentParser.Create<CLIArguments>(programName = "dotnet run --", errorHandler = errorHandler, helpTextMessage = "Roommate Tool")

    let results = parser.Parse argv

    match results.GetAllResults() with
    | [] ->
        printUsageAndExamples parser results
    | _ ->
        if results.Contains Mqtt_Publish then
            let mqttEndpoint = readSecretFromEnv "mqttEndpoint"
            let topic,message = results.GetResult Mqtt_Publish
            let portalUrl = "https://console.aws.amazon.com/iot/home?region=us-east-1#/test"
            printfn "You can also pub/sub from the aws portal at:\n%s" portalUrl

            // IOT wants your JSON to have string keys.
            let fixJson = Newtonsoft.Json.JsonConvert.DeserializeObject<Map<string,string>> >> Newtonsoft.Json.JsonConvert.SerializeObject

            let canonicalizedMessage = fixJson message
            printfn "message: %s" canonicalizedMessage
            let result = AwsIotClient.publish mqttEndpoint topic canonicalizedMessage
            printfn "result: %s" (serializeIndented result)
            Environment.Exit 0

        let authType = inferAuthTypeFromEnvVars ()
        let calendarService = calendarServiceForAuthType authType

        if results.Contains Print_Ids then
            printfn "Retrieving meeting rooms (these can be pasted into config file).."

            GoogleCalendarClient.fetchCalendarIds calendarService
            |> Async.RunSynchronously
            |> Seq.map (fun x -> (x.name |> shortName, x.calId |> shorten))
            |> Map.ofSeq
            |> RoommateConfig.serializeIndented
            |> printfn "%s"

        if results.Contains Fetch_Calendar then
            let calendarName = results.GetResult Fetch_Calendar

            let calendarId = calendarName
                                |> RoommateConfig.looukpCalByName config
                                |> fun mr -> mr.calendarId

            GoogleCalendarClient.fetchEvents calendarService calendarId
                |> Async.RunSynchronously
                |> GoogleCalendarClient.logEvents (printfn "%s")

        if results.Contains Fetch_Calendars then
            let calendarIds = RoommateConfig.allCalendarIds config
            printfn "Fetching calendar events.."

            calendarIds |> Seq.iter (fun calendarId ->
                GoogleCalendarClient.fetchEvents calendarService calendarId
                    |> Async.RunSynchronously
                    |> GoogleCalendarClient.logEvents (printfn "%s")
            )

        if results.Contains Subscribe_Webhook then
            let calendar,endpoint = results.GetResult Subscribe_Webhook
            let result = GoogleCalendarClient.activateWebhook calendarService (LongCalId calendar) endpoint |> Async.RunSynchronously
            printfn "subscribe result: \n%s" (serializeIndented result)

        if results.Contains Unsubscribe_Webhook then
            let calendar,resourceId,endpoint = results.GetResult Unsubscribe_Webhook
            let result = GoogleCalendarClient.deactivateWebhook calendarService (LongCalId calendar) endpoint resourceId |> Async.RunSynchronously
            printfn "unsubscribe result: \n%s" (serializeIndented result)

        if results.Contains Subscribe_All_Webhooks then
            let endpoint = results.GetResult Subscribe_All_Webhooks
            let calendarIds = RoommateConfig.allCalendarIds config
            calendarIds |> List.iter (fun calId ->
                printf "calendar %s" (calId.ToString())
                try
                    GoogleCalendarClient.activateWebhook calendarService calId endpoint |> Async.RunSynchronously |> ignore
                    printfn " ..success"
                with
                | :? System.AggregateException as e  when e.InnerException.Message.Contains("not unique") -> printfn " .. already active"
                | e -> printfn " ..error: \n%s\n" (e.ToString())
                )
            // todo: log all the results
            ()

        if results.Contains Create_Event then
            let attendeeNameSubstring = results.GetResult Create_Event
            // todo: lookup english name of config.myCalendar
            let roomToInvite = RoommateConfig.looukpCalByName config attendeeNameSubstring

            let start = System.DateTime.Now.AddHours(12.0)
            let finish = System.DateTime.Now.AddHours(12.0).AddMinutes(15.0)
            let result = (GoogleCalendarClient.createEvent calendarService config.myCalendar roomToInvite.calendarId start finish |> Async.RunSynchronously)

            match result with
            | Ok r ->
                printfn "created: %s" (r.Id)
                printfn ""
                printfn "%s" (summarizeEvent r)
            | Error reason ->
                printfn "failed to create event."
                printfn "%s" reason

        if results.Contains Update_Event then
            let eventId = results.GetResult Update_Event

            let calId = (LongCalId config.myCalendar)

            let roommateEvents = GoogleCalendarClient.fetchEvents calendarService calId |> Async.RunSynchronously
            let event = roommateEvents.Items |> Seq.find (fun e -> e.Id = eventId)
            printfn "found event to extend: %s" (event.ToString())

            event.End.DateTime <- System.Nullable (event.End.DateTime.Value.AddMinutes 15.0)
            let result = (GoogleCalendarClient.editEvent calendarService config.myCalendar event) |> Async.RunSynchronously

            ()

        if results.Contains Lookup_CalId then
            results.GetResult Lookup_CalId
            |> RoommateConfig.looukpCalByName config
            |> fun mr -> mr.name,mr.calendarId
            |> fun (name,LongCalId calId) -> printfn "%s\n%s" name calId
            ()

        let lightsForRoom (room:MeetingRoom) =
            room
            |> fun room -> printf "%s\t" room.name; room.calendarId
            |> GoogleCalendarClient.fetchEvents calendarService
            |> Async.RunSynchronously
            |> fun x -> x.Items
            |> Seq.map RoommateLogic.transformEvent
            |> List.ofSeq
            |> getLights

        if results.Contains Fake_Board then
            let room = results.GetResult Fake_Board |> RoommateConfig.looukpCalByName config
            let lights = lightsForRoom room

            printLights lights
            printfn ""


        let logDesiredMeetingTime (mt:DesiredMeetingTime) =
            match mt with
            | None ->
                printfn "room booked solid! no slots available."
            | Some (range,pos)->
                printf "\trequesting %d:%d - %d:%d (light #%d)..\t" range.start.Hour range.start.Minute range.finish.Hour range.finish.Minute pos
            mt

        if results.Contains Reserve_Room then
            let room = results.GetResult Reserve_Room |> RoommateConfig.looukpCalByName config
            let roommateAccountEmail = readSecretFromEnv "serviceAccountEmail"
            let lights = lightsForRoom room
            let desiredMeetingTime = chooseNextTime lights
                                        |> logDesiredMeetingTime
                                        |> function
                                        | None -> failwith "exiting."
                                        | Some (range,_) -> range

            let events = GoogleCalendarClient.fetchEvents calendarService room.calendarId
                            |> Async.RunSynchronously |> (fun g -> g.Items)
                            |> List.ofSeq
                            |> List.map GoogleEventMapper.mapEvent

//            printfn "%s" (Newtonsoft.Json.JsonConvert.SerializeObject(events))

            let operation = ReservationMaker.processRequest events desiredMeetingTime roommateAccountEmail
            printfn "selected operation: %s" (operation.ToString())
            match operation with
            | Result.Error e ->
                printfn "Error: %s" e
                ()
            | Ok (ReservationMaker.CreateNewEvent timeRange) ->
                // todo: pass timeRange as one parameter
                let createResult = GoogleCalendarClient.createEvent calendarService config.myCalendar room.calendarId timeRange.start timeRange.finish |> Async.RunSynchronously
                printfn "createResult %s" (createResult.ToString())
                ()
            | Ok (ReservationMaker.ExtendEvent reservationmakerExtension) ->
                let googleExtension : GoogleCalendarClient.EventExtension = {
                    eventId = reservationmakerExtension.eventId
                    newRange = reservationmakerExtension.newRange
                    oldRange = reservationmakerExtension.oldRange
                }
                let extendResult = GoogleCalendarClient.extendEvent calendarService config.myCalendar googleExtension room.calendarId
                printfn "extendResult %s" (extendResult.ToString())
                ()


            ()

        if results.Contains Push_Button then
            let room = results.GetResult Push_Button |> RoommateConfig.looukpCalByName config

            let lights = lightsForRoom room

            printLights lights

            let desiredMeetingTime = chooseNextTime lights
            let boardId = RoommateConfig.boardsForCalendar config room.calendarId |> List.head
            desiredMeetingTime
                |> logDesiredMeetingTime
                |> Option.iter (fun (range,_) ->
                    let startTime = TimeUtil.unixTimeFromDate range.start
                    let finishTime = TimeUtil.unixTimeFromDate range.finish
                    let message = sprintf "{\"boardId\":\"%s\",\"start\":%d,\"finish\":%d}" boardId startTime finishTime

                    let mqttEndpoint = readSecretFromEnv "mqttEndpoint"
                    let result = AwsIotClient.publish mqttEndpoint "reservation-request" message
                    printfn "result %s" (result.HttpStatusCode.ToString())
                   )


            ()

    0
