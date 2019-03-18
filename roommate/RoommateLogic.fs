namespace Roommate

module RoommateLogic =

    open System
    open GoogleCalendarClient
    open Roommate
    open Roommate.RoommateConfig
    open Roommate.TimeUtil

    type LambdaConfiguration = {
        roommateConfig : RoommateConfig.RoommateConfig
        serviceAccountEmail: string
        serviceAccountPrivKey: string
        serviceAccountAppName: string
        mqttEndpoint: string
        webhookUrl: string
    }

    let fetchEventsForCalendar logFn (config:LambdaConfiguration) calId =
        let (LongCalId s) = calId
        calId |> (fun calId ->
            match RoommateConfig.tryLookupCalById config.roommateConfig calId with
            | Some room -> Ok room
            | None -> s |> sprintf "Calendar %s is not in my list!" |> Result.Error )
        |> Result.map (fun room ->
                sprintf "Calendar ID %s" room.name |> logFn
                let calendarService = serviceAccountSignIn config.serviceAccountEmail config.serviceAccountPrivKey config.serviceAccountAppName |> Async.RunSynchronously

                let events = fetchEvents calendarService room.calendarId |> Async.RunSynchronously

                events |> logEvents logFn

                room.calendarId,events
                )

    type RoommateEvent = {
        range : TimeRange
        gcalId : string
        isRoommateEvent : bool
    }

    type CalendarCreateAction =
        | CreateEvent of TimeRange
        | UpdateEvent of string * DateTime * DateTime
        | Nothing of string

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
        let roommateEvents = events |> Seq.where (fun e -> e.isRoommateEvent)

        printfn "found %d roommate events." (roommateEvents |> Seq.length)

        // todo: Seq.where (there may be multiple!)
        let adjacentEvent = roommateEvents |> Seq.tryFind (fun e ->
            let distance = (e.range.finish - desiredTimeRange.start).Duration()
            distance < System.TimeSpan.FromMinutes 2.0
            )

        if desiredTimeRange.start > desiredTimeRange.finish then
            (Nothing "invalid event")
        else if desiredTimeRange.finish < System.DateTime.UtcNow then
            (Nothing "cannot create historic event")
        else if desiredTimeRange.start > (System.DateTime.UtcNow.AddHours 3.0) then
            (Nothing "cannot create event >3 hours in the future")
        else if adjacentEvent.IsSome then
            printfn "found event on roommate's calendar adjacent to requested range."
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
            | None -> calIdString |> sprintf "Calendar %s is not in my list!" |> Result.Error )
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
                                | UpdateEvent (eventId,start,finish) -> editAssociatedEventLength calendarService config.roommateConfig.myCalendar calIdString eventId start finish |> Async.RunSynchronously
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
            events = events.Items
                         |> Seq.map(fun e -> ({s=maybeDateTimeString e.Start;e=maybeDateTimeString e.End;r=isRoommateEvent e}:Messages.CalendarEvent))
                         |> List.ofSeq
        }
        Ok (calendarId,msg)

    let determineTopicsToPublishTo (config:RoommateConfig) (calendarId:LongCalId,msg) =
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



