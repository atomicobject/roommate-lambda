namespace Roommate

module FakeBoard =
    open CalendarWatcher
    open Google.Apis.Calendar.v3.Data
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

    let getLights (events: RoommateEvent list) =
        let start = DateTime.Now |> roundDown
        let totalRange = {start=start;finish=start.AddHours(2.0)}
        let relevantEvents = events |> List.where (fun e -> timeRangeIntersects e.range totalRange)
        let slots = start |> timeSlots
        let currentEvent = relevantEvents |> List.tryFind (fun e -> timeRangeIntersects e.range slots.[0])

        slots |> List.map (fun slotRange ->
                    if currentEvent.IsSome && timeRangeIntersects currentEvent.Value.range slotRange then
                        CurrentMeeting
                    else if (relevantEvents |> List.exists (fun e -> timeRangeIntersects slotRange e.range)) then
                        Busy
                    else Available )


