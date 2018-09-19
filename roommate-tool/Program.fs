// Learn more about F# at http://fsharp.org

open System
open Roommate

[<EntryPoint>]
let main argv =
    printfn "Fetching calendar events.."

    let secrets = SecretReader.readSecrets()
    
    let calendarId = "atomicobject.com_3935353434383037353937@resource.calendar.google.com"
    
    let events = CalendarFetcher.fetchEvents secrets.googleClientId secrets.googleClientSecret calendarId |> Async.RunSynchronously
    
    CalendarFetcher.printEvents events

    0