namespace Roommate

module Types =
    type EventExtension = {
        eventId : string
        oldRange : TimeUtil.TimeRange
        newRange : TimeUtil.TimeRange
    }

