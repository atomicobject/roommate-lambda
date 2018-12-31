// Learn more about F# at http://fsharp.org

open System
open Roommate
open Argu
open Google.Apis.Http
open System.Globalization
open SecretReader
open GoogleCalendarClient
open Roommate.RoommateConfig
open roommate

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
    | Subscribe_Webhook of calendar:string * endpoint:string
    | Create_Event of attendee:string
    | Lookup_CalId of search:string
    | Mqtt_Publish of topic:string * message:string

with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Auth _ -> "specify authentication mechanism"
            | Print_Ids -> "print available Google calendar IDs"
            | Lookup_CalId _ -> "lookup calendar ID for name substring"
            | Fetch_Calendars -> "retrieve events from all calendars"
            | Subscribe_Webhook _ -> "subscribe to webhook for calendar x and endpoint y"
            | Create_Event _ -> "create event on calendar (by name substring)"
            | Mqtt_Publish _ -> "publish message to MQTT topic"


let CONFIG_FILENAME = "roommate.json"

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
        printfn "Roommate Tool"
        printfn "%s" (parser.PrintUsage())
        printfn "EXAMPLES"
        printfn ""
        printfn "%s\t        //print all calendar IDs visible to the account" (parser.PrintCommandLineArgumentsFlat [Auth ClientIdSecret; Print_Ids])
        printfn "%s\t//create an event" (parser.PrintCommandLineArgumentsFlat [Auth ServiceAccount; Create_Event "eniac"])
        printfn ""
        printfn "Authentication uses environment variables googleClientId/googleClientSecret or"
        printfn "serviceAccountEmail/serviceAccountAppName/serviceAccountPrivKey"
    | _ ->
        if results.Contains Mqtt_Publish then
            let mqttEndpoint = readSecretFromEnv "mqttEndpoint"
            let topic,message = results.GetResult Mqtt_Publish
            let portalUrl = "https://console.aws.amazon.com/iot/home?region=us-east-1#/test"
            printfn "You can also pub/sub from the aws portal at:\n%s" portalUrl
            let result = AwsIotClient.publish mqttEndpoint topic message
            printfn "%s" (result.HttpStatusCode.ToString())
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
            ()

        if results.Contains Create_Event then
            let attendeeNameSubstring = results.GetResult Create_Event
            // todo: lookup english name of config.myCalendar
            let roomToInvite = RoommateConfig.looukpCalByName config attendeeNameSubstring
            let result = (GoogleCalendarClient.createEvent calendarService config.myCalendar roomToInvite.calendarId |> Async.RunSynchronously)

            printfn "created:"
            printfn ""
            printfn "%s" (summarizeEvent result)

        if results.Contains Lookup_CalId then
            results.GetResult Lookup_CalId
            |> RoommateConfig.looukpCalByName config
            |> fun mr -> mr.name,mr.calendarId
            |> fun (name,LongCalId calId) -> printfn "%s\n%s" name calId
            ()

    0
