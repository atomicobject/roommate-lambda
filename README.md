# Steps take to create project

1. Install the dotnet CLI

    Find installer here: https://www.microsoft.com/net/learn/get-started-with-dotnet-tutorial

2. Install the Amazon templates 

    `dotnet new -i Amazon.Lambda.Templates`

3. Create a new _empty lambda function_ project using the dotnet CLI

    `dotnet new lambda.EmptyFunction -lang F# -o FSharpBasicFunction --region us-west-2 --profile default`
    
4. Follow the steps in the tutorial below to enable the Google API

    https://developers.google.com/calendar/quickstart/dotnet
    



