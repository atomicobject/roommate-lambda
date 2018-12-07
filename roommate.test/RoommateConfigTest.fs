namespace Roommate.Tests

open Xunit
open FsUnit
open Roommate

module RoommateConfigTest =
    open Roommate.RoommateConfig

    let exampleJson = """{"myCalendar":"mine","meetingRooms":[{"calendarId":"abc","name":"Ada"}],"boardAssignments":[{"boardId":"12:34","calendarId":"789"}]}"""
    let squish json =
        json
        |> Newtonsoft.Json.JsonConvert.DeserializeObject<RoommateConfig>
        |> Newtonsoft.Json.JsonConvert.SerializeObject

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
