namespace Roommate

module Messages =

    type CalendarEvent = {
        s: string
        e: string
    }

    type CalendarUpdate = {
        time: string
        events: CalendarEvent list
    }

    type UpdateRequest = {
        boardId: string
    }

    (*
    Example json data. Paste this into AWS console to test sending messages.
    {
        "time" : "2018-09-28T12:00:00Z",
        "events" : [
            { "s" : "2018-09-29T12:15:00Z", "e" : "2018-09-28T12:30:00Z" },
            { "s" : "2018-09-29T12:15:00Z", "e" : "2018-09-28T12:30:00Z" },
            { "s" : "2018-09-29T12:15:00Z", "e" : "2018-09-28T12:30:00Z" },
            { "s" : "2018-09-29T12:15:00Z", "e" : "2018-09-28T12:30:00Z" },
            { "s" : "2018-09-29T12:15:00Z", "e" : "2018-09-28T12:30:00Z" },
            { "s" : "2018-09-29T12:15:00Z", "e" : "2018-09-28T12:30:00Z" },
            { "s" : "2018-09-29T12:15:00Z", "e" : "2018-09-28T12:30:00Z" },
            { "s" : "2018-09-29T12:15:00Z", "e" : "2018-09-28T12:30:00Z" },
            { "s" : "2018-09-29T12:15:00Z", "e" : "2018-09-28T12:30:00Z" },
            { "s" : "2018-09-29T12:30:00Z", "e" : "2018-09-28T12:45:00Z" }
        ]
    }
    *)
