namespace Roommate.Tests


module RoommateConfigTest =
    open Xunit
    open FsUnit
    open Roommate
    open Roommate.RoommateConfig

    let exampleJson = """{"myCalendar":"mine","meetingRooms":{"Ada":"abc"},"boardAssignments":{"789":["12:34"]}}"""

    // to strip indentation:
    let squish =
        Newtonsoft.Json.JsonConvert.DeserializeObject
        >> Newtonsoft.Json.JsonConvert.SerializeObject

    [<Fact>]
    let ``serializes config``() =
        let config : RoommateConfig.RoommateConfig = {
            boardAssignments = ["789",["12:34"]] |> Map.ofList
            meetingRooms = ["Ada","abc"] |> Map.ofList
            myCalendar = "mine"
        }

        let result = RoommateConfig.serializeConfig config
        result |> squish |> should equal exampleJson

    [<Fact>]
    let ``deserializes config``() =
        let result = RoommateConfig.deserializeConfig exampleJson
        let expectation : RoommateConfig.RoommateConfig = {
            boardAssignments = ["789",["12:34"]] |> Map.ofList
            meetingRooms = ["Ada","abc"] |> Map.ofList
            myCalendar = "mine"
        }
        result |> should equal expectation
