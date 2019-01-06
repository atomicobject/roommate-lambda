
module roommate.AwsIotClient

    open Amazon.IotData

    // https://docs.aws.amazon.com/sdkfornet/v3/apidocs/Index.html
    let publish endpoint topic (message:string) =

        let client = new AmazonIotDataClient("https://" + endpoint)
        let req = new Model.PublishRequest(
                                Topic = topic,
                                Payload = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(message)),
                                Qos = 1 // QoS 1 means wait for ack or timeout
                                )
        client.PublishAsync(req) |> Async.AwaitTask |> Async.RunSynchronously

