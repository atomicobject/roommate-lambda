namespace Roommate
open GoogleEventMapper
open TimeUtil

module ReservationMaker =
    (*
        big picture:
        - receive request for particular board and time range
        - look up the room for the board
        - for a wider time range (+/- 12h?)
//            - fetch all events for roommate account
            - fetch all events for conference room account
        - decide what we're trying to do
            - create a new event
            - extend an existing event
            - reject the request
        - request the change via google calendar API (and wait for it to be accepted)
        - send updated schedule
    *)

    (*
        This file is the "decide what we're trying to do" part.

        // todo: a separate function to sanity check input first (event is valid, near future, etc.)
    *)


    type InputInformation = {
//        RoommateAccountEvents : RoommateEvent list
        ConferenceRoomAccountEvents : RoommateEvent list
        RequestedTimeRange : TimeRange
    }
    type EventExtension = {
        eventId : string
        newRange : TimeRange
    }
    type ProcessResult =
        | CreateNewEvent of TimeRange
        | DoNothing of string
        | ExtendEvent of EventExtension


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

    let planOperation (input: InputInformation) (roommateAccountEmail:string): ProcessResult =
        let conflictingEvent = getFirstConflictingEvent input
        let adjacentRoommateEvents = getAdjacentRoommateEvents input roommateAccountEmail

        match conflictingEvent, adjacentRoommateEvents with
        | (Some _),_ -> DoNothing "Room is booked during that time."
        | None,[] -> CreateNewEvent input.RequestedTimeRange
        | None,[eventToExtend] ->
            ExtendEvent {eventId = eventToExtend.gCalId;newRange={start=eventToExtend.timeRange.start;finish=input.RequestedTimeRange.finish}}
        | None,x ->
            printfn "Found %d candidate events to extend. Giving up and creating a new one." x.Length
            CreateNewEvent input.RequestedTimeRange


    ()

