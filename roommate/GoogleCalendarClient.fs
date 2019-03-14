namespace Roommate

module GoogleCalendarClient =

    open System.Threading
    open System
    open System
    open Google.Apis.Auth.OAuth2
    open Google.Apis.Auth.OAuth2.Flows
    open Google.Apis.Calendar.v3;
    open Google.Apis.Calendar.v3.Data;
    open Google.Apis.Services;
    open Google.Apis.Util.Store;
    open RoommateConfig
    open System
    open System.Threading

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

    let pollForAttendee (calendarService:CalendarService) eventId calendarId =
        // todo: better
        let mutable keepGoing = true
        let mutable count = 0
        let maxCount = 20
        let mutable lastResult = None
        while keepGoing do
            if count > maxCount then
                keepGoing <- false

            let getRequest = calendarService.Events.Get(calendarId,eventId)
            lastResult <- Some (getRequest.Execute())
            let newStatus = singleAttendeesStatus lastResult.Value
            printfn "new status: %s" (lastResult.Value.Attendees.Item(0).ResponseStatus)
            if newStatus = "needsAction" then
                printfn "still waiting.."
                count <- count + 1
                Thread.Sleep(1000)
            else
                keepGoing <- false
        match lastResult with
        | Some x -> Ok x
        | None -> Result.Error "Error while polling"

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

    let editEvent (calendarService:CalendarService) calId event =
        async {
            (*
            plan:
             - store original event start/end times
             - pull target calendar ID from attendee
             - query events from it within the time range of our event + 15 minutes
             - if the only event is ours, capture its ID and proceed. else quit.
             - edit roommate's event
             - poll and check:
               - attendee's status (to watch for "declined")
               - attendee's event (to watch for it to grow)
             - then, if:
               - attendee's event grows to match roomate's event -> success
               - status goes to declined ->
                 - edit roommate event back to its original end time
                 - log failure
               - timeout ->
                 - edit roommate event back to its original end time
                 - log failure
            *)

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

    let approxEqual (a:DateTime) (b:DateTime) =
        (a - b).Duration() < TimeSpan.FromMinutes(1.0)

    let containsAttendee (e:Event) roommateCalId =
        printfn "event attendees:"
        e.Attendees |> Seq.map (fun a -> a.Email) |> Seq.reduce (sprintf "%s,%s") |> printfn "%s"
        e.Attendees |> Seq.tryFind(fun a -> a.Email = roommateCalId) |> (fun x -> x.IsSome)

    let editAssociatedEventLength (calendarService:CalendarService) roommateCalId roomCalId eventId (start:DateTime) (finish:DateTime) =
        async {
            let! roomEvent = calendarService.Events.Get(roomCalId,eventId).ExecuteAsync() |> Async.AwaitTask
            let roommateEventReq = calendarService.Events.List(roommateCalId)
            roommateEventReq.TimeMin <- (roomEvent.Start.DateTime)
            roommateEventReq.MaxResults <- Nullable 50
            let! roommateEvents = roommateEventReq.ExecuteAsync() |> Async.AwaitTask

//            printfn "looking for attendee %s" roomCalId
            let eventsWithAttendee= roommateEvents.Items |> Seq.where (fun e -> containsAttendee e roomCalId)
            printfn "found %d events with attendee" (eventsWithAttendee |> Seq.length)
            let roommateEvent = eventsWithAttendee |> Seq.find (fun e -> (approxEqual e.Start.DateTime.Value roomEvent.Start.DateTime.Value) && (approxEqual e.End.DateTime.Value roomEvent.End.DateTime.Value))
            printfn "found the event! %s" (roommateEvent.ToString())

            roommateEvent.Start.DateTime <- System.Nullable start
            roommateEvent.End.DateTime <- System.Nullable finish
            let! editResult = editEvent calendarService roommateCalId roommateEvent
            return Ok editResult
        }

    let fetchEvents (calendarService:CalendarService) (LongCalId calendarId) =
        async {
            let request = calendarService.Events.List(calendarId)
            request.TimeMin <-System.Nullable DateTime.Now
            request.ShowDeleted <- System.Nullable false
            request.SingleEvents <- System.Nullable true
            request.MaxResults <- System.Nullable 10
            request.OrderBy <- System.Nullable EventsResource.ListRequest.OrderByEnum.StartTime

            return! request.ExecuteAsync() |> Async.AwaitTask
        }

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
