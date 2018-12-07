namespace Roommate.Tests

open Xunit
open FsUnit
open Roommate

module RoommateConfigTest =
    open Roommate.RoommateConfig

    [<Fact>]
    let ``serializes config``() =
        let config : RoommateConfig.RoommateConfig = {
            boardAssignments = [{boardId="12:34";calendarId="789"}]
            meetingRooms = [{name="Ada";calendarId="abc"}]
            myCalendar = "mine"
        }

        let result = RoommateConfig.serializeConfig config
        result |> should equal """{"myCalendar":"mine","meetingRooms":[{"calendarId":"abc","name":"Ada"}],"boardAssignments":[{"boardId":"12:34","calendarId":"789"}]}"""

    [<Fact>]
    let ``deserializes config``() =
        let json = """{"myCalendar":"mine","meetingRooms":[{"calendarId":"abc","name":"Ada"}],"boardAssignments":[{"boardId":"12:34","calendarId":"789"}]}"""
        let result = RoommateConfig.deserializeConfig json
        let expectation : RoommateConfig.RoommateConfig = {
            boardAssignments = [{boardId="12:34";calendarId="789"}]
            meetingRooms = [{name="Ada";calendarId="abc"}]
            myCalendar = "mine"
        }
        result |> should equal expectation
