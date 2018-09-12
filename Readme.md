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
= `dotnet test` from the `roommate-lambda.Tests` directory to run tests.

