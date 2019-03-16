namespace Roommate.Tests

module ReservationMakerTest =

    open Xunit
    open FsUnit
    open Roommate
    open ReservationMaker

    [<Fact>]
    let ``plans to creates event when both calendars are empty``() =
        let input: InputInformation = {
            RoommateAccountEvents = []
            ConferenceRoomAccountEvents = []
            RequestedTimeStart = System.DateTime.Parse("03/15/2019 23:00:01")
            RequestedTimeEnd = System.DateTime.Parse("03/15/2019 23:00:01") }
        let result = processInput input
        result |> should equal (CreateNewEvent (System.DateTime.Parse("03/15/2019 23:00:01"),System.DateTime.Parse("03/15/2019 23:00:01") ))

    // extends event when appropriate

    // fails if room is busy with another event

