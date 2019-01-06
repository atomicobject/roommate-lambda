namespace Roommate

module GoogleCalendarClient =

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
            return! request.ExecuteAsync() |> Async.AwaitTask
        }

    let editEvent (calendarService:CalendarService) calId event =
        async {
            let req = calendarService.Events.Update(event,calId,event.Id)
            return! req.ExecuteAsync() |> Async.AwaitTask
        }

    let editAssociatedEventLength (calendarService:CalendarService) roommateCalId roomCalId eventId (start:DateTime) (finish:DateTime) =
        async {
            let! roomEvent = calendarService.Events.Get(roomCalId,eventId).ExecuteAsync() |> Async.AwaitTask
            let roommateEventReq = calendarService.Events.List(roommateCalId)
            roommateEventReq.TimeMin <- (roomEvent.Start.DateTime)
            roommateEventReq.MaxResults <- Nullable 50
            let! roommateEvents = roommateEventReq.ExecuteAsync() |> Async.AwaitTask

            let attendee = new Google.Apis.Calendar.v3.Data.EventAttendee()
            let roommateEvent = roommateEvents.Items |> Seq.find (fun e -> e.Attendees.Contains(attendee) && e.Start = roomEvent.Start && e.End = roomEvent.End)

            roommateEvent.Start.DateTime <- System.Nullable start
            roommateEvent.End.DateTime <- System.Nullable finish
            return! editEvent calendarService roommateCalId roommateEvent
        }

    let fetchRoommateEventsForRoom (calendarService:CalendarService) (LongCalId calendarId) =
        async {
            let request = calendarService.Events.List(calendarId)
            // todo: make sure we get long events that are still ongoing
            // (so maybe don't use TimeMin?)

            // can we request events created or organized by roommate account?
//            request.TimeMin <-System.Nullable DateTime.Now
//            request.ShowDeleted <- System.Nullable false
//            request.SingleEvents <- System.Nullable true
//            request.MaxResults <- System.Nullable 10
//            request.OrderBy <- System.Nullable EventsResource.ListRequest.OrderByEnum.StartTime

            return! request.ExecuteAsync() |> Async.AwaitTask
        }

    let fetchEvents (calendarService:CalendarService) (LongCalId calendarId) =
        // todo: convert all-day events to start/end, make sure they work correctly
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
                logFn (sprintf "%s" (serializeIndented e))
                )

    let activateWebhook (calendarService:CalendarService) (LongCalId calendarId) url =
        async {
            let subscriptionId = sprintf "roommate-tool-%s" (shorten (LongCalId calendarId))
            // https://developers.google.com/calendar/v3/push#making-watch-requests
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
