namespace Roommate

module Messages =

    type CalendarEvent = {
        s: string
        e: string
        r: bool
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

    type DeviceConnect = {
      clientId: string
      timestamp: int32
      eventType: string
      sessionIdentifier: string
      principalIdentifier: string
      versionNumber: int32
    }
