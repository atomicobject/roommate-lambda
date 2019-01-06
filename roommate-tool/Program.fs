// Learn more about F# at http://fsharp.org

open System
open Roommate
open Argu
open Google.Apis.Http
open System.Globalization
open SecretReader
open GoogleCalendarClient
open Roommate.RoommateConfig
open FakeBoard
open Roommate
open Roommate

 (*
     todo
      - infer auth type from env vars
      - only require auth when the operation needs it
 *)

type AuthTypes =
    | ClientIdSecret
    | ServiceAccount

type CLIArguments =
    | Auth of AuthTypes
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

with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Auth _ -> "specify authentication mechanism"
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
            | Push_Button _ -> "simulate pushing button on board assigned to given room (request a reservation)"


let CONFIG_FILENAME = "roommate.json"

let printUsageAndExamples (parser:ArgumentParser<CLIArguments>) results =
    printfn "Roommate Tool"
    printfn "%s" (parser.PrintUsage())
    printfn "EXAMPLES"
    printfn ""
    printfn "%s\t        //print all calendar IDs visible to the account" (parser.PrintCommandLineArgumentsFlat [Auth ClientIdSecret; Print_Ids])
    printfn "%s\t//fetch calendar events" (parser.PrintCommandLineArgumentsFlat [Auth ServiceAccount; Fetch_Calendar "eniac"])
    printfn ""
    printfn "Authentication uses environment variables googleClientId/googleClientSecret or"
    printfn "serviceAccountEmail/serviceAccountAppName/serviceAccountPrivKey"

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

        let authType = results.TryGetResult Auth

        let calendarService =
            match authType with
            | Some ServiceAccount ->
                let serviceAccountEmail = readSecretFromEnv "serviceAccountEmail"
                let serviceAccountPrivKey = readSecretFromEnv "serviceAccountPrivKey"
                let serviceAccountAppName = readSecretFromEnv "serviceAccountAppName"
                GoogleCalendarClient.serviceAccountSignIn serviceAccountEmail serviceAccountPrivKey serviceAccountAppName |> Async.RunSynchronously

            | Some ClientIdSecret->
                let googleClientId = readSecretFromEnv "googleClientId"
                let googleClientSecret = readSecretFromEnv "googleClientSecret"
                GoogleCalendarClient.humanSignIn googleClientId googleClientSecret |> Async.RunSynchronously

            | _ ->  failwith "please specify an --auth type"

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
            calendarIds |> List.iter (fun calId -> GoogleCalendarClient.activateWebhook calendarService calId endpoint |> Async.RunSynchronously |> ignore)
            // todo: log all the results
            ()

        if results.Contains Create_Event then
            let attendeeNameSubstring = results.GetResult Create_Event
            // todo: lookup english name of config.myCalendar
            let roomToInvite = RoommateConfig.looukpCalByName config attendeeNameSubstring

            let start = System.DateTime.Now.AddHours(12.0)
            let finish = System.DateTime.Now.AddHours(12.0).AddMinutes(15.0)
            let result = (GoogleCalendarClient.createEvent calendarService config.myCalendar roomToInvite.calendarId start finish |> Async.RunSynchronously)

            printfn "created:"
            printfn ""
            printfn "%s" (summarizeEvent result)

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

        if results.Contains Fake_Board then
            let attendeeNameSubstring = results.GetResult Fake_Board
            let roomToInvite = RoommateConfig.looukpCalByName config attendeeNameSubstring
            printfn "using room %s" roomToInvite.name
            printfn ""
            let events = GoogleCalendarClient.fetchEvents calendarService roomToInvite.calendarId |> Async.RunSynchronously
            let roommateEvents = events.Items |> Seq.map CalendarWatcher.transformEvent |> List.ofSeq
            let lights = getLights roommateEvents
            let start = DateTime.Now |> roundDown
            let finish = start.AddHours 2.0

            let startTime = sprintf "%d:%d" start.Hour start.Minute
            let endTime = sprintf "%d:%d" finish.Hour finish.Minute
            let ledSeriesLen =(8*2+7*2-1)
            let spaceLen1 = ledSeriesLen- startTime.Length - endTime.Length
            printfn "%s%s%s"
                startTime
                ([0..spaceLen1] |> List.map (fun _ -> " ") |> List.reduce (+))
                endTime
            let spaceLen2 = ledSeriesLen - 2
            printfn "|%s|" ([0..spaceLen2] |> List.map (fun _ -> " ") |> List.reduce (+))

            lights |> List.iter (fun x -> printLed x;printf "  ")
            printfn ""
            printfn ""


        if results.Contains Push_Button then
            let attendeeNameSubstring = results.GetResult Push_Button
            let roomToInvite = RoommateConfig.looukpCalByName config attendeeNameSubstring
            let events = GoogleCalendarClient.fetchEvents calendarService roomToInvite.calendarId |> Async.RunSynchronously
            let roommateEvents = events.Items |> Seq.map CalendarWatcher.transformEvent |> List.ofSeq
            let lights = getLights roommateEvents
//            Printf.kprintf ()
            // let desiredMeetingTime = chooseNextTime lights
            // let result = publish blah blah blah

            ()

    0
