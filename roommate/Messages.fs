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

    type ReservationRequest = {
        boardId: string
        start: int32
        finish: int32
    }

