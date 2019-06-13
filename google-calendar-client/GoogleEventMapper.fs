namespace Roommate

module GoogleEventMapper =

    open Google.Apis.Calendar.v3.Data;

    let mapEvent (event: Event): Types.RoommateEvent =
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

    let isRoommateEvent (event:Types.RoommateEvent) =
        event.creatorEmail.StartsWith("roommate") && event.creatorEmail.EndsWith(".gserviceaccount.com")

