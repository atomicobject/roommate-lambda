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
        startTime: System.DateTime
        endTime: System.DateTime
        creatorEmail: string
        attendees: RoommateEventAttendee list
        } // attendees - name, email, responseStatus
        // updated

    let mapEvent (event: Event): RoommateEvent =
        {
            gCalId = event.Id
            startTime = event.Start.DateTime.Value
            endTime = event.End.DateTime.Value
            creatorEmail = event.Creator.Email
            attendees = event.Attendees |> List.ofSeq |> List.map (fun a -> {
                name = a.DisplayName
                email = a.Email
                responseStatus = a.ResponseStatus
            })
        }
