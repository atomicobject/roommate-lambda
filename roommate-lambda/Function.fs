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


type Functions() =

    member __.Get (request: APIGatewayProxyRequest) (context: ILambdaContext) =
        let verificationCode = secretOrBust "GOOGLE_VERIFICATION_CODE"

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

        let toMap kvps =
            kvps
            |> Seq.map (|KeyValue|)
            |> Map.ofSeq

        // let googHeaders = [
        //     "X-Goog-Resource-State"
        //     "X-Goog-Resource-ID"
        //     "X-Goog-Resource-URI"]

        let maybeHeaders = request.Headers |> Option.ofObj
        let googHeaders = match maybeHeaders with
                            | None ->
                                context.Logger.LogLine "No headers."
                                [] |> Map.ofList
                            | Some h -> h |> toMap |> Map.filter ( fun k _ -> k.Contains "Goog")

        context.Logger.LogLine("Received push notification! Headers:")
        googHeaders |> Map.toList |> List.iter( fun (k,v) -> context.Logger.LogLine(sprintf "%s : %s" k v))

        let calIdsStr = secretOrBust "CALENDAR_IDS"
        let googleTokenJson = secretOrBust "googleTokenJson"
        let googleClientId = secretOrBust "googleClientId"
        let googleClientSecret = secretOrBust "googleClientSecret"

        let calendarIds = calIdsStr.Split(",")
        match googHeaders.TryFind "X-Goog-Resource-URI" with
        |None ->
            context.Logger.LogLine("No X-Google-Resource-ID header found.")
        |Some calURI ->
            // todo: unit test
            let calId = calURI.Split("/") |> List.ofArray |> List.find (fun x -> x.Contains "atomicobject.com")

            if calendarIds |> Array.contains calId then
                context.Logger.LogLine(sprintf "Calendar %s is in my list!" calId)
                let calendarService = accessTokenSignIn googleClientId googleClientSecret googleTokenJson |> Async.RunSynchronously

                let events = fetchEvents calendarService calId |> Async.RunSynchronously
                logEvents events (fun (s:string) -> context.Logger.LogLine(s))
            else
                context.Logger.LogLine(sprintf "Calendar %s is not in my list!" calId)



        // let headers = request.Headers |> Option.ofObj |> Option.map (fun headers ->
        //     |> Seq.map (fun x -> x.Key,x.Value) |> Map.ofSeq

        // ) |> ignore
        (*

            |> List.map( fun h -> h, headers.tryFind h )
            let resourceState = headers.TryFind "X-Goog-Resource-State"
            let resourceId = headers.TryFind "X-Goog-Resource-ID"
            let resourceUri = headers.TryFind "X-Goog-Resource-URI"

            sprintf "state=%s resource=%s" (resourceState.ToString()) (resourceUri.ToString()) |> context.Logger.LogLine
        *)

        APIGatewayProxyResponse(
            StatusCode = int HttpStatusCode.OK,
            Body = "Hello AWS Serverless (POST)",
            Headers = dict [ ("Content-Type", "text/plain") ]
        )
