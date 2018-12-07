namespace Roommate

module CalendarWatcher =

    open GoogleCalendarClient
    type LambdaConfiguration = {
        calIds : string
        serviceAccountEmail:string
        serviceAccountPrivKey:string
        serviceAccountAppName:string
    }

    // todo: unit test
    let calIdFromURI (calURI:string) =
        calURI.Split('/') |> List.ofArray |> List.find (fun x -> x.Contains "atomicobject.com")
    let processPushNotification logFn config (pushNotificationHeaders:Map<string,string>) =

        let calendarIds = config.calIds.Split(',')

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
            if calendarIds |> Array.contains calId then
                Ok calId
            else
                calId |> sprintf "Calendar %s is not in my list!" |> Error )
        |> Result.map (fun calId ->
                sprintf "Calendar %s is in my list!" calId |> logFn
                let calendarService = serviceAccountSignIn config.serviceAccountEmail config.serviceAccountPrivKey config.serviceAccountAppName |> Async.RunSynchronously

                let events = fetchEvents calendarService calId |> Async.RunSynchronously
                logEvents events logFn)