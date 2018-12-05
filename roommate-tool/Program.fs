// Learn more about F# at http://fsharp.org

open System
open Roommate
open Argu
open Google.Apis.Http
open System.Globalization
 
type AuthTypes =
    | ClientIdSecret
    | ApiKey
    | ServiceAccount
    | AccessToken
    
type CLIArguments =
    | Print_Ids
    | Fetch_Calendars
    | Subscribe_Webhook of calendar:string * endpoint:string
    | Auth of AuthTypes
    | Create_Event // of TODO
    
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Print_Ids -> "print Google calendar IDs"
            | Fetch_Calendars -> "print events from all calendars"
            | Subscribe_Webhook (_,_) -> "subscribe to webhook for calendar x and endpoint y"
            | Auth _ -> "specify authentication mechanism"
            | Create_Event -> "create event"
            
[<EntryPoint>]
let main argv =

    let errorHandler = ProcessExiter()
    let parser = ArgumentParser.Create<CLIArguments>(programName = "dotnet run --", errorHandler = errorHandler, helpTextMessage = "Roommate Tool")
    
    let results = parser.Parse argv

    let secrets =
        try
            SecretReader.readSecrets()
        with ex ->
            printfn "%s" ex.Message
            exit 0

    match results.GetAllResults() with
    | [] ->
        printfn "Roommate Tool"
        printfn "%s" (parser.PrintUsage())
    | _ ->
    
    
        let authType = results.TryGetResult Auth
        
        
        let calendarService = 
            match authType with
            | Some ApiKey ->
                let apiKey = SecretReader.secretOrBust "googleApiKey"
                CalendarFetcher.apiKeySignIn apiKey |> Async.RunSynchronously
            | Some ServiceAccount ->
                let serviceAccountEmail = SecretReader.secretOrBust "serviceAccountEmail"
                let serviceAccountPrivKey = SecretReader.secretOrBust "serviceAccountPrivKey"
                let serviceAccountAppName = SecretReader.secretOrBust "serviceAccountAppName"
                CalendarFetcher.serviceAccountSignIn serviceAccountEmail serviceAccountPrivKey serviceAccountAppName |> Async.RunSynchronously
            | x when x.IsNone || x = (Some ClientIdSecret) ->
                CalendarFetcher.humanSignIn secrets.googleClientId secrets.googleClientSecret |> Async.RunSynchronously
            | Some AccessToken ->
                CalendarFetcher.accessTokenSignIn secrets.googleClientId secrets.googleClientSecret secrets.googleTokenJson |> Async.RunSynchronously
            | _ -> failwith "oops"

        if results.Contains Print_Ids then
            CalendarFetcher.printCalendars calendarService |> Async.RunSynchronously
        if results.Contains Fetch_Calendars then
            match secrets.calendarIds with
            | None -> printfn "Please set CALENDAR_IDS environment variable. (fetch IDs with --print_ids)"
            | Some calendarIds ->
                let calendarIds = calendarIds.Split(',') |> Seq.ofArray
                printfn "Fetching calendar events.."

                calendarIds |> Seq.iter (fun calendarId ->
                    let events = CalendarFetcher.fetchEvents calendarService calendarId |> Async.RunSynchronously
                    CalendarFetcher.printEvents events
                )
        if results.Contains Subscribe_Webhook then
            let calendar,endpoint = results.GetResult Subscribe_Webhook
            
            let result = CalendarFetcher.activateWebhook calendarService calendar endpoint |> Async.RunSynchronously
            
            ()
        if results.Contains Create_Event then
            printfn "create event!"
            let calendarId = "atomicobject.com_60eh168tq1oiaji3eool392gn4@group.calendar.google.com"
            let attendee = "atomicobject.com_3935353434383037353937@resource.calendar.google.com"
            let result = (CalendarFetcher.createEvent calendarService calendarId attendee |> Async.RunSynchronously)
            printfn "created? event %s" (result.ToString())
            ()

    0
