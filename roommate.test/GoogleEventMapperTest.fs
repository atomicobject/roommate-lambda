namespace Roommate.Tests

module GoogleEventMapperTest =
    // todo: move to separate file
    let eventJson = """
{
    "anyoneCanAddSelf": null,
    "attachments": null,
    "attendees": [
        {
            "additionalGuests": null,
            "comment": null,
            "displayName": "conference-room-name",
            "email": "ajksdlfjaskdlfjasdklfjasdklf@resource.calendar.google.com",
            "id": null,
            "optional": null,
            "organizer": null,
            "resource": true,
            "responseStatus": "accepted",
            "self": true,
            "ETag": null
        }
    ],
    "attendeesOmitted": null,
    "colorId": null,
    "conferenceData": null,
    "created": "2019-03-16T03:05:26.000Z",
    "creator": {
        "displayName": null,
        "email": "foobar@foobar-12345.iam.gserviceaccount.com",
        "id": null,
        "self": null
    },
    "description": "This is the event's description.",
    "end": {
        "date": null,
        "dateTime": "2019-03-15T23:14:59-04:00",
        "timeZone": null,
        "ETag": null
    },
    "endTimeUnspecified": null,
    "etag": "\"12345\"",
    "extendedProperties": null,
    "gadget": null,
    "guestsCanInviteOthers": null,
    "guestsCanModify": null,
    "guestsCanSeeOtherGuests": null,
    "hangoutLink": null,
    "htmlLink": "https://www.google.com/calendar/event?eid=jkljkljkljkl",
    "iCalUID": "qwerqwerqwer@google.com",
    "id": "123123123id",
    "kind": "calendar#event",
    "location": "conference-room-name",
    "locked": null,
    "organizer": {
        "displayName": "the-sub-calendar",
        "email": "foo.com_890890890890890890@group.calendar.google.com",
        "id": null,
        "self": null
    },
    "originalStartTime": null,
    "privateCopy": null,
    "recurrence": null,
    "recurringEventId": null,
    "reminders": {
        "overrides": null,
        "useDefault": true
    },
    "sequence": 0,
    "source": null,
    "start": {
        "date": null,
        "dateTime": "2019-03-15T23:00:01-04:00",
        "timeZone": null,
        "ETag": null
    },
    "status": "confirmed",
    "summary": "event title",
    "transparency": null,
    "updated": "2019-03-16T03:05:29.528Z",
    "visibility": null
}
"""
    open Xunit
    open FsUnit
    open Roommate

    type GoogleEvent = Google.Apis.Calendar.v3.Data.Event
    let exampleJson = """{"myCalendar":"mine","meetingRooms":{"Ada":"abc"},"boardAssignments":{"789":["12:34"]}}"""

    // to strip indentation:
    let squish =
        Newtonsoft.Json.JsonConvert.DeserializeObject
        >> Newtonsoft.Json.JsonConvert.SerializeObject

    [<Fact>]
    let ``maps event``() =
        let event = Newtonsoft.Json.JsonConvert.DeserializeObject<GoogleEvent>(eventJson)
        let result = GoogleEventMapper.mapEvent event
        let expected: GoogleEventMapper.RoommateEvent = {
            gCalId = "123123123id"
            startTime = System.DateTime.Parse("03/15/2019 23:00:01")
            endTime = System.DateTime.Parse("03/15/2019 23:14:59")
            creatorEmail = "foobar@foobar-12345.iam.gserviceaccount.com"
            attendees = [{
                name = "conference-room-name"
                email = "ajksdlfjaskdlfjasdklfjasdklf@resource.calendar.google.com"
                responseStatus = "accepted"
            }]
        }
        result |> should equal expected


    // todo:
    // todo: all-day events (null start/endtime?)
