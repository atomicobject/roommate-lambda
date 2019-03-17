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
    type ProcessResult =
        | CreateNewEvent of TimeRange
        | DoNothing of string


    let planOperation (input: InputInformation): ProcessResult =
        let intersectsRequestedRange = timeRangeIntersects input.RequestedTimeRange
        let overlap = input.ConferenceRoomAccountEvents |> Seq.map (fun e -> e.timeRange) |> Seq.tryFind intersectsRequestedRange
        match overlap with
        | None -> CreateNewEvent input.RequestedTimeRange
        | Some _ -> DoNothing "Room is booked during that time."



    ()

