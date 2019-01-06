namespace Roommate.Tests

open Xunit
open FsUnit
open Roommate

module CalendarWatcherTest =
    open Google.Apis.Calendar.v3.Data
    open Google.Apis.Calendar.v3.Data
    open Google.Apis.Calendar.v3.Data
    open Roommate.CalendarWatcher

    [<Fact>]
    let ``parses calendar ID from URI``() =
        // from X-Goog-Resource-URI header in push notification
        let uri = "https://www.googleapis.com/calendar/v3/calendars/atomicobject.com_234523452345@resource.calendar.google.com/events?maxResults=250&alt=json"
        calIdFromURI uri |> should equal (RoommateConfig.LongCalId "atomicobject.com_234523452345@resource.calendar.google.com")


    [<Fact>]
    let ``creates event``() =
        let now = System.DateTime.UtcNow
        let range = {
            start = now
            finish = now.AddMinutes 15.0
        }
        let result = determineWhatToDo [] range
        result |> should equal (CreateEvent range)

    [<Fact>]
    let ``rejects events in the past``() =
        let now = System.DateTime.UtcNow
        let range = {
            start = now.AddHours -1.0
            finish = now.AddMinutes -45.0
        }
        let result = determineWhatToDo [] range
        result |> should equal (Nothing "cannot create historic event")
        ()

    [<Fact>]
    let ``rejects events far in the future``() =
        let now = System.DateTime.UtcNow
        let range = {
            start = now.AddHours 5.0
            finish = now.AddHours 6.0
        }
        let result = determineWhatToDo [] range
        result |> should equal (Nothing "cannot create event >3 hours in the future")
        ()

    [<Fact>]
    let ``rejects invalid events``() =
        let now = System.DateTime.UtcNow
        let range = {
            finish = now
            start = now.AddMinutes 15.0
        }
        let result = determineWhatToDo [] range
        result |> should equal (Nothing "invalid event")
        ()

    let buildGoogleEventDateTime (time:System.DateTime) =
        new EventDateTime(DateTimeRaw = time.ToString("o"))

    let buildGoogleEvent (start:System.DateTime) (finish:System.DateTime) =
        new Event(Start = buildGoogleEventDateTime start, End=buildGoogleEventDateTime finish)

    [<Fact>]
    let ``rejects when the room is already reserved``() =
        let now = System.DateTime.UtcNow
        let existingEventRange = {
            range = {
                start = now.AddHours -3.0
                finish = now.AddHours 3.0
            }
            gcalId = "123"
            isRoommateEvent = false
        }
//        let event = buildGoogleEvent
        let events = [existingEventRange]

        let desiredRange = {
            start = now
            finish = now.AddMinutes 15.0
        }

        let result = timeRangeIntersects existingEventRange.range desiredRange
        result |> should equal true
        let result = determineWhatToDo events desiredRange
        result |> should equal (Nothing "busy")
        ()

    [<Fact>]
    let ``merges adjacent roommate reservations``() =
        let now = System.DateTime.UtcNow
        let event = {range={start=now.AddMinutes -3.0;finish= now.AddMinutes 12.0};gcalId="123";isRoommateEvent=true}
        let events = [event]

        let desiredRange = {
            start = now.AddMinutes 12.0
            finish = now.AddMinutes 27.0
        }

        let result = determineWhatToDo events desiredRange

        let newStart = now.AddMinutes -3.0
        let newEnd = desiredRange.finish

        let (resultCalid,resultStart,resultEnd) = match result with
                                                    | UpdateEvent (a,b,c) -> (a,b,c)
                                                    | _ -> failwith "oops"
        resultCalid |> should equal "123"
        resultStart |> should (equalWithin (System.TimeSpan.FromSeconds 1.0)) newStart
        resultEnd |> should (equalWithin (System.TimeSpan.FromSeconds 1.0)) newEnd

    [<Fact>]
    let ``detects when TimeRanges partially intersect``() =
        let now = System.DateTime.UtcNow
        let range1 = {start = now.AddHours -3.0; finish =now.AddHours -1.0}
        let range2 = {start = now.AddHours -2.0;finish = now.AddHours 1.0}
        timeRangeIntersects range1 range2 |> should equal true
        ()

    [<Fact>]
    let ``detects intersection when TimeRange covers another``() =
        let now = System.DateTime.UtcNow
        let range1 = {start=now.AddHours -3.0;finish=now.AddHours 3.0}
        let range2 = {start=now.AddHours -2.0;finish=now.AddHours 1.0}
        timeRangeIntersects range1 range2 |> should equal true
        ()

    [<Fact>]
    let ``detects when TimeRanges don't intersect``() =
        let now = System.DateTime.UtcNow
        let range1 = {start=now.AddHours -3.0;finish=now.AddHours -1.0}
        let range2 = {start=now.AddHours 2.0;finish=now.AddHours 5.0}
        timeRangeIntersects range1 range2 |> should equal false
