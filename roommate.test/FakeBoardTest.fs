namespace Roommate.Tests


module FakeBoardTest =
    open Xunit
    open FsUnit
    open Roommate.FakeBoard
    open System


    [<Fact>]
    let ``startOfHour``() =
        let time = DateTime.Parse("2019-01-06T09:22:37.4239890-05:00")
        (roundDown time) |> should equal (DateTime.Parse("2019-01-06T09:15:00.0-05:00"))
        ()
