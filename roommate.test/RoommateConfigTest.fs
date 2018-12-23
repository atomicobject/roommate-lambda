namespace Roommate.Tests


module RoommateConfigTest =
    open Xunit
    open FsUnit
    open Roommate
    open Roommate.RoommateConfig

    let exampleJson = """{"myCalendar":"mine","meetingRooms":[{"calendarId":"abc","name":"Ada"}],"boardAssignments":[{"boardId":"12:34","calendarId":"789"}]}"""

    // to strip indentation:
    let squish =
        Newtonsoft.Json.JsonConvert.DeserializeObject
        >> Newtonsoft.Json.JsonConvert.SerializeObject

    [<Fact>]
    let ``serializes config``() =
        let config : RoommateConfig.RoommateConfig = {
            boardAssignments = [{boardId="12:34";calendarId="789"}]
            meetingRooms = [{name="Ada";calendarId="abc"}]
            myCalendar = "mine"
        }

        let result = RoommateConfig.serializeConfig config
        result |> squish |> should equal exampleJson

    [<Fact>]
    let ``deserializes config``() =
        let result = RoommateConfig.deserializeConfig exampleJson
        let expectation : RoommateConfig.RoommateConfig = {
            boardAssignments = [{boardId="12:34";calendarId="789"}]
            meetingRooms = [{name="Ada";calendarId="abc"}]
            myCalendar = "mine"
        }
        result |> should equal expectation

    [<Fact>]
    let ``parses calendar ID from URI``() =
        // from X-Goog-Resource-URI header in push notification
        let uri = "https://www.googleapis.com/calendar/v3/calendars/atomicobject.com_234523452345@resource.calendar.google.com/events?maxResults=250&alt=json"
        calIdFromURI uri |> should equal "atomicobject.com_234523452345@resource.calendar.google.com"

    [<Fact>]
    let ``determines short name from full calendar name``() =
        shortName "AOGR-2-Cerf & Kahn (x23456) (2) [Telephone]" |> should equal "AOGR-2-Cerf & Kahn"
        shortName "AOGR-1-Hopper (x12345) (10) [TV, Telephone]" |> should equal "AOGR-1-Hopper"
        shortName "AOA2-2-Radium (x5432) (4) [TV, Telephone]" |> should equal "AOA2-2-Radium"
