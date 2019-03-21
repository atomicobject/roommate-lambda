namespace Roommate.Tests

open Xunit
open FsUnit
open Roommate

module RoommateLogicTest =
    open Google.Apis.Calendar.v3.Data

    [<Fact>]
    let ``parses calendar ID from URI``() =
        // from X-Goog-Resource-URI header in push notification
        let uri = "https://www.googleapis.com/calendar/v3/calendars/atomicobject.com_234523452345@resource.calendar.google.com/events?maxResults=250&alt=json"
        RoommateConfig.calIdFromURI uri |> should equal (RoommateConfig.LongCalId "atomicobject.com_234523452345@resource.calendar.google.com")


    let buildGoogleEventDateTime (time:System.DateTime) =
        new EventDateTime(DateTimeRaw = time.ToString("o"))

    let buildGoogleEvent (start:System.DateTime) (finish:System.DateTime) =
        new Event(Start = buildGoogleEventDateTime start, End=buildGoogleEventDateTime finish)

