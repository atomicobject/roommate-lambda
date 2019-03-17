namespace Roommate.Tests

module ReservationMakerTest =

    open Xunit
    open FsUnit
    open Roommate
    open ReservationMaker

    [<Fact>]
    let ``plans to create event when both calendars are empty``() =
        let input: InputInformation = {
            ConferenceRoomAccountEvents = []
            RequestedTimeRange = {
                start = System.DateTime.Parse("03/15/2019 23:00:01")
                finish = System.DateTime.Parse("03/15/2019 23:00:01")
            }
        }

        let result = planOperation input
        result |> should equal (CreateNewEvent input.RequestedTimeRange)


    let anEvent : GoogleEventMapper.RoommateEvent = {
        gCalId = "123123123id"
        timeRange = {
            start = System.DateTime.Parse("03/15/2019 23:00:01")
            finish= System.DateTime.Parse("03/15/2019 23:14:59")
            }
        creatorEmail = "foobar@foobar-12345.iam.gserviceaccount.com"
        attendees = [{
            name = "conference-room-name"
            email = "ajksdlfjaskdlfjasdklfjasdklf@resource.calendar.google.com"
            responseStatus = "accepted"
        }]
    }
    [<Fact>]
    let ``rejects when event already exists``() =
        let input: InputInformation = {
            ConferenceRoomAccountEvents = [anEvent]
            RequestedTimeRange = anEvent.timeRange
        }

        let result = planOperation input
        result |> should equal (DoNothing "Room is booked during that time.")
    // extends event when appropriate

    // rejects if room is busy with another event
    // rejects if exact event already exists

