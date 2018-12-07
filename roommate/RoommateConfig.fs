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
