namespace Roommate

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

    let printCalendars clientId clientSecret =
        let scopes = [CalendarService.Scope.CalendarReadonly]
        let tempFile = new FileDataStore("google-filedatastore", true)
        
        async {
            let! credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                                ClientSecrets( ClientId = clientId, ClientSecret = clientSecret),
                                scopes, "user", CancellationToken.None, tempFile) |> Async.AwaitTask
            // Create the service
            let bar = new BaseClientService.Initializer(
                        ApplicationName = "roommate-test",
                        HttpClientInitializer = credential )
            let service = new CalendarService(bar)

            let request = service.CalendarList.List()

            // Execute the request
            let! result =request.ExecuteAsync() |> Async.AwaitTask
            let aogr_rooms = result.Items 
                                |> Seq.filter (fun cal -> cal.Summary.Contains("AOGR"))
                                |> Seq.filter (fun cal -> cal.Summary.Contains("Social") |> not)
                                
            aogr_rooms |> Seq.iter (fun item -> printfn "%s,\t%s" item.Id item.Summary)
            printfn ""
            printfn "export CALENDAR_IDS=%s" (aogr_rooms |> Seq.map (fun i -> i.Id) |> Seq.reduce (sprintf "%s,%s"))
        }
        
        
    let fetchEvents clientId clientSecret calendarId =

        let scopes = [CalendarService.Scope.CalendarReadonly]
        let tempFile = new FileDataStore("google-filedatastore", true)
        async {
            let! credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                                ClientSecrets( ClientId = clientId, ClientSecret = clientSecret),
                                scopes, "user", CancellationToken.None, tempFile) |> Async.AwaitTask
            // Create the service
            let bar = new BaseClientService.Initializer(
                        ApplicationName = "roommate-test",
                        HttpClientInitializer = credential )
            let service = new CalendarService(bar)

             // Define parameters of request.
            let request = service.Events.List(calendarId)
            request.TimeMin <-System.Nullable DateTime.Now
            request.ShowDeleted <- System.Nullable false
            request.SingleEvents <- System.Nullable true
            request.MaxResults <- System.Nullable 10
            request.OrderBy <- System.Nullable EventsResource.ListRequest.OrderByEnum.StartTime

            // Execute the request
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
