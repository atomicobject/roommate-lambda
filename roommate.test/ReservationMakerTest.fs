namespace Roommate.Tests

module ReservationMakerTest =

    open Xunit
    open FsUnit
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
            result |> should equal (CreateNewEvent input.RequestedTimeRange)

        [<Fact>]
        let ``rejects when event already exists``() =
            let input: InputInformation = {
                ConferenceRoomAccountEvents = [anEvent]
                RequestedTimeRange = anEvent.timeRange
            }

            let result = planOperation anEvent.creatorEmail input
            result |> should equal (Error "Room is booked during that time.")

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
            result |> should equal (Error "Room is booked during that time.")

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
            result |> should equal (ExtendEvent {eventId = anEvent.gCalId; newRange = {start=time1.start;finish=time2.finish}})


    module sanityCheck =

        let exampleInput : InputInformation = {
            ConferenceRoomAccountEvents = []
            RequestedTimeRange = {
                start = System.DateTime.Now.AddMinutes(5.0)
                finish = System.DateTime.Now.AddMinutes(20.0)
            }
        }

        // https://stackoverflow.com/a/23991168/202907
        let shouldEqual (x: 'a) (y: 'a) =
            Assert.AreEqual(x, y, sprintf "Expected: %A\nActual: %A" x y)

        [<Fact>]
        let ``should accept valid input``() =
            let result = sanityCheck exampleInput
            printfn "result %s" (result.ToString())
            result |> shouldEqual (Ok exampleInput)

        [<Fact>]
        let ``should reject valid input``() =
            let result = sanityCheck {exampleInput with RequestedTimeRange = {start=System.DateTime.Now;finish=System.DateTime.Now.AddHours(-1.0)}}
            printfn "result %s" (result.ToString())
            result |> shouldEqual (Error "invalid event")

        [<Fact>]
        let ``should reject event in past``() =
            let result = sanityCheck {exampleInput with RequestedTimeRange = {start=System.DateTime.Now.AddDays(-1.0);finish=System.DateTime.Now.AddDays(-1.0).AddHours(1.0)}}
            printfn "result %s" (result.ToString())
            result |> shouldEqual (Error "cannot create historic event")

        [<Fact>]
        let ``should reject event in far future``() =
            let result = sanityCheck {exampleInput with RequestedTimeRange = {start=System.DateTime.Now.AddDays(3.0);finish=System.DateTime.Now.AddDays(3.0).AddHours(1.0)}}
            printfn "result %s" (result.ToString())
            result |> shouldEqual (Error "cannot create event >3 hours in the future")

