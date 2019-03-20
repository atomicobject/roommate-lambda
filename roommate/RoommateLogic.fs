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

                events
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

    let iso8601datez (dt:DateTime) =
        // https://stackoverflow.com/a/115034
        dt.ToString("s", System.Globalization.CultureInfo.InvariantCulture) + "Z"

    let mapEventsToMessage (events:GoogleEventMapper.RoommateEvent list) = //Google.Apis.Calendar.v3.Data.Events) =
        // todo: unit test
        let msg : Messages.CalendarUpdate = {
            time = iso8601datez DateTime.UtcNow
            events = events
                         |> Seq.map(fun e -> ({s=iso8601datez e.timeRange.start;e=iso8601datez e.timeRange.finish;r=GoogleEventMapper.isRoommateEvent e}:Messages.CalendarEvent))
                         |> List.ofSeq
        }
        msg

    let determineTopicsToPublishTo (config:RoommateConfig) (calendarId:LongCalId) =
        let boardTopics = RoommateConfig.boardsForCalendar config calendarId
                            |> List.map (fun boardId -> sprintf "calendar-updates/for-board/%s" boardId)
        let calendarTopic = sprintf "calendar-updates/for-calendar/%s" (calendarId |> fun (LongCalId s) -> s)
        let topics = calendarTopic::boardTopics
        (topics)

    let sendMessageToTopics (logFn:string->unit) (endpoint:string) (topics:string list) (message:Messages.CalendarUpdate) =
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

    let doEverything desiredMeetingTime roommateAccountEmail calendarService myCalendar (room:MeetingRoom) mappedEvents =
        let logMappedEvents (events:GoogleEventMapper.RoommateEvent list) =
            printfn "fetched %d events." events.Length
            events

        let logSelectedOperation (op:ReservationMaker.ProcessResult) =
//                printfn "selected operation %s" op.
            match op with
            | ReservationMaker.CreateNewEvent range ->
                printfn "Creating new event %s" (printRange range)
            | ReservationMaker.ExtendEvent ext ->
                printfn "Extending existing event %s => %s" (printRange ext.oldRange) (printRange ext.newRange)
            Ok op

        let spliceInEvent (mappedEvents:GoogleEventMapper.RoommateEvent list) (mappedNewEvent:GoogleEventMapper.RoommateEvent) =
                let otherEvents = mappedEvents |> List.where (fun e -> e.gCalId <> mappedNewEvent.gCalId)
                mappedNewEvent::otherEvents |> List.sortBy(fun e -> e.timeRange.start)

        mappedEvents
            |> logMappedEvents
            |> ReservationMaker.processRequest desiredMeetingTime roommateAccountEmail
            |> Result.bind logSelectedOperation
            |> Result.bind (ReservationMaker.executeOperation calendarService myCalendar room.calendarId)
            |> Result.bind (fun newEvent ->
                printfn "created event %s" <| summarizeEvent newEvent
                Ok newEvent
                )
            |> Result.bind (GoogleEventMapper.mapEvent >> Ok)
            |> Result.bind (fun mappedNewEvent ->
                let updatedSet = spliceInEvent mappedEvents mappedNewEvent
                Ok updatedSet)

    let createCalendarEvent logFn (config:LambdaConfiguration) (startTime:DateTime) (endTime:DateTime) (calId:LongCalId) =
        let desiredMeetingTime = {start=startTime;finish=endTime}
        let (LongCalId calIdString) = calId
        let roommateAccountEmail = config.serviceAccountEmail
        calId |> (fun calId ->
            match RoommateConfig.tryLookupCalById config.roommateConfig calId with
            | Some room -> Ok room
            | None -> calIdString |> sprintf "Calendar %s is not in my list!" |> Result.Error )
        |> Result.bind (fun room ->
                sprintf "Calendar ID %s" room.name |> logFn
                let calendarService = serviceAccountSignIn config.serviceAccountEmail config.serviceAccountPrivKey config.serviceAccountAppName |> Async.RunSynchronously

                let mappedEvents = GoogleCalendarClient.fetchEvents2 calendarService room.calendarId
                                   |> List.map GoogleEventMapper.mapEvent


                mappedEvents
                    |> doEverything desiredMeetingTime roommateAccountEmail calendarService config.roommateConfig.myCalendar room
                    |> function
                    | Ok events ->
                        printfn "updated event list:"
                        events |> List.iter (fun e -> printfn "%s %s" (e.timeRange.start.Date.ToString()) (printRange e.timeRange))
                        Ok events
                    | Error e ->
                        printfn "Error %s" e
                        Error e
                )

