namespace Roommate
open TimeUtil

module GoogleCalendarClient =

    open System.Threading
    open System
    open Google.Apis.Auth.OAuth2
    open Google.Apis.Calendar.v3;
    open Google.Apis.Calendar.v3.Data;
    open Google.Apis.Services; // necessary for BaseClientService below
    open Google.Apis.Util.Store;
    open RoommateConfig

    let scopes = [CalendarService.Scope.CalendarReadonly;CalendarService.Scope.CalendarEvents]
    let humanSignIn clientId clientSecret =
        let dataStore = new FileDataStore("google-filedatastore", true)

        async {
            let! credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                                ClientSecrets( ClientId = clientId, ClientSecret = clientSecret),
                                scopes, "user", CancellationToken.None, dataStore) |> Async.AwaitTask
            return new CalendarService(
                            new BaseClientService.Initializer(
                                ApplicationName = "roommate-test",
                                HttpClientInitializer = credential ) )
        }

    let serviceAccountSignIn serviceAccountEmail serviceAccountPrivKey serviceAccountAppName =
        // https://gist.github.com/tjmoore/6947d152eb5cfa569ef1

        let init = (new ServiceAccountCredential.Initializer(serviceAccountEmail, Scopes = scopes))
                    .FromPrivateKey(serviceAccountPrivKey)
        let cred = new ServiceAccountCredential(init)
        let service = new CalendarService(new BaseClientService.Initializer(
                                                HttpClientInitializer = cred,
                                                    ApplicationName = serviceAccountAppName))
        async {
            return service
        }

    type CalNameAndId = {
        calId:LongCalId
        name:string
    }

    let fetchCalendarIds (calendarService:CalendarService) =
        async {
            let request = calendarService.CalendarList.List()
            let! result = request.ExecuteAsync() |> Async.AwaitTask
            return result.Items
                |> Seq.filter (fun cal -> cal.Summary.StartsWith("AO"))
                |> Seq.filter (fun cal -> cal.Summary.Contains("Social") |> not)
                |> Seq.map (fun item -> {calId=LongCalId item.Id;name=item.Summary})
        }

    let singleAttendeesStatus (event:Event) =
        // todo: handle attendees length != 1
        (event.Attendees.Item(0).ResponseStatus)


    let x () = Some 5

    type PollResult<'T> =
        | Success of 'T * int
        | Timeout of int

    let private pollNTimesOrUntil (numTimes:int) (fn: (unit -> 'T option)) : PollResult<'T> =
        // todo: better
        let mutable keepGoing = true
        let mutable count = 0
        let mutable result : 'T option = None
        while keepGoing do
            if count > numTimes then
                keepGoing <- false
            else
                count <- count + 1
                result <- fn ()
                if result |> Option.isSome then
                    keepGoing <- false
                else
                    printfn "waiting.."
                    Thread.Sleep(1000)

        match result with
        | None -> Timeout count
        | Some x -> Success (x,count)

    let private pollForAttendee (calendarService:CalendarService) eventId calendarId =
        let pollFn () =
            let getRequest = calendarService.Events.Get(calendarId,eventId)
            let result = getRequest.Execute()
            let newStatus = singleAttendeesStatus result
            match newStatus with
            | "needsAction" -> None
            | _ -> Some result
        let pollResult = pollNTimesOrUntil 20 pollFn

        match pollResult with
        | Success (result,timeout) -> Ok result
        | Timeout count -> Result.Error (sprintf "Timed out after %d tries" count)

    let createEvent (calendarService:CalendarService) calendarId (LongCalId attendee) start finish =
        async {
            let start = new EventDateTime(DateTime = System.Nullable start)
            let finish = new EventDateTime(DateTime = System.Nullable finish)

            let room = new EventAttendee(Email = attendee)
            let event = new Event(
                            Start = start,
                            End = finish,
                            Summary = "Roommate reservation",
                            Attendees = [|room|],
                            Description = "This event was created by the roommate system.\n\n(Just testing so far. Ask Jordan and John about it!)"
                            )
            let request = calendarService.Events.Insert(event, calendarId)
            let! creationResult = request.ExecuteAsync() |> Async.AwaitTask
            printfn "Successfully created event on %s" calendarId
            printfn "initial attendee response status %s" (creationResult.Attendees.Item(0).ResponseStatus)
            let latestResult = pollForAttendee calendarService creationResult.Id calendarId

            let finalResult = latestResult
                                |> Result.map singleAttendeesStatus
                                |> function
                                    | Ok "needsAction" -> Result.Error "timed out, apparently"
                                    | Ok "accepted" -> Result.Ok creationResult
                                    | Ok "declined" -> Result.Error "declined"
                                    | Result.Error s -> Result.Error s
                                    | _ -> failwith "bonk"


            match finalResult with
            | Ok _ -> () |> ignore
            | Result.Error s ->
                printfn "Attendee status '%s'; removing event from Roommate's calendar" s
                let req = calendarService.Events.Delete(calendarId, creationResult.Id)
                let result = req.Execute()
                printfn "deletion result '%s'" result
            return finalResult

        }

    let private tryOrRevertEvent (calendarService:CalendarService) calId (event:Event) fn =
        let originalEvent = calendarService.Events.Get(calId,event.Id).Execute()

        let tryResult = fn ()

        match tryResult with
        | Ok x -> Ok x
        | Error e ->
            let restoredEvent = (calendarService.Events.Update(originalEvent,calId,originalEvent.Id)).Execute()
            printfn "Restored event %s to %s-%s" (restoredEvent.Id) (restoredEvent.Start.ToString()) (restoredEvent.End.ToString())
            Result.Error e

    let private editEvent (calendarService:CalendarService) calId event =
        async {

            let req = calendarService.Events.Update(event,calId,event.Id)
            let result = req.Execute()
            printfn "\nattendee %s" (Newtonsoft.Json.JsonConvert.SerializeObject(result.Attendees.Item(0)))

            let attendee_email = result.Attendees.Item(0).Email
            let req2 = calendarService.Calendars.Get( attendee_email )
            let cal2 = req2.Execute()
            printfn "\nattendee calendar? %s" (Newtonsoft.Json.JsonConvert.SerializeObject(cal2))

            let req3 = calendarService.Events.List(attendee_email)
            req3.TimeMin <- System.Nullable(result.Start.DateTime.Value.AddDays(-1.0))
            req3.TimeMax <- System.Nullable(result.Start.DateTime.Value.AddDays(1.0))
            let events = req3.Execute()

            printfn "\nattendee events %s"  (Newtonsoft.Json.JsonConvert.SerializeObject(events))


            return result
        }

    let private approxEqual (a:DateTime) (b:DateTime) =
        (a - b).Duration() < TimeSpan.FromMinutes(1.0)

    let private containsAttendee (e:Event) roommateCalId =
//        printfn "event attendees:"
        e.Attendees |> Seq.map (fun a -> a.Email) |> Seq.reduce (sprintf "%s,%s") |> printfn "%s"
        e.Attendees |> Seq.tryFind(fun a -> a.Email = roommateCalId) |> (fun x -> x.IsSome)

    let private editAssociatedEventLength (calendarService:CalendarService) roommateCalId roomCalId roommateEventId (start:DateTime) (finish:DateTime) =
        printfn "editAssociatedEventLength %s %s %s" roommateCalId roomCalId roommateEventId
        async {
            // first we get the event from the target room's calendar
            let! roomEvent = calendarService.Events.Get(roomCalId,roommateEventId).ExecuteAsync() |> Async.AwaitTask
            let roommateEvent = calendarService.Events.Get(roommateCalId,roommateEventId).Execute()

//            printfn "room event %s" (Newtonsoft.Json.JsonConvert.SerializeObject(roomEvent))
//            printfn "roommate event: %s" (Newtonsoft.Json.JsonConvert.SerializeObject(roommateEvent))

            // edit the event (which will send an update to the room, which may accept/decline the change)
            roommateEvent.Start.DateTime <- System.Nullable start
            roommateEvent.End.DateTime <- System.Nullable finish
            let! editResult = editEvent calendarService roommateCalId roommateEvent
            return Ok editResult
        }

    type EventExtension = {
        eventId : string
        oldRange : TimeRange
        newRange : TimeRange
    }

    type EditResult =
        | Accepted of Event
        | Rejected

    type ReceivedEditResult =
        | AcceptedEdit
        | RejectedEdit
        | EditError of string

    let private pollForReceivedEdit (calendarService:CalendarService) (ext:EventExtension) attendeeCalendarId =
        let pollFn () =
            let getRequest = calendarService.Events.Get(attendeeCalendarId,ext.eventId)
            let result = getRequest.Execute()
            let newStatus = singleAttendeesStatus result
            let newTimeRange = {start=result.Start.DateTime.Value;finish=result.End.DateTime.Value}
//            printfn "%s,%s" newStatus (newTimeRange.ToString())
            match newStatus,newTimeRange with
            | "needsAction",_ -> None
            | _,r when r = ext.oldRange -> None
            | "declined",r when r = ext.newRange -> Some Rejected
            | "accepted",r when r = ext.newRange -> Some (Accepted result)
            | a,b -> failwith (sprintf "unanticipated polling status %s, %s" a (b.ToString()))

        printfn "waiting for edit to be accepted by conference room calendar.."
        let pollResult = pollNTimesOrUntil 20 pollFn

        match pollResult with
        | Success (Accepted result,_) -> AcceptedEdit
        | Success (Rejected,_) -> RejectedEdit
        | Timeout count -> EditError (sprintf "Timed out after %d tries" count)

    let private editEventEndTime (calendarService:CalendarService) calId eventId endTime =
        let updatedEvent : Event = new Event();
        updatedEvent.End <- new EventDateTime()
        updatedEvent.End.DateTime <- System.Nullable(endTime)
        let req = calendarService.Events.Patch(updatedEvent,calId,eventId)
        req.Execute()

    let extendEvent (calendarService:CalendarService) (calId:string) (eventExtension:EventExtension) (LongCalId attendeeCalId) =
        let editResult = editEventEndTime calendarService calId eventExtension.eventId eventExtension.newRange.finish
        let receivedEditResult = pollForReceivedEdit calendarService eventExtension attendeeCalId
        match receivedEditResult with
        | AcceptedEdit -> Ok editResult
        | EditError e -> Result.Error e
        | RejectedEdit ->
            printfn "Edit was declined by room. Reverting.."
            let revertResult = editEventEndTime calendarService calId eventExtension.eventId eventExtension.oldRange.finish
            printfn "revert result: %s" (revertResult.ToString())

            Result.Error "Edit was declined by room."

    let fetchEvents (calendarService:CalendarService) (LongCalId calendarId) =
        let request = calendarService.Events.List(calendarId)
        request.TimeMin <-System.Nullable DateTime.Now
        request.TimeMax <- System.Nullable (DateTime.Now.AddDays(1.0))
        request.ShowDeleted <- System.Nullable false
        request.SingleEvents <- System.Nullable true
        request.OrderBy <- System.Nullable EventsResource.ListRequest.OrderByEnum.StartTime

        request.Execute()

    let isRoommateEvent (event:Event) =
        event.Creator.Email.StartsWith("roommate") && event.Creator.Email.EndsWith(".gserviceaccount.com")

    let logEvents (logFn: string -> unit) (events:Events) =
        logFn (sprintf "\n==== %s %s ====" events.Summary events.Description)

        if events.Items.Count = 0 then
            logFn "No upcoming events found."

        let hoursMinutes (d:DateTime) = sprintf "%2d:%02d" d.TimeOfDay.Hours d.TimeOfDay.Minutes

        let someOrBust = function
                | None -> failwith "oops"
                | Some opt -> opt

        events.Items
            |> Seq.filter (fun e ->
                                e.Start.DateTime |> Option.ofNullable |> Option.isSome
                                    && e.End.DateTime |> Option.ofNullable |> Option.isSome )
            |> Seq.iter (fun e ->
                let start = e.Start.DateTime |> Option.ofNullable |> someOrBust
                let fin = e.End.DateTime |> Option.ofNullable |> someOrBust
                logFn (sprintf "%s\t%s-%s\t%s\t%s id=%s" (start.ToString("MM/dd"))
                                               (start |> hoursMinutes)
                                               (fin |> hoursMinutes)
                                               e.Summary
                                               (if isRoommateEvent e then "(R)" else "")
                                               e.Id
                                               )
//                logFn (sprintf "%s" (serializeIndented e))
                )

    let activateExpiringWebhook (calendarService:CalendarService) (LongCalId calendarId) url expiration_ms =
        async {
            let d : int32 = DateTime.Now.Date.Day
            let subscriptionId = sprintf "roommate-lambda-%d-%s" d (shorten (LongCalId calendarId))
            // https://developers.google.com/calendar/v3/push#making-watch-requests
            let channel = new Channel(Address = url, Type = "web_hook",Id = subscriptionId, Expiration = System.Nullable expiration_ms)

            let request = calendarService.Events.Watch(channel,calendarId)

            // Execute the request
            let! result =request.ExecuteAsync() |> Async.AwaitTask
            return result
        }
    let activateWebhook (calendarService:CalendarService) (LongCalId calendarId) url =
        async {
            let subscriptionId = sprintf "roommate-tool-%s" (shorten (LongCalId calendarId))
            let channel = new Channel(Address = url, Type = "web_hook",Id = subscriptionId)

            let request = calendarService.Events.Watch(channel,calendarId)

            // Execute the request
            let! result =request.ExecuteAsync() |> Async.AwaitTask
            return result
        }

    let deactivateWebhook (calendarService:CalendarService) (LongCalId calendarId) url resourceId =
        async {
            (*
                https://developers.google.com/calendar/v3/push#making-watch-requests

                The Subscription ID is a value we specify at the time of creating the channel.
                The Resource ID comes from Google.

                Both are given to us in the HTTP Headers with each push notification, and
                can be seen in the Lambda logs.

            *)
            let subscriptionId = sprintf "roommate-tool-%s" (shorten (LongCalId calendarId))
            let channel = new Channel(ResourceId = resourceId, Id = subscriptionId)
            let request = calendarService.Channels.Stop(channel);

            // Execute the request
            let! result = request.ExecuteAsync() |> Async.AwaitTask
            return result
        }

    let summarizeEvent (event:Event) =
        sprintf "%s\n" event.Summary +
        sprintf "%s - %s\n" (event.Start.DateTime.ToString()) (event.End.DateTime.ToString()) +
        sprintf "%s\n" event.Description +
        sprintf "location: %s\n" event.Location +
        sprintf "calendar: %s\n" (event.Organizer.DisplayName)
