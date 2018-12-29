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
                sprintf "Calendar ID %s" room.name |> logFn
                let calendarService = serviceAccountSignIn config.serviceAccountEmail config.serviceAccountPrivKey config.serviceAccountAppName |> Async.RunSynchronously

                let events = fetchEvents calendarService room.calendarId |> Async.RunSynchronously

                events |> logEvents logFn

                room.calendarId,events
                )

    let iso8601datez (dt:DateTime) =
        // https://stackoverflow.com/a/115034
        dt.ToString("s", System.Globalization.CultureInfo.InvariantCulture) + "z"

    let maybeDateTimeString (ndt:Google.Apis.Calendar.v3.Data.EventDateTime) =
        ndt.DateTime |> Option.ofNullable
                     |> function
                        | Some dt -> iso8601datez dt
                        | None -> "(n/a)"

    let mapEventsToMessage (calendarId,events:Google.Apis.Calendar.v3.Data.Events) =
        // todo: unit test
        let msg : Messages.CalendarUpdate = {
            time = iso8601datez DateTime.UtcNow
            // todo: handle all-day events?
            events = events.Items
                         |> Seq.map(fun e -> ({s=maybeDateTimeString e.Start;e=maybeDateTimeString e.End}:Messages.CalendarEvent))
                         |> List.ofSeq
        }
        Ok (calendarId,msg)

    let determineTopicsToPublishTo logFn (config:RoommateConfig) (calendarId,msg) =
        let boardTopics = RoommateConfig.boardsForCalendar config calendarId
                            |> List.map (fun boardId -> sprintf "calendar-updates/for-board/%s" boardId)
        let calendarTopic = sprintf "calendar-updates/for-calendar/%s" calendarId
        let topics = calendarTopic::boardTopics
        Ok (topics,msg)

    let sendMessageToTopics (logFn:string->unit) (endpoint:string) (topics:string list, message:Messages.CalendarUpdate) =
        let json = (message |> Newtonsoft.Json.JsonConvert.SerializeObject)
        logFn "calendarUpdate message:"
        logFn json
        logFn "publishing to topics.."
        topics |> List.iter (fun topic ->
            let result = AwsIotClient.publish endpoint topic json
            logFn (sprintf "%s.. %s" topic (result.HttpStatusCode.ToString()))
        )
        Ok ""

    let flip (a,b) = (b,a)

    let lookupCalendarForBoard (config:RoommateConfig) boardId =
        let reversed = config.boardAssignments |> Map.toList |> List.map (fun (calId,boardList) -> boardList |> List.map (fun b -> b,calId)) |> List.concat |> Map.ofList
        reversed.TryFind boardId
