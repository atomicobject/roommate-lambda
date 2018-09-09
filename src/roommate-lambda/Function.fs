namespace FSharpBasicFunction
open Amazon.Lambda.Core
open System
open System.Threading
open System.IO

open Google.Apis.Auth.OAuth2;
open Google.Apis.Calendar.v3;
open Google.Apis.Calendar.v3.Data;
open Google.Apis.Services;
open Google.Apis.Util.Store;
open Google.Apis.Services

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.Json.JsonSerializer>)>]
()

type Function() =
    /// <summary>
    /// A simple function that takes a string and does a ToUpper
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    member __.FunctionHandler (input: string) (_: ILambdaContext) =

        let scopes = [CalendarService.Scope.CalendarReadonly]
        let credPath = "token.json"
        let stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read)
        let foo = new FileDataStore(credPath, true)
        let credential = GoogleWebAuthorizationBroker.AuthorizeAsync(GoogleClientSecrets.Load(stream).Secrets, scopes, "user", CancellationToken.None, foo).Result

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

        // Print some results
        if events.Items <> null && events.Items.Count > 0 then
          Console.WriteLine("Got events")
          for i in events.Items do
            let start = i.Start.DateTime.ToString()
            Console.WriteLine("{0} ({1})", i.Summary, start)
        else Console.WriteLine("No upcoming events found.")

        // Default code from template... Delete this once we have an actual response
        match input with
        | null -> String.Empty
        | _ -> input.ToUpper()
        
