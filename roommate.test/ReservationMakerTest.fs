namespace Roommate.Tests
open FsUnitTyped

module ReservationMakerTest =

    open Xunit
    open Roommate
    open ReservationMaker

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

    module planOperation =
        [<Fact>]
        let ``creates event when calendar is empty``() =
            let input: InputInformation = {
                ConferenceRoomAccountEvents = []
                RequestedTimeRange = {
                    start = System.DateTime.Parse("03/15/2019 23:00:00")
                    finish = System.DateTime.Parse("03/15/2019 23:14:59")
                }
            }

            let result = planOperation "foo@bar.com" input
            result |> shouldEqual (Ok (CreateNewEvent input.RequestedTimeRange))

        [<Fact>]
        let ``rejects when event already exists``() =
            let input: InputInformation = {
                ConferenceRoomAccountEvents = [anEvent]
                RequestedTimeRange = anEvent.timeRange
            }

            let result = planOperation anEvent.creatorEmail input
            result |> shouldEqual (Error "Room is booked during that time.")

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

            let result = planOperation anEvent.creatorEmail input
            result |> shouldEqual (Error "Room is booked during that time.")

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

            let result = planOperation anEvent.creatorEmail input
            result |> shouldEqual (Ok (ExtendEvent {
                eventId = anEvent.gCalId
                oldRange = {start=time1.start;finish=time1.finish}
                newRange = {start=time1.start;finish=time2.finish}
            }))


    module sanityCheck =

        let exampleInput : InputInformation = {
            ConferenceRoomAccountEvents = []
            RequestedTimeRange = {
                start = System.DateTime.Now.AddMinutes(5.0)
                finish = System.DateTime.Now.AddMinutes(20.0)
            }
        }

        [<Fact>]
        let ``should accept valid input``() =
            let result = sanityCheck exampleInput
            result |> shouldEqual (Ok exampleInput)

        [<Fact>]
        let ``should reject backwards event``() =
            let result = sanityCheck {exampleInput with RequestedTimeRange = {start=System.DateTime.Now;finish=System.DateTime.Now.AddHours(-1.0)}}
            result |> shouldEqual (Error "invalid event")

        [<Fact>]
        let ``should reject event in past``() =
            let result = sanityCheck {exampleInput with RequestedTimeRange = {start=System.DateTime.Now.AddDays(-1.0);finish=System.DateTime.Now.AddDays(-1.0).AddHours(1.0)}}
            result |> shouldEqual (Error "cannot create historic event")

        [<Fact>]
        let ``should reject event in far future``() =
            let result = sanityCheck {exampleInput with RequestedTimeRange = {start=System.DateTime.Now.AddDays(3.0);finish=System.DateTime.Now.AddDays(3.0).AddHours(1.0)}}
            result |> shouldEqual (Error "cannot create event >3 hours in the future")

