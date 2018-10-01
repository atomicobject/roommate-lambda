// Learn more about F# at http://fsharp.org

open System
open Roommate
open Argu
open Google.Apis.Http
 
type CLIArguments =
    | Print_Ids
    | Fetch_Calendars
    | Subscribe_Webhook of calendar:string * endpoint:string
    
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Print_Ids -> "print Google calendar IDs"
            | Fetch_Calendars -> "print events from all calendars"
            | Subscribe_Webhook (_,_) -> "subscribe to webhook for calendar x and endpoint y"
            
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
    
    let calendarService = CalendarFetcher.humanSignIn secrets.googleClientId secrets.googleClientSecret |> Async.RunSynchronously

    match results.GetAllResults() with
    | [] ->
        printfn "Roommate Tool"
        printfn "%s" (parser.PrintUsage())
    | _ ->
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

    0
