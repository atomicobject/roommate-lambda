namespace Roommate

module CalendarWatcher =

    open System
    open GoogleCalendarClient
    open Roommate
    open Roommate.RoommateConfig
    open roommate

    type LambdaConfiguration = {
        roommateConfig : RoommateConfig.RoommateConfig
        serviceAccountEmail: string
        serviceAccountPrivKey: string
        serviceAccountAppName: string
        mqttEndpoint: string
    }

    let calIdFromURI (calURI:string) =
        calURI.Split('/') |> List.ofArray |> List.find (fun x -> x.Contains "atomicobject.com")

    let calendarIdFromPushNotification logFn (config:LambdaConfiguration) (pushNotificationHeaders:Map<string,string>) =

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

    let processCalendarId logFn (config:LambdaConfiguration) calId =
        calId |> (fun calId ->
            match RoommateConfig.tryLookupCalById config.roommateConfig calId with
            | Some room -> Ok room
            | None -> calId |> sprintf "Calendar %s is not in my list!" |> Error )
        |> Result.map (fun room ->
                sprintf "Received push notification for %s" room.name |> logFn
                let calendarService = serviceAccountSignIn config.serviceAccountEmail config.serviceAccountPrivKey config.serviceAccountAppName |> Async.RunSynchronously

                let events = fetchEvents calendarService room.calendarId |> Async.RunSynchronously

                events |> logEvents logFn

                room.calendarId,events
                )

    let mapEventsToMessage (calendarId,events:Google.Apis.Calendar.v3.Data.Events) =
        // todo: implement (and unit test):
        let msg : Messages.CalendarUpdate = {
            time = (DateTime.UtcNow.ToString())
            // todo: handle all-day events?
            events = events.Items
                         |> Seq.map(fun e -> ({s=e.Start.ToString();e=e.End.ToString()}:Messages.CalendarEvent))
                         |> List.ofSeq
        }
        Ok (calendarId,msg)

    let determineTopicsToPublishTo logFn (config:LambdaConfiguration) (calendarId,msg) =
        let boardTopics = config.roommateConfig.boardAssignments
                            |> List.where (fun ba -> ba.calendarId = calendarId)
                            |> List.map (fun ba -> ba.boardId)
                            |> List.map (fun boardId -> sprintf "calendar-updates/for-board/%s" boardId)
        let calendarTopic = sprintf "calendar-updates/for-calendar/%s" calendarId
        let topics = calendarTopic::boardTopics
        Ok (topics,msg)

    let sendMessageToTopics endpoint (topics:string list, message:Messages.CalendarUpdate) =
        let json = (message |> Newtonsoft.Json.JsonConvert.SerializeObject)
        topics |> List.iter (fun topic ->
            AwsIotClient.publish endpoint topic json
        )
        // todo: collect, log results
        Ok ""

    let lookupCalendarForBoard (config:LambdaConfiguration) boardId =
        config.roommateConfig.boardAssignments
            |> List.tryFind (fun ba -> ba.boardId = boardId)
            |> Option.map (fun ba -> ba.calendarId)
