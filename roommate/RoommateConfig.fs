namespace Roommate

module RoommateConfig =
    type MeetingRoom = {
        calendarId: string
        name: string
    }
    type BoardAssignment = {
        boardId: string // mac address?
        calendarId: string
    }
    type RoommateConfig = {
        myCalendar : string // create reservations on this calendar
        meetingRooms: MeetingRoom list
        boardAssignments: BoardAssignment list
    }

    let serializeConfig (config:RoommateConfig) : string =
        Newtonsoft.Json.JsonConvert.SerializeObject config