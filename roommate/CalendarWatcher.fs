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
        calURI.Split('/') |> List.ofArray |> List.find (fun x -> x.Contains "atomicobject.com") |> LongCalId

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

    let fetchEventsForCalendar logFn (config:LambdaConfiguration) calId =
        let (LongCalId s) = calId
        calId |> (fun calId ->
            match RoommateConfig.tryLookupCalById config.roommateConfig calId with
            | Some room -> Ok room
            | None -> s |> sprintf "Calendar %s is not in my list!" |> Error )
        |> Result.map (fun room ->
                sprintf "Calendar ID %s" room.name |> logFn
                let calendarService = serviceAccountSignIn config.serviceAccountEmail config.serviceAccountPrivKey config.serviceAccountAppName |> Async.RunSynchronously

                let events = fetchEvents calendarService room.calendarId |> Async.RunSynchronously

                events |> logEvents logFn

                room.calendarId,events
                )

    type TimeRange = {
        start : DateTime
        finish : DateTime
    }

    type RoommateEvent = {
        range : TimeRange
        gcalId : string
        isRoommateEvent : bool
    }

    type CalendarCreateAction =
        | CreateEvent of TimeRange
        | UpdateEvent of string * DateTime * DateTime
        | Nothing of string

    let timeRangeIntersects (r1:TimeRange) (r2:TimeRange) =
        let times = [ "1_start",r1.start;"1_end",r1.finish;"2_start",r2.start;"2_end",r2.finish]
        let sequence = times |> List.sortBy snd
        let sortedNames = sequence |> List.map fst
        match sortedNames with
        | ["1_start";"1_end";"2_start";"2_end"] -> false
        | ["2_start";"2_end";"1_start";"1_end"] -> false
        | ["1_start";"2_start";"1_end";"2_end"] -> true
        | ["2_start";"1_start";"2_end";"1_end"] -> true
        | ["1_start";"2_start";"2_end";"1_end"] -> true
        | ["2_start";"1_start";"1_end";"2_end"] -> true
        |_ -> failwith "unhandled time range sequence"

    let transformEvent (event:Google.Apis.Calendar.v3.Data.Event) : RoommateEvent =
        {
            range = {
                start = event.Start.DateTime.Value
                finish = event.End.DateTime.Value
            }
            gcalId=event.Id
            isRoommateEvent = isRoommateEvent event
        }

    let determineWhatToDo (events:RoommateEvent list) (desiredTimeRange:TimeRange) =
        let adjacentEvent = events |> Seq.where (fun e -> e.isRoommateEvent) |> Seq.tryFind (fun e -> (e.range.finish - desiredTimeRange.start).Duration() < System.TimeSpan.FromMinutes 2.0)

        if desiredTimeRange.start > desiredTimeRange.finish then
            (Nothing "invalid event")
        else if desiredTimeRange.finish < System.DateTime.UtcNow then
            (Nothing "cannot create historic event")
        else if desiredTimeRange.start > (System.DateTime.UtcNow.AddHours 3.0) then
            (Nothing "cannot create event >3 hours in the future")
        else if adjacentEvent.IsSome then
            printfn "found adjacent event %s-%s" (adjacentEvent.Value.range.start.ToString()) (adjacentEvent.Value.range.finish.ToString())
            (UpdateEvent (adjacentEvent.Value.gcalId,adjacentEvent.Value.range.start,desiredTimeRange.finish))
        else if (events |> List.tryFind (fun e -> timeRangeIntersects e.range desiredTimeRange)).IsSome then
            (Nothing "busy")
        else
            CreateEvent desiredTimeRange

    let createCalendarEvent logFn (config:LambdaConfiguration) (startTime:DateTime) (endTime:DateTime) (calId:LongCalId) =
        let desiredTimeRange = {start=startTime;finish=endTime}
        let (LongCalId calIdString) = calId
        calId |> (fun calId ->
            match RoommateConfig.tryLookupCalById config.roommateConfig calId with
            | Some room -> Ok room
            | None -> calIdString |> sprintf "Calendar %s is not in my list!" |> Error )
        |> Result.map (fun room ->
                sprintf "Calendar ID %s" room.name |> logFn
                let calendarService = serviceAccountSignIn config.serviceAccountEmail config.serviceAccountPrivKey config.serviceAccountAppName |> Async.RunSynchronously

                let googleEvents = (fetchEvents calendarService room.calendarId |> Async.RunSynchronously)

                googleEvents |> logEvents (printfn "%s")

                let events = googleEvents.Items |> List.ofSeq |> List.map transformEvent
                printfn "requested time range %s %s" (startTime.ToString("o")) (endTime.ToString("o"))

                let action = determineWhatToDo events desiredTimeRange
                let result = match action with
                                | CreateEvent r -> createEvent calendarService config.roommateConfig.myCalendar room.calendarId r.start r.finish |> Async.RunSynchronously
                                | UpdateEvent (eventId,start,finish) -> editEventLengths calendarService calIdString eventId start finish |> Async.RunSynchronously
                                | Nothing s -> failwith ("createCalendarEvent rejection: "+s)

                sprintf "result event: %s" (serializeIndented result) |> logFn
                )

    let iso8601datez (dt:DateTime) =
        // https://stackoverflow.com/a/115034
        dt.ToString("s", System.Globalization.CultureInfo.InvariantCulture) + "Z"

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
                         |> Seq.map(fun e -> ({s=maybeDateTimeString e.Start;e=maybeDateTimeString e.End;r=isRoommateEvent e}:Messages.CalendarEvent))
                         |> List.ofSeq
        }
        Ok (calendarId,msg)

    let determineTopicsToPublishTo logFn (config:RoommateConfig) (calendarId:LongCalId,msg) =
        let boardTopics = RoommateConfig.boardsForCalendar config calendarId
                            |> List.map (fun boardId -> sprintf "calendar-updates/for-board/%s" boardId)
        let calendarTopic = sprintf "calendar-updates/for-calendar/%s" (calendarId |> fun (LongCalId s) -> s)
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
        let reversed = config.boardAssignments
                        |> Map.toList
                        |> List.map (fun (calId,boardList) -> boardList |> List.map (fun b -> b,calId))
                        |> List.concat
                        |> Map.ofList

        reversed.TryFind boardId
            |> Option.map lengthen



