namespace RoommateLambda

module CalendarFetcher =

    open System
    open System.IO
    open System.Threading
    
    open Google.Apis.Auth.OAuth2;
    open Google.Apis.Calendar.v3;
    open Google.Apis.Calendar.v3.Data;
    open Google.Apis.Services;
    open Google.Apis.Util.Store;
    open Google.Apis.Services

    let fetchEvents () =

        let scopes = [CalendarService.Scope.CalendarReadonly]
        let stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read)
        let tempFile = new FileDataStore("google-filedatastore", true)
        let credential = GoogleWebAuthorizationBroker.AuthorizeAsync(GoogleClientSecrets.Load(stream).Secrets, scopes, "user", CancellationToken.None, tempFile).Result

        // Create the service
        let bar = new BaseClientService.Initializer()
        bar.ApplicationName <- "lkjsdf"
        bar.HttpClientInitializer <- credential
        let service = new CalendarService(bar)

         // Define parameters of request.
        let klienCalendarId = "atomicobject.com_3935353434383037353937@resource.calendar.google.com"
        let request = service.Events.List(klienCalendarId)
        request.TimeMin <-System.Nullable DateTime.Now
        request.ShowDeleted <- System.Nullable false
        request.SingleEvents <- System.Nullable true
        request.MaxResults <- System.Nullable 10
        request.OrderBy <- System.Nullable EventsResource.ListRequest.OrderByEnum.StartTime

        // Execute the request
        let events = request.Execute()
        events
        
    let printEvents (events:Events) =
        printfn "summary %s" events.Summary
        printfn "description %s" events.Description
        
        match events.Items.Count with
        | 0 -> printfn "No upcoming events found."
        | n -> printfn "Got %d events:" n
        
        events.Items |> Seq.iter (fun e -> 
            let start = e.Start.DateTime.ToString()
            Console.WriteLine("{0} ({1})", e.Summary, start)
        )
        
