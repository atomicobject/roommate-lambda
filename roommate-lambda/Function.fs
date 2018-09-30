namespace RoommateLambda


open Amazon.Lambda.Core
open Amazon.Lambda.APIGatewayEvents

open System.Net


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.Json.JsonSerializer>)>]
()


type Functions() =

    member __.Get (request: APIGatewayProxyRequest) (context: ILambdaContext) =
        sprintf "Request: %s" request.Path
        |> context.Logger.LogLine

        APIGatewayProxyResponse(
            StatusCode = int HttpStatusCode.OK,
            Body = "Hello AWS Serverless (GET)",
            Headers = dict [ ("Content-Type", "text/plain") ]
        )

    member __.Post (request: APIGatewayProxyRequest) (context: ILambdaContext) =
        sprintf "Request: %s" request.Path
        |> context.Logger.LogLine

        APIGatewayProxyResponse(
            StatusCode = int HttpStatusCode.OK,
            Body = "Hello AWS Serverless (POST)",
            Headers = dict [ ("Content-Type", "text/plain") ]
        )
