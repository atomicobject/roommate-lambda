namespace Roommate.Tests

open Xunit
open FsUnit
open Roommate

module CalendarWatcherTest =
    open Roommate.CalendarWatcher


    [<Fact>]
    let ``parses calendar ID from URI``() =
        let uri = "https://www.googleapis.com/calendar/v3/calendars/atomicobject.com_234523452345@resource.calendar.google.com/events?maxResults=250&alt=json"
        calIdFromURI uri |> should equal "atomicobject.com_234523452345@resource.calendar.google.com"
