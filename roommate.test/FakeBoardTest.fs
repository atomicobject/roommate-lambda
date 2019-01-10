namespace Roommate.Tests


module FakeBoardTest =
    open Xunit
    open FsUnit
    open Roommate.FakeBoard
    open System


    [<Fact>]
    let ``roundDown``() =
        let time = DateTime.Parse("2019-01-06T09:22:37.4239890-05:00")
        (roundDown time) |> should equal (DateTime.Parse("2019-01-06T09:15:00.0-05:00"))
        ()

    [<Fact>]
    let ``roundUp``() =
        let time = DateTime.Parse("2019-01-06T09:22:37.4239890-05:00")
        (roundUp time) |> should equal (DateTime.Parse("2019-01-06T09:30:00.0-05:00"))
        ()
