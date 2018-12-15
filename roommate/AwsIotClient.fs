
module roommate.AwsIotClient

    open Amazon.IotData

    // https://docs.aws.amazon.com/sdkfornet/v3/apidocs/Index.html
    let portalUrl = "https://console.aws.amazon.com/iot/home?region=us-east-1#/test"

    let publish endpoint topic (message:string) =

        printfn "You can also pub/sub from the aws portal at:\n%s" portalUrl
        printfn ""
        
        let client = new AmazonIotDataClient("https://" + endpoint)
        let req = new Model.PublishRequest(
                                Topic = topic,
                                Payload = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(message)))
        let result = client.PublishAsync(req) |> Async.AwaitTask |> Async.RunSynchronously
        printfn "%s" (result.HttpStatusCode.ToString())
        ()

