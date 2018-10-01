namespace RoommateLambda


open Amazon.Lambda.Core
open Amazon.Lambda.APIGatewayEvents

open System.Net

open System

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.Json.JsonSerializer>)>]
()


type Functions() =

    member __.Get (request: APIGatewayProxyRequest) (context: ILambdaContext) =
        let verificationCode = Environment.GetEnvironmentVariable "GOOGLE_VERIFICATION_CODE"

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

        request.Headers |> Option.ofObj |> Option.map (fun headers ->
            let headers = headers |> Seq.map (fun x -> x.Key,x.Value) |> Map.ofSeq
            let resourceState = headers.TryFind "X-Goog-Resource-State"
            let resourceId = headers.TryFind "X-Goog-Resource-ID"
            let resourceUri = headers.TryFind "X-Goog-Resource-URI"
            sprintf "state=%s resource=%s" (resourceState.ToString()) (resourceUri.ToString()) |> context.Logger.LogLine
        ) |> ignore

        APIGatewayProxyResponse(
            StatusCode = int HttpStatusCode.OK,
            Body = "Hello AWS Serverless (POST)",
            Headers = dict [ ("Content-Type", "text/plain") ]
        )
