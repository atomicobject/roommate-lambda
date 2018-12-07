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
