namespace RoommateLambda
open Amazon.Lambda.Core
open System


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.Json.JsonSerializer>)>]
()

type Function() =
    member __.FunctionHandler (input: string) (lambdaContext: ILambdaContext) =

        lambdaContext.Logger.Log "Hello from Lambda"
        
        // Default code from template... Delete this once we have an actual response
        match input with
        | null -> String.Empty
        | _ -> input.ToUpper()
        
