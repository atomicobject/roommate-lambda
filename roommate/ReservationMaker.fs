namespace Roommate
open TimeUtil

module ReservationMaker =

    type InputInformation = {
        ConferenceRoomAccountEvents : Types.RoommateEvent list
        RequestedTimeRange : TimeRange
    }
    type ProcessResult =
        | CreateNewEvent of TimeRange
        | ExtendEvent of Types.EventExtension

    let getAdjacentRoommateEvents input roommateAccountEmail =
        let isCloseTo (a:System.DateTime) (b:System.DateTime) =
            (a-b).Duration() < System.TimeSpan.FromMinutes 2.0
        let isCloseToRequestedStart = isCloseTo input.RequestedTimeRange.start
        input.ConferenceRoomAccountEvents
            |> List.where (fun e -> e.creatorEmail = roommateAccountEmail)
            |> List.where (fun e -> isCloseToRequestedStart e.timeRange.finish)

    let getFirstConflictingEvent input =
        let intersectsRequestedRange = timeRangeIntersects input.RequestedTimeRange
        input.ConferenceRoomAccountEvents |> Seq.map (fun e -> e.timeRange) |> Seq.tryFind intersectsRequestedRange

    let planOperation (roommateAccountEmail:string) (input: InputInformation): Result<ProcessResult,string> =
        let conflictingEvent = getFirstConflictingEvent input
        let adjacentRoommateEvents = getAdjacentRoommateEvents input roommateAccountEmail

        match conflictingEvent, adjacentRoommateEvents with
        | (Some _),_ -> Error "Room is booked during that time."
        | None,[] -> Ok (CreateNewEvent input.RequestedTimeRange)
        | None,[eventToExtend] ->
            Ok (ExtendEvent
                    {
                        eventId = eventToExtend.gCalId
                        oldRange=eventToExtend.timeRange
                        newRange={start=eventToExtend.timeRange.start;finish=input.RequestedTimeRange.finish}
                    })
        | None,x ->
            printfn "Found %d candidate events to extend. Giving up and creating a new one." x.Length
            Ok (CreateNewEvent input.RequestedTimeRange)

    let sanityCheck (input: InputInformation) =
        let desiredTimeRange = input.RequestedTimeRange
        if desiredTimeRange.start > desiredTimeRange.finish then
            (Error "invalid event")
        else if desiredTimeRange.finish < System.DateTime.Now then
            (Error "cannot create historic event")
        else if desiredTimeRange.start > (System.DateTime.Now.AddHours 3.0) then
            (Error "cannot create event >3 hours in the future")
        else
            Ok input

    let processRequest desiredMeetingTime roommateAccountEmail events =
            let input: InputInformation = {
                            ConferenceRoomAccountEvents = events
                            RequestedTimeRange = desiredMeetingTime
                        }
            input |> sanityCheck |> Result.bind (planOperation roommateAccountEmail)



    let executeOperation calendarService myCalendar roomCalendarId op =
        match op with
            | CreateNewEvent timeRange ->
                GoogleCalendarClient.createEvent calendarService myCalendar roomCalendarId timeRange |> Async.RunSynchronously
            | ExtendEvent reservationmakerExtension ->
                let googleExtension : Types.EventExtension = {
                    eventId = reservationmakerExtension.eventId
                    newRange = reservationmakerExtension.newRange
                    oldRange = reservationmakerExtension.oldRange
                }
                GoogleCalendarClient.extendEvent calendarService myCalendar googleExtension roomCalendarId
