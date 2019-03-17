namespace Roommate.Tests

module ReservationMakerTest =

    open Xunit
    open FsUnit
    open Roommate
    open ReservationMaker

    [<Fact>]
    let ``creates event when calendar is empty``() =
        let input: InputInformation = {
            ConferenceRoomAccountEvents = []
            RequestedTimeRange = {
                start = System.DateTime.Parse("03/15/2019 23:00:00")
                finish = System.DateTime.Parse("03/15/2019 23:14:59")
            }
        }

        let result = planOperation input "foo@bar.com"
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

        let result = planOperation input anEvent.creatorEmail
        result |> should equal (DoNothing "Room is booked during that time.")

    [<Fact>]
    let ``rejects when the room is busy``() =
        let requestedTimeRange = anEvent.timeRange
        let input: InputInformation = {
            ConferenceRoomAccountEvents = [
                {anEvent with timeRange = {
                                start = anEvent.timeRange.start.AddHours(-3.0)
                                finish = anEvent.timeRange.finish.AddHours(3.0)}
                } ]
            RequestedTimeRange = requestedTimeRange
        }

        let result = planOperation input anEvent.creatorEmail
        result |> should equal (DoNothing "Room is booked during that time.")

    [<Fact>]
    let ``extends when the requested range immediately follows a roommate event and the room is free``() =
        let time1 : TimeUtil.TimeRange = {
            start = System.DateTime.Parse("03/15/2019 23:00:00")
            finish= System.DateTime.Parse("03/15/2019 23:14:59")
        }
        let time2 : TimeUtil.TimeRange = {
            start = System.DateTime.Parse("03/15/2019 23:15:00")
            finish= System.DateTime.Parse("03/15/2019 23:29:59")
        }
        let input: InputInformation = {
            ConferenceRoomAccountEvents = [{anEvent with timeRange = time1}]
            RequestedTimeRange = time2
        }

        let result = planOperation input anEvent.creatorEmail
        result |> should equal (ExtendEvent {eventId = anEvent.gCalId; newRange = {start=time1.start;finish=time2.finish}})


