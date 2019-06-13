namespace Roommate

module RoommateConfig =
    type ShortCalId = string // aliased to string so that it serializes cleanly
    type LongCalId = LongCalId of string

    type MeetingRoom =
        {calendarId: LongCalId
         name: string}

    type BoardAssignment =
        {boardId: string // mac address?
         calendarId: string}


    type RoommateConfig =
        {myCalendar: string // create reservations on this calendar
         meetingRooms: Map<string,ShortCalId>
         boardAssignments: Map<string,string list>}

    let shorten (LongCalId s) : ShortCalId =
        s.Split('_').[1].Split('@').[0]

    let lengthen (s:ShortCalId) : LongCalId =
        sprintf "atomicobject.com_%s@resource.calendar.google.com" s |> LongCalId

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
             ["calendarID",["board ID 1";"board ID 2"]] |> Map.ofList}

    let meetingRooms config =
        config.meetingRooms
            |> Map.toList
            |> List.map (fun (name,calId) ->{name=name;calendarId=lengthen calId})

    let looukpCalByName config search =
        meetingRooms config
        |> List.tryFind (fun room -> room.name.ToLower().Contains search)
        |> function
            | Some room -> room
            | None -> failwith (sprintf "no room found matching %s (check your config?)" search)

    let tryLookupCalById config search =
        meetingRooms config
        |> List.tryFind (fun room -> room.calendarId = search)

    let lookupCalById config search =
        let (LongCalId s) = search
        tryLookupCalById config search
        |> function
            | Some room -> room
            | None -> failwith (sprintf "no room found matching %s (check your config?)" s)

    let boardsForCalendar config (calendarId:LongCalId) =
        config.boardAssignments
            |> Map.tryFind (calendarId |> shorten)
            |> function
                | Some boards -> boards
                | None -> []

    let calIdFromURI (calURI:string) =
        calURI.Split('/') |> List.ofArray |> List.find (fun x -> x.Contains "atomicobject.com") |> LongCalId

    let shortName (calName:string) =
        calName.Split('(') |> List.ofArray |> List.head |> (fun s -> s.Trim())

    let allCalendarIds config =
        config.meetingRooms |> Map.toList |> List.map snd |> List.map lengthen
