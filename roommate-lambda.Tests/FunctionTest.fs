namespace RoommateLambda.Tests


open Xunit
open Amazon.Lambda.TestUtilities
open Amazon.Lambda.APIGatewayEvents

open RoommateLambda


module FunctionTest =
    [<Fact(Skip = "env")>]
    let ``Call HTTP GET on Root``() =
        let functions = Functions()
        let context = TestLambdaContext()
        let request = APIGatewayProxyRequest()
        let response = functions.Get request context

        Assert.Equal(200, response.StatusCode)
        Assert.Contains("Hello AWS Serverless (GET)", response.Body)

    [<Fact(Skip = "env")>]
    let ``Call HTTP POST on Root``() =
        let functions = Functions()
        let context = TestLambdaContext()
        let emptyMap = [] |> Map.ofList
        let request = APIGatewayProxyRequest(Headers = emptyMap)
        let response = functions.Post request context

        Assert.Equal(200, response.StatusCode)
        Assert.Equal("Hello AWS Serverless (POST)", response.Body)
