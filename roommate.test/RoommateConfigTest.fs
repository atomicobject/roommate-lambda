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

    [<Fact>]
    let ``determines short name from full calendar name``() =
        shortName "AOGR-2-Cerf & Kahn (x23456) (2) [Telephone]" |> should equal "AOGR-2-Cerf & Kahn"
        shortName "AOGR-1-Hopper (x12345) (10) [TV, Telephone]" |> should equal "AOGR-1-Hopper"
        shortName "AOA2-2-Radium (x5432) (4) [TV, Telephone]" |> should equal "AOA2-2-Radium"

    [<Fact>]
    let ``shorten cal ID to just the number`` () =
        shorten (LongCalId "atomicobject.com_234523452345@resource.calendar.google.com") |> should equal "234523452345"

    [<Fact>]
    let ``lengthen cal ID number to full ID string`` () =
        lengthen "234523452345" |> should equal (LongCalId "atomicobject.com_234523452345@resource.calendar.google.com")
