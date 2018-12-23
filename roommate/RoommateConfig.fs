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
         meetingRooms: MeetingRoom list
         boardAssignments: BoardAssignment list}

    let serializeIndented o : string =
        Newtonsoft.Json.JsonConvert.SerializeObject(o, Newtonsoft.Json.Formatting.Indented)

    let serializeConfig(config: RoommateConfig): string =
        serializeIndented config

    let deserializeConfig(json: string): RoommateConfig =
        Newtonsoft.Json.JsonConvert.DeserializeObject<RoommateConfig>(json)

    let defaultConfig: RoommateConfig =
        {myCalendar = "calendar ID to create events on"
         meetingRooms =
             [{calendarId = "calendar ID of conference room"
               name = "name of conference room"}]
         boardAssignments =
             [{boardId = "board ID"
               calendarId = "calendarID board is assigned to"}]}

    let looukpCalByName config search =
        config.meetingRooms
        |> List.tryFind (fun room -> room.name.ToLower().Contains search)
        |> function
            | Some room -> room
            | None -> failwith (sprintf "no room found matching %s (check your config?)" search)

    let tryLookupCalById config search =
        config.meetingRooms
        |> List.tryFind (fun room -> room.calendarId = search)

    let lookupCalById config search =
        tryLookupCalById config search
        |> function
            | Some room -> room
            | None -> failwith (sprintf "no room found matching %s (check your config?)" search)

    let lookupCalendarForBoard config boardId =
        config.boardAssignments
            |> List.tryFind (fun ba -> ba.boardId = boardId)
            |> Option.map (fun ba -> ba.calendarId)

    let calIdFromURI (calURI:string) =
        calURI.Split('/') |> List.ofArray |> List.find (fun x -> x.Contains "@")

    let shortName (calName:string) =
        calName.Split('(') |> List.ofArray |> List.head |> (fun s -> s.Trim())






