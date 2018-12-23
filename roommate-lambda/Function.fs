namespace RoommateLambda

open System.Net

open Amazon.Lambda.Core
open Amazon.Lambda.APIGatewayEvents

open Roommate
open Roommate.SecretReader
open Roommate.CalendarWatcher

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.Json.JsonSerializer>)>]
()



type Functions() =

    let toMap kvps =
        kvps
        |> Seq.map (|KeyValue|)
        |> Map.ofSeq

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
            roommateConfig = readSecretFromEnv "roommateConfig" |> RoommateConfig.deserializeConfig
            serviceAccountEmail = readSecretFromEnv "serviceAccountEmail"
            serviceAccountPrivKey = readSecretFromEnv "serviceAccountPrivKey"
            serviceAccountAppName = readSecretFromEnv "serviceAccountAppName"
            mqttEndpoint = readSecretFromEnv "mqttEndpoint"
        }

        let logFn = context.Logger.LogLine

        request.Headers |> Option.ofObj |> Option.map toMap
            |> function
                | None -> Error  "No headers."
                | Some h -> Ok h
            |> Result.bind (calendarIdFromPushNotification logFn config)
            |> Result.bind (processCalendarId logFn config)
            |> Result.bind (mapEventsToMessage)
            |> Result.bind (determineTopicsToPublishTo logFn config)
            |> Result.bind (sendMessageToTopics logFn config.mqttEndpoint)
            |> function
                | Error e -> logFn e
                | _ -> ()


        APIGatewayProxyResponse(
            StatusCode = int HttpStatusCode.OK,
            Body = "Hello AWS Serverless (POST)",
            Headers = dict [ ("Content-Type", "text/plain") ]
        )

    member __.UpdateRequest (request: Messages.UpdateRequest) (context: ILambdaContext) =
        sprintf "Updated requested for boardId %s" (request.boardId) |> context.Logger.LogLine

        let config : LambdaConfiguration = {
            roommateConfig = readSecretFromEnv "roommateConfig" |> RoommateConfig.deserializeConfig
            serviceAccountEmail = readSecretFromEnv "serviceAccountEmail"
            serviceAccountPrivKey = readSecretFromEnv "serviceAccountPrivKey"
            serviceAccountAppName = readSecretFromEnv "serviceAccountAppName"
            mqttEndpoint = readSecretFromEnv "mqttEndpoint"
        }

        let logFn = context.Logger.LogLine

        request.boardId |> RoommateConfig.lookupCalendarForBoard config.roommateConfig
                        |> function
                            | None -> Error  "Unknown board"
                            | Some calId -> Ok calId
                        |> Result.bind (processCalendarId logFn config)
                        |> Result.bind (mapEventsToMessage)
                        |> Result.bind (determineTopicsToPublishTo logFn config)
                        |> Result.bind (sendMessageToTopics logFn config.mqttEndpoint)
                        |> function
                            | Error e -> logFn e
                            | _ -> ()
        ()
