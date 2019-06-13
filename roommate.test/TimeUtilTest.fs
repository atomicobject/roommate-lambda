namespace Roommate.Tests


module TimeUtilTest =
    open Xunit
    open FsUnit
    open Roommate.TimeUtil
    open System

    [<Fact>]
    let ``detects when TimeRanges partially intersect``() =
        let now = System.DateTime.UtcNow
        let range1 = {start = now.AddHours -3.0; finish =now.AddHours -1.0}
        let range2 = {start = now.AddHours -2.0;finish = now.AddHours 1.0}
        timeRangeIntersects range1 range2 |> should equal true
        ()

    [<Fact>]
    let ``detects intersection when TimeRange covers another``() =
        let now = System.DateTime.UtcNow
        let range1 = {start=now.AddHours -3.0;finish=now.AddHours 3.0}
        let range2 = {start=now.AddHours -2.0;finish=now.AddHours 1.0}
        timeRangeIntersects range1 range2 |> should equal true
        ()

    [<Fact>]
    let ``detects when TimeRanges don't intersect``() =
        let now = System.DateTime.UtcNow
        let range1 = {start=now.AddHours -3.0;finish=now.AddHours -1.0}
        let range2 = {start=now.AddHours 2.0;finish=now.AddHours 5.0}
        timeRangeIntersects range1 range2 |> should equal false

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
