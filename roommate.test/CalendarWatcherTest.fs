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
        let start = System.DateTime.UtcNow
        let finish = (System.DateTime.UtcNow.AddMinutes 15.0)
        let result = determineWhatToDo [] start finish
        result |> should equal (CreateEvent (start, finish))

    [<Fact>]
    let ``rejects events in the past``() =
        let start = System.DateTime.UtcNow.AddHours -1.0
        let finish = start.AddMinutes 15.0
        let result = determineWhatToDo [] start finish
        result |> should equal (Nothing "cannot create historic event")
        ()

    [<Fact>]
    let ``rejects invalid events``() =
        let finish = System.DateTime.UtcNow
        let start = (System.DateTime.UtcNow.AddMinutes 15.0)
        let result = determineWhatToDo [] start finish
        result |> should equal (Nothing "invalid event")
        ()

    let buildGoogleEvent start finish =
        new Event(Start = new EventDateTime(DateTime = System.Nullable start) , End= new EventDateTime(DateTime = System.Nullable finish))

    [<Fact>]
    let ``rejects when the room is already reserved``() =
        let event = buildGoogleEvent (System.DateTime.UtcNow.AddHours -3.0) (System.DateTime.UtcNow.AddHours 3.0)
        let events : Event list = [event]

        let start = System.DateTime.UtcNow
        let finish = (System.DateTime.UtcNow.AddMinutes 15.0)
        let result = determineWhatToDo events start finish
        result |> should equal (Nothing "busy")
        ()

    [<Fact>]
    let ``merges adjacent roommate reservations``() =

        let event = buildGoogleEvent (System.DateTime.UtcNow.AddMinutes -3.0) (System.DateTime.UtcNow.AddMinutes 12.0)
        event.Id <- "12345"
        let events = [event]

        let start = System.DateTime.UtcNow.AddMinutes 12.0
        let finish = System.DateTime.UtcNow.AddMinutes 27.0

        let result = determineWhatToDo events start finish

        let newStart = System.DateTime.UtcNow.AddMinutes -3.0
        let newEnd = finish

        result |> should equal (UpdateEvent ("12345",newStart,newEnd))
        ()

