namespace Roommate

module Types =

    type EventExtension = {
        eventId : string
        oldRange : TimeUtil.TimeRange
        newRange : TimeUtil.TimeRange
    }

    type RoommateEventAttendee = {
        name: string
        email: string
        responseStatus: string // todo: enum
    }
    type RoommateEvent = {
        gCalId: string
        timeRange: TimeUtil.TimeRange
        creatorEmail: string
        attendees: RoommateEventAttendee list
        } // attendees - name, email, responseStatus
        // updated
