namespace RoommateLambda.Tests


open Xunit
open Amazon.Lambda.TestUtilities
open Amazon.Lambda.APIGatewayEvents
open RoommateLambda


module FunctionTest =
    type OverriddenFunctions () =
        inherit Functions ()
        override __.ReadSecret s =
            "ASDF"

    [<Fact>]
    let ``HTTP GET on Root returns domain verification code``() =
        let functions = OverriddenFunctions ()
        let context = TestLambdaContext()
        let request = APIGatewayProxyRequest()
        let response = functions.Get request context

        Assert.Equal(200, response.StatusCode)
        let expectation = sprintf "<meta name=\"google-site-verification\" content=\"%s\" />" "ASDF"
        Assert.Contains(expectation, response.Body)

    [<Fact(Skip = "env")>]
    let ``Call HTTP POST on Root``() =
        let functions = Functions()
        let context = TestLambdaContext()
        let emptyMap = [] |> Map.ofList
        let request = APIGatewayProxyRequest(Headers = emptyMap)
        let response = functions.Post request context

        Assert.Equal(200, response.StatusCode)
        Assert.Equal("Hello AWS Serverless (POST)", response.Body)
