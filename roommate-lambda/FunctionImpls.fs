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


    let sendAnUpdateToCal logFn (calId:LongCalId) =

        let config = readConfig()
        calId
            |> (fetchEventsForCalendar logFn config)
            |> Result.bind (mapEventsToMessage)
            |> Result.bind (determineTopicsToPublishTo logFn config.roommateConfig)
            |> Result.bind (sendMessageToTopics logFn config.mqttEndpoint)

    let sendAnUpdateToBoard (boardId:string) logFn =

        let config = readConfig()

        sprintf "Sending an update for %s" boardId |> logFn

        boardId
            |> lookupCalendarForBoard config.roommateConfig
            |> function
                | None -> Error  "Unknown board"
                | Some calId -> Ok calId
            |> Result.bind (sendAnUpdateToCal logFn)


