namespace Roommate

module CalendarWatcher =

    open GoogleCalendarClient
    open Roommate
    open Roommate.RoommateConfig
    type LambdaConfiguration = {
        roommateConfig : RoommateConfig.RoommateConfig
        serviceAccountEmail:string
        serviceAccountPrivKey:string
        serviceAccountAppName:string
    }

    let calIdFromURI (calURI:string) =
        calURI.Split('/') |> List.ofArray |> List.find (fun x -> x.Contains "atomicobject.com")
    let processPushNotification logFn (config:LambdaConfiguration) (pushNotificationHeaders:Map<string,string>) =

        let calendarIds = config.roommateConfig.meetingRooms

        // https://developers.google.com/calendar/v3/push
        pushNotificationHeaders
        |> (fun h -> h |> Map.filter ( fun k _ -> k.Contains "Goog") |> Ok)
        |> Result.map (fun gh ->
            logFn "Received push notification! Google headers:"
            gh |> Map.toList |> List.map (fun (k,v) -> sprintf "%s : %s" k v) |> List.iter logFn
            gh)
        |> Result.bind (fun gh -> 
                            match gh.TryFind "X-Goog-Resource-URI" with
                            | None -> Error "No X-Google-Resource-ID header found."
                            | Some resourceId -> Ok resourceId)
        |> Result.map calIdFromURI
        |> Result.bind (fun calId ->
            match RoommateConfig.tryLookupCalById config.roommateConfig calId with
            | Some room -> Ok room
            | None -> calId |> sprintf "Calendar %s is not in my list!" |> Error )
        |> Result.map (fun room ->
                sprintf "Received push notification for %s" room.name |> logFn
                let calendarService = serviceAccountSignIn config.serviceAccountEmail config.serviceAccountPrivKey config.serviceAccountAppName |> Async.RunSynchronously

                fetchEvents calendarService room.calendarId |> Async.RunSynchronously |> logEvents logFn)