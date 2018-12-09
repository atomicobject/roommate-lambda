namespace RoommateLambda


open System.Net

open Amazon.Lambda.Core
open Amazon.Lambda.APIGatewayEvents

open Roommate
open Roommate.CalendarWatcher

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.Json.JsonSerializer>)>]
()



type Functions() =

    let toMap kvps =
        kvps
        |> Seq.map (|KeyValue|)
        |> Map.ofSeq

    abstract member ReadSecret : string -> string
    default u.ReadSecret (s:string) = SecretReader.readSecretFromEnv s

    member __.Get (request: APIGatewayProxyRequest) (context: ILambdaContext) =
        let verificationCode = __.ReadSecret "GOOGLE_VERIFICATION_CODE"

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
            roommateConfig = __.ReadSecret "roommateConfig" |> RoommateConfig.deserializeConfig
            serviceAccountEmail = __.ReadSecret "serviceAccountEmail"
            serviceAccountPrivKey = __.ReadSecret "serviceAccountPrivKey"
            serviceAccountAppName = __.ReadSecret "serviceAccountAppName"
        }

        let logFn = context.Logger.LogLine

        request.Headers |> Option.ofObj |> Option.map toMap
            |> function
                | None -> Error  "No headers."
                | Some h -> Ok h
            |> Result.bind (processPushNotification logFn config)
            |> function
                | Error e -> logFn e
                | _ -> ()


        APIGatewayProxyResponse(
            StatusCode = int HttpStatusCode.OK,
            Body = "Hello AWS Serverless (POST)",
            Headers = dict [ ("Content-Type", "text/plain") ]
        )
