namespace Roommate

module TimeUtil =
    open System

    type TimeRange = {
        start : DateTime
        finish : DateTime
    }

    let timeRangeIntersects (r1:TimeRange) (r2:TimeRange) =
        let times = [ "1_start",r1.start;"1_end",r1.finish;"2_start",r2.start;"2_end",r2.finish]
        let sequence = times |> List.sortBy snd
        let sortedNames = sequence |> List.map fst
        match sortedNames with
        | ["1_start";"1_end";"2_start";"2_end"] -> false
        | ["2_start";"2_end";"1_start";"1_end"] -> false
        | ["1_start";"2_start";"1_end";"2_end"] -> true
        | ["2_start";"1_start";"2_end";"1_end"] -> true
        | ["1_start";"2_start";"2_end";"1_end"] -> true
        | ["2_start";"1_start";"1_end";"2_end"] -> true
        |_ -> failwith "unhandled time range sequence"

    let timeRangeContains (r:TimeRange) (d:DateTime) =
        r.start < d && d < r.finish

    let dateTimeFromUnixTime =
        int64 >> System.DateTimeOffset.FromUnixTimeSeconds >> (fun x -> x.UtcDateTime)

    let unixTimeFromDate (d:DateTime) =
        d |> DateTimeOffset |> fun x -> x.ToUnixTimeSeconds()



