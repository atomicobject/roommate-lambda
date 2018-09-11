namespace RoommateLambda
open Amazon.Lambda.Core
open System


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.Json.JsonSerializer>)>]
()

type Function() =
    member __.FunctionHandler (input: string) (_: ILambdaContext) =

        let secrets = SecretReader.readSecrets()
        
        let calendarId = "atomicobject.com_3935353434383037353937@resource.calendar.google.com"
        
        let events = CalendarFetcher.fetchEvents secrets.googleClientId secrets.googleClientSecret calendarId |> Async.RunSynchronously
        
        CalendarFetcher.printEvents events
        
        // Default code from template... Delete this once we have an actual response
        match input with
        | null -> String.Empty
        | _ -> input.ToUpper()
        
