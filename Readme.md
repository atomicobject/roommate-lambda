# Roommate - Backend
AO Conference Room IoT Project

## CI
[![CircleCI](https://circleci.com/gh/atomicobject/roommate-lambda.svg?style=svg)](https://circleci.com/gh/atomicobject/roommate-lambda)

## Prerequisites

- [dotnet core SDK](https://www.microsoft.com/net/download)

## Secrets
API keys are needed to talk to Google to read calendar events. We're currently using your user account; in the future we should use a service account not owned by any one person.

The secrets we need are called the `clientId` and `clientSecret`.

I neglected to document the detailed process for creating them, but it starts here:
https://console.cloud.google.com/apis

For now we're keeping secrets in environment variables. I keep an unversioned env.sh that looks like this:

```
export googleClientId=asdfasdfasdf
export googleClientSecret=jkljkljkl
```

(then you can `source env.sh`)

## Build and Run
- `dotnet build` to build the solution
- `dotnet test` from the `roommate-lambda.Tests` directory to run tests.

## Steps taken to create initial project

1. Install the dotnet CLI

    Find installer here: https://www.microsoft.com/net/learn/get-started-with-dotnet-tutorial

2. Install the Amazon templates 

    `dotnet new -i Amazon.Lambda.Templates`

3. Create a new _empty lambda function_ project using the dotnet CLI

    `dotnet new lambda.EmptyFunction -lang F# -o FSharpBasicFunction --region us-west-2 --profile default`
    
4. Follow the steps in the tutorial below to enable the Google API

    https://developers.google.com/calendar/quickstart/dotnet

