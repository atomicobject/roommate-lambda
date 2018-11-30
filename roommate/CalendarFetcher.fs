namespace Roommate

open Google.Apis.Auth.OAuth2
module CalendarFetcher =

    open System
    open System.IO
    open System.Threading
    
    open Google.Apis.Auth.OAuth2
    open Google.Apis.Auth.OAuth2.Flows
    open System
    open Google.Apis.Auth.OAuth2;
    open Google.Apis.Calendar.v3;
    open Google.Apis.Calendar.v3.Data;
    open Google.Apis.Services;
    open Google.Apis.Util.Store;
    open Google.Apis.Services

        
    let commonSignIn clientId clientSecret dataStore =
        let scopes = [CalendarService.Scope.CalendarReadonly]
        
        async {
            let! credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                                ClientSecrets( ClientId = clientId, ClientSecret = clientSecret),
                                scopes, "user", CancellationToken.None, dataStore) |> Async.AwaitTask
            printfn "UserId: %s" credential.UserId
            // Create the service
            let bar = new BaseClientService.Initializer(
                        ApplicationName = "roommate-test",
                        HttpClientInitializer = credential )
            let service = new CalendarService(bar)
            return service
        }
        
    let accessTokenSignIn fullJson clientId clientSecret accessToken refreshToken =
        let deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<Responses.TokenResponse>(fullJson)
        let dataStore = new google_data_store.MyStore(deserialized)
        commonSignIn clientId clientSecret dataStore
            
    let humanSignIn clientId clientSecret =
        let dataStore = new FileDataStore("google-filedatastore", true)
//        let dataStore = new google_data_store.MyStore("google-filedatastore", true)
        commonSignIn clientId clientSecret dataStore
    
    let serviceAccountSignIn serviceAccountEmail serviceAccountPrivKey serviceAccountAppName =
        // https://gist.github.com/tjmoore/6947d152eb5cfa569ef1
        let scopes = [CalendarService.Scope.CalendarReadonly]

        let init = (new ServiceAccountCredential.Initializer(serviceAccountEmail, Scopes = scopes))
                    .FromPrivateKey(serviceAccountPrivKey)
        let cred = new ServiceAccountCredential(init)
        let service = new CalendarService(new BaseClientService.Initializer(
                                                HttpClientInitializer = cred, 
                                                    ApplicationName = serviceAccountAppName))
        async {
            return service
        }

    let apiKeySignIn apiKey =
        let scopes = [CalendarService.Scope.CalendarReadonly]
        let tempFile = new FileDataStore("google-filedatastore", true)
        
        async {
            let bar = new BaseClientService.Initializer(
                        ApplicationName = "roommate",
                        ApiKey = apiKey )
            let service = new CalendarService(bar)
            return service
        }
    let printCalendars (calendarService:CalendarService) =
        async {
            let request = calendarService.CalendarList.List()
            let! result = request.ExecuteAsync() |> Async.AwaitTask
            match result.Items.Count with
            | 0 -> printfn "0 results!"
            | _ ->
                printfn "%d results:" (result.Items.Count)
                let aogr_rooms = result.Items 
                                    |> Seq.filter (fun cal -> cal.Summary.Contains("AOGR"))
                                    |> Seq.filter (fun cal -> cal.Summary.Contains("Social") |> not)
                                    
                aogr_rooms |> Seq.iter (fun item -> printfn "%s,\t%s" item.Id item.Summary)
                printfn ""
                printfn "export CALENDAR_IDS=%s" (aogr_rooms |> Seq.map (fun i -> i.Id) |> Seq.reduce (sprintf "%s,%s"))
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
        
    let printEvents (events:Events) =
        printfn "\n==== %s %s ====" events.Summary events.Description
        
        if events.Items.Count = 0 then
            printfn "No upcoming events found."
        
        let hoursMinutes (d:DateTime) = sprintf "%2d:%02d" d.TimeOfDay.Hours d.TimeOfDay.Minutes
            
        let someOrBust = function
                | None -> failwith "oops"
                | Some opt -> opt
        
        events.Items 
            |> Seq.map (fun e -> e.Start.DateTime |> Option.ofNullable,e.End.DateTime |> Option.ofNullable,e.Summary)
            |> Seq.filter (fun (a,b,_) -> a.IsSome && b.IsSome)
            |> Seq.map (fun (a,b,c) -> a |> someOrBust |> hoursMinutes, b |> someOrBust |> hoursMinutes, c)
            |> Seq.iter (fun (a,b,c) -> printfn "  %s-%s  %s" a b c)
            
    let activateWebhook (calendarService:CalendarService) calendarId url =
        async {
            let guid = Guid.NewGuid().ToString()
            // https://developers.google.com/calendar/v3/push#making-watch-requests
            let channel = new Channel(Address = url, Type = "web_hook",Id = guid)
            
            let request = calendarService.Events.Watch(channel,calendarId)

            // Execute the request
            let! result =request.ExecuteAsync() |> Async.AwaitTask
            printfn "watch result: %s" (result.ToString())
        }
        
        
