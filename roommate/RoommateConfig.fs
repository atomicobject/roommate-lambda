namespace Roommate

module RoommateConfig =
    type MeetingRoom =
        {calendarId: string
         name: string}

    type BoardAssignment =
        {boardId: string // mac address?
         calendarId: string}

    type RoommateConfig =
        {myCalendar: string // create reservations on this calendar
         meetingRooms: Map<string,string>
         boardAssignments: Map<string,string list>}

    let serializeIndented o : string =
        Newtonsoft.Json.JsonConvert.SerializeObject(o, Newtonsoft.Json.Formatting.Indented)

    let serializeConfig(config: RoommateConfig): string =
        serializeIndented config

    let deserializeConfig(json: string): RoommateConfig =
        Newtonsoft.Json.JsonConvert.DeserializeObject<RoommateConfig>(json)

    let defaultConfig: RoommateConfig =
        {myCalendar = "calendar ID to create events on"
         meetingRooms = ["name","12345"] |> Map.ofList
         boardAssignments =
             ["board ID", ["calendarID board is assigned to"]] |> Map.ofList}

    let looukpCalByName config search =
        config.meetingRooms |> Map.toList |> List.map( fun (a,b) -> {name=a;calendarId=b})
        |> List.tryFind (fun room -> room.name.ToLower().Contains search)
        |> function
            | Some room -> room
            | None -> failwith (sprintf "no room found matching %s (check your config?)" search)

    let tryLookupCalById config search =
        config.meetingRooms |> Map.toList |> List.map( fun (a,b) -> {name=a;calendarId=b})
        |> List.tryFind (fun room -> room.calendarId = search)

    let lookupCalById config search =
        tryLookupCalById config search
        |> function
            | Some room -> room
            | None -> failwith (sprintf "no room found matching %s (check your config?)" search)

    let allCalendarIds config =
        config.meetingRooms |> Map.toList |> List.map snd

    let boardsForCalendar config calendarId =
        config.boardAssignments
            |> Map.tryFind calendarId
            |> function
                | Some boards -> boards
                | None -> []







