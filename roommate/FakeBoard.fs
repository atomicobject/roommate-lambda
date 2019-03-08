namespace Roommate

module FakeBoard =
    open RoommateLogic
    open System
    open TimeUtil

    let roundDown (d:DateTime) =
        d.Date.AddHours(float d.Hour).AddMinutes((d.Minute / 15) * 15 |> float)

    let roundUp (d:DateTime) =
        d.AddMinutes 15.0 |> roundDown

    let timeSlots (d:DateTime) : TimeRange list =
        [0..7] |> List.map (fun i -> {start=d.AddMinutes(i * 15 |> float).AddSeconds(1.0);finish=d.AddMinutes(i * 15 + 15 |> float).AddSeconds(-1.0)})

    type LightState =
        | CurrentMeeting
        | Busy
        | Available

    let red = [0xFD;0x45;0x57]
    let green = [0x00;0xCD;0xBD]
    let yellow = [0xDC;0xAD;0x66]

    let printLed = function
                    | CurrentMeeting -> yellow
                    | Busy -> red
                    | Available -> green
                    >> (fun color -> TermColors.printBgColored color "  ")

    let getLights (events: RoommateEvent list) : (LightState*TimeRange) list =
        let start = DateTime.Now |> roundDown
        let totalRange = {start=start;finish=start.AddHours(2.0)}
        let relevantEvents = events |> List.where (fun e -> timeRangeIntersects e.range totalRange)
        let slots = start |> timeSlots
        let currentEvent = relevantEvents |> List.tryFind (fun e -> timeRangeIntersects e.range slots.[0])

        slots |> List.map (fun slotRange ->
                    if currentEvent.IsSome && timeRangeIntersects currentEvent.Value.range slotRange then
                        CurrentMeeting,slotRange
                    else if (relevantEvents |> List.exists (fun e -> timeRangeIntersects slotRange e.range)) then
                        Busy,slotRange
                    else Available,slotRange )

    let printLights (lights:(LightState*TimeRange) list) =
        let start = DateTime.Now |> roundDown
        let finish = start.AddHours 2.0

        printf "%d:%d  " start.Hour start.Minute
        lights |> List.map fst |> List.iter (fun x -> printLed x;printf "  ")
        printf "%d:%d" finish.Hour finish.Minute

    let chooseNextTime (lights:(LightState*TimeRange) list) =
        let numberedLights = lights |> List.mapi (fun i (lightState,timeRange) -> (lightState,timeRange,i))
        let maybeLight = numberedLights |> List.tryFind (function | (Available,_,_) -> true | _ -> false)
        if maybeLight.IsNone then
            printfn "room booked solid! no slots available."
            None
        else
            let light,range,pos = maybeLight.Value
            Some (range,pos)




