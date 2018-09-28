// Learn more about F# at http://fsharp.org

open System
open System
open Roommate
open Argu
 
type CLIArguments =
    | Print_Ids
    | Fetch_Calendars
    
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Print_Ids -> "print Google calendar IDs"
            | Fetch_Calendars -> "print events from all calendars"
            
[<EntryPoint>]
let main argv =

    let errorHandler = ProcessExiter()
    let parser = ArgumentParser.Create<CLIArguments>(programName = "dotnet run --", errorHandler = errorHandler, helpTextMessage = "Roommate Tool")
    
    let results = parser.Parse argv

    let secrets = SecretReader.readSecrets()
    
    match results.GetAllResults() with
    | [] ->
        printfn "Roommate Tool"
        printfn "%s" (parser.PrintUsage())
    | _ ->
        if results.Contains Print_Ids then
            CalendarFetcher.printCalendars secrets.googleClientId secrets.googleClientSecret  |> Async.RunSynchronously
        if results.Contains Fetch_Calendars then
            match secrets.calendarIds with
            | None -> printfn "Please set CALENDAR_IDS environment variable. (fetch IDs with --print_ids)"
            | Some calendarIds ->
                let calendarIds = calendarIds.Split(',') |> Seq.ofArray
                printfn "Fetching calendar events.."

                calendarIds |> Seq.iter (fun calendarId ->
                    let events = CalendarFetcher.fetchEvents secrets.googleClientId secrets.googleClientSecret calendarId |> Async.RunSynchronously
                    CalendarFetcher.printEvents events
                )

    0
