namespace Roommate

module GoogleEventMapper =

    open Google.Apis.Calendar.v3.Data;

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

    let mapEvent (event: Event): RoommateEvent =
        {
            gCalId = event.Id
            timeRange = {
                start = event.Start.DateTime.Value
                finish = event.End.DateTime.Value
            }
            creatorEmail = event.Creator.Email
            attendees = event.Attendees |> List.ofSeq |> List.map (fun a -> {
                name = a.DisplayName
                email = a.Email
                responseStatus = a.ResponseStatus
            })
        }
