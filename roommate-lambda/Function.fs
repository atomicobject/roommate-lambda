namespace RoommateLambda


open Amazon.Lambda.Core
open Amazon.Lambda.APIGatewayEvents

open System.Net

open System
open Roommate.SecretReader
open Roommate.CalendarFetcher

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.Json.JsonSerializer>)>]
()


type LambdaConfiguration = {
    calIds : string
    serviceAccountEmail:string
    serviceAccountPrivKey:string
    serviceAccountAppName:string
}


type Functions() =

    let getGoogHeaders (headers:Collections.Generic.IDictionary<string,string>) =
        let toMap kvps =
            kvps
            |> Seq.map (|KeyValue|)
            |> Map.ofSeq

        let maybeHeaders = headers |> Option.ofObj |> Option.map toMap
        match maybeHeaders with
        | None ->
            // context.Logger.LogLine "No headers."
            // [] |> Map.ofList
            Error  "No headers."
        | Some h -> h |> Map.filter ( fun k _ -> k.Contains "Goog") |> Ok

    member __.Get (request: APIGatewayProxyRequest) (context: ILambdaContext) =
        let verificationCode = readSecretFromEnv "GOOGLE_VERIFICATION_CODE"

        sprintf "Request: %s" request.Path
        |> context.Logger.LogLine

        let htmlBody = sprintf 
                        """
                        <html>
                          <head>
                            <meta name="google-site-verification" content="%s" />
                          </head>
                          <body>Hello AWS Serverless (GET)</body>
                        </html>
                        """
                        verificationCode

        APIGatewayProxyResponse(
            StatusCode = int HttpStatusCode.OK,
            Body = htmlBody,
            Headers = dict [ ("Content-Type", "text/html") ]
        )


    member __.Post (request: APIGatewayProxyRequest) (context: ILambdaContext) =
        sprintf "Request: %s" request.Path |> context.Logger.LogLine

        let config : LambdaConfiguration = {
            calIds = readSecretFromEnv "CALENDAR_IDS"
            serviceAccountEmail = readSecretFromEnv "serviceAccountEmail"
            serviceAccountPrivKey = readSecretFromEnv "serviceAccountPrivKey"
            serviceAccountAppName = readSecretFromEnv "serviceAccountAppName"
        }

        let calendarIds = config.calIds.Split(",")

        let logFn = context.Logger.LogLine

        request.Headers 
            |> getGoogHeaders
            |> Result.map (fun gh ->
                logFn "Received push notification! Google headers:"
                gh |> Map.toList |> List.map (fun (k,v) -> sprintf "%s : %s" k v) |> List.iter logFn
                gh)
            |> Result.bind (fun gh -> 
                                match gh.TryFind "X-Goog-Resource-URI" with
                                | None -> Error "No X-Google-Resource-ID header found."
                                | Some resourceId -> Ok resourceId)
            |> Result.bind (fun calURI ->
                // todo: unit test
                let calId = calURI.Split("/") |> List.ofArray |> List.find (fun x -> x.Contains "atomicobject.com")
                if calendarIds |> Array.contains calId then
                    Ok calId
                else
                    calId |> sprintf "Calendar %s is not in my list!" |> Error )
            |> Result.map (fun calId ->
                    sprintf "Calendar %s is in my list!" calId |> logFn
                    let calendarService = serviceAccountSignIn config.serviceAccountEmail config.serviceAccountPrivKey config.serviceAccountAppName |> Async.RunSynchronously

                    let events = fetchEvents calendarService calId |> Async.RunSynchronously
                    logEvents events (fun (s:string) -> context.Logger.LogLine(s)))
            |> function
                | Error e -> logFn e
                | _ -> ()


        APIGatewayProxyResponse(
            StatusCode = int HttpStatusCode.OK,
            Body = "Hello AWS Serverless (POST)",
            Headers = dict [ ("Content-Type", "text/plain") ]
        )
