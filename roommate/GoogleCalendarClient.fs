namespace Roommate

module GoogleCalendarClient =

    open Google.Apis.Auth.OAuth2
    open Google.Apis.Auth.OAuth2.Flows
    open Google.Apis.Calendar.v3;
    open Google.Apis.Calendar.v3.Data;
    open Google.Apis.Services;
    open Google.Apis.Util.Store;
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

    let fetchCalendarIds (calendarService:CalendarService) =
        async {
            let request = calendarService.CalendarList.List()
            let! result = request.ExecuteAsync() |> Async.AwaitTask
            return result.Items
                |> Seq.filter (fun cal -> cal.Summary.StartsWith("AO"))
                |> Seq.filter (fun cal -> cal.Summary.Contains("Social") |> not)
                |> Seq.map (fun item -> item.Id, item.Summary)
        }

    let createEvent (calendarService:CalendarService) calendarId attendee =
        async {
            let start = new EventDateTime(DateTime = System.Nullable (System.DateTime.Now.AddHours(12.0)))
            let finish = new EventDateTime(DateTime = System.Nullable (System.DateTime.Now.AddHours(12.0).AddMinutes(15.0)))

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

    let fetchEvents (calendarService:CalendarService) calendarId =
        async {
            let request = calendarService.Events.List(calendarId)
            request.TimeMin <-System.Nullable DateTime.Now
            request.ShowDeleted <- System.Nullable false
            request.SingleEvents <- System.Nullable true
            request.MaxResults <- System.Nullable 10
            request.OrderBy <- System.Nullable EventsResource.ListRequest.OrderByEnum.StartTime

            return! request.ExecuteAsync() |> Async.AwaitTask
        }

    let logEvents (logFn: string -> unit) (events:Events) =
        logFn (sprintf "\n==== %s %s ====" events.Summary events.Description)

        if events.Items.Count = 0 then
            logFn "No upcoming events found."

        let hoursMinutes (d:DateTime) = sprintf "%2d:%02d" d.TimeOfDay.Hours d.TimeOfDay.Minutes

        let someOrBust = function
                | None -> failwith "oops"
                | Some opt -> opt

        events.Items
            |> Seq.map (fun e -> e.Start.DateTime |> Option.ofNullable,e.End.DateTime |> Option.ofNullable,e.Summary)
            |> Seq.filter (fun (a,b,_) -> a.IsSome && b.IsSome)
            |> Seq.map (fun (a,b,c) -> a |> someOrBust |> hoursMinutes, b |> someOrBust |> hoursMinutes, c)
            |> Seq.iter (fun (a,b,c) -> logFn (sprintf "  %s-%s  %s" a b c))

    let activateWebhook (calendarService:CalendarService) calendarId url =
        async {
            let guid = Guid.NewGuid().ToString()
            // https://developers.google.com/calendar/v3/push#making-watch-requests
            // different ID. calendar id?
            // want to be able to cancel-all without grepping logs
            let channel = new Channel(Address = url, Type = "web_hook",Id = guid)

            let request = calendarService.Events.Watch(channel,calendarId)

            // Execute the request
            let! result = request.ExecuteAsync() |> Async.AwaitTask
            printfn "watch result: %s" (result.ToString())
        }

    let cancelWebhook (calendarService:CalendarService) calendarId url =
        ()

    let summarizeEvent (event:Event) =
        sprintf "%s\n" event.Summary +
        sprintf "%s - %s\n" (event.Start.DateTime.ToString()) (event.End.DateTime.ToString()) +
        sprintf "%s\n" event.Description +
        sprintf "location: %s\n" event.Location +
        sprintf "calendar: %s\n" (event.Organizer.DisplayName)
