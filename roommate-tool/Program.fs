﻿// Learn more about F# at http://fsharp.org

open System
open Roommate
open Argu
open Google.Apis.Http
open System.Globalization
open SecretReader
open GoogleCalendarClient
open Roommate.RoommateConfig
 
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
    | Create_Event of calendarId:string * attendee:string
    | Lookup_CalId of search:string
    
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Print_Ids -> "print available Google calendar IDs"
            | Fetch_Calendars -> "retrieve events from all calendars"
            | Subscribe_Webhook _ -> "subscribe to webhook for calendar x and endpoint y"
            | Auth _ -> "specify authentication mechanism"
            | Create_Event _ -> "create event"
            | Lookup_CalId _ -> "lookup calendar ID for name substring"
            

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
    printfn "read config! myCal=%s" (config.myCalendar)


    let errorHandler = ProcessExiter()
    let parser = ArgumentParser.Create<CLIArguments>(programName = "dotnet run --", errorHandler = errorHandler, helpTextMessage = "Roommate Tool")
    
    let results = parser.Parse argv

    match results.GetAllResults() with
    | [] ->
        printfn "Roommate Tool"
        printfn "%s" (parser.PrintUsage())
    | _ ->
    
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
            let makeRecord (id:string,name:string) : RoommateConfig.MeetingRoom =
                {calendarId=id;name=name}

            GoogleCalendarClient.fetchCalendarIds calendarService |> Async.RunSynchronously
            |> Seq.map makeRecord // the type from the config file
            |> Seq.toList
            |> RoommateConfig.serializeIndented
            |> printfn "%s"

        if results.Contains Fetch_Calendars then
            let calendarIds= readSecretFromEnv "CALENDAR_IDS"
            let calendarIds = calendarIds.Split(',') |> Seq.ofArray
            printfn "Fetching calendar events.."

            calendarIds |> Seq.iter (fun calendarId ->
                let events = GoogleCalendarClient.fetchEvents calendarService calendarId |> Async.RunSynchronously
                GoogleCalendarClient.printEvents events
            )
        if results.Contains Subscribe_Webhook then
            let calendar,endpoint = results.GetResult Subscribe_Webhook
            let result = GoogleCalendarClient.activateWebhook calendarService calendar endpoint |> Async.RunSynchronously
            ()

        if results.Contains Create_Event then
            printfn "create event!"
            let calendarId,attendee= results.GetResult Create_Event
            let result = (GoogleCalendarClient.createEvent calendarService calendarId attendee |> Async.RunSynchronously)
            printfn "created event %s" (result.ToString())
            ()

        if results.Contains Lookup_CalId then
            results.GetResult Lookup_CalId
            |> RoommateConfig.looukpCalByName config
            |> fun cal -> printfn "%s\n%s" cal.name cal.calendarId
            ()

    0
