namespace RoommateLambda

open System
open System.Net

open Amazon.Lambda.Core
open Amazon.Lambda.APIGatewayEvents

open Roommate
open Roommate
open Roommate.SecretReader

open Roommate.RoommateLogic
module FunctionImpls =
    open Roommate.RoommateConfig

    let readConfig () : LambdaConfiguration =
        {
            roommateConfig = readSecretFromEnv "roommateConfig" |> RoommateConfig.deserializeConfig
            serviceAccountEmail = readSecretFromEnv "serviceAccountEmail"
            serviceAccountPrivKey = readSecretFromEnv "serviceAccountPrivKey"
            serviceAccountAppName = readSecretFromEnv "serviceAccountAppName"
            mqttEndpoint = readSecretFromEnv "mqttEndpoint"
            webhookUrl = readSecretFromEnv "webhookUrl"
        }


    let mapEventsAndSendMessage calId config logFn (events:GoogleEventMapper.RoommateEvent list) =
        let msg = events |> mapEventsToMessage
        let topics = determineTopicsToPublishTo config.roommateConfig calId
        sendMessageToTopics logFn config.mqttEndpoint topics msg

    let sendAnUpdateToCal logFn (calId:LongCalId) =
        let config = readConfig()
        let events = fetchEventsForCalendar logFn config calId
                    |> Result.bind(fun e -> e.Items |> List.ofSeq |> List.map GoogleEventMapper.mapEvent |> Ok)
        events |> Result.bind (mapEventsAndSendMessage calId config logFn)

    let sendAnUpdateToBoard (boardId:string) logFn =

        let config = readConfig()

        sprintf "Sending an update for %s" boardId |> logFn

        boardId
            |> lookupCalendarForBoard config.roommateConfig
            |> function
                | None -> Error  "Unknown board"
                | Some calId -> Ok calId
            |> Result.bind (sendAnUpdateToCal logFn)

    let calendarIdFromPushNotification logFn (config:LambdaConfiguration) (pushNotificationHeaders:Map<string,string>) =

        // https://developers.google.com/calendar/v3/push
        pushNotificationHeaders
        |> (fun h -> h |> Map.filter ( fun k _ -> k.Contains "Goog") |> Ok)
        |> Result.map (fun gh ->
            logFn "Received push notification! Google headers:"
            gh |> Map.toList |> List.map (fun (k,v) -> sprintf "%s : %s" k v) |> List.iter logFn
            gh)
        |> Result.bind (fun gh ->
                            match gh.TryFind "X-Goog-Resource-URI" with
                            | None -> Error "No X-Google-Resource-ID header found."
                            | Some resourceId -> Ok resourceId)
        |> Result.map calIdFromURI

