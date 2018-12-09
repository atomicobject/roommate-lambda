# Roommate - Backend

_Roommate_ is a project to create a conference room gadget that displays availability, takes impromptu reservations, etc.

This repo contains the cloud backend (F#, AWS Lambda) and a command-line tool.

| Embedded Device                                                                                                           | Cloud Backend                                                                                                                           |
| ------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| [atomicobject/roommate](https://github.com/atomicobject/roommate)                                                         | [atomicobject/roommate-lambda](https://github.com/atomicobject/roommate-lambda)                                                         |
| [![CircleCI](https://circleci.com/gh/atomicobject/roommate.svg?style=svg)](https://circleci.com/gh/atomicobject/roommate) | [![CircleCI](https://circleci.com/gh/atomicobject/roommate-lambda.svg?style=svg)](https://circleci.com/gh/atomicobject/roommate-lambda) |

## Prerequisites

- [.NET Core SDK](https://www.microsoft.com/net/download)

## Build and Run

- `dotnet build` to build the solution
- `dotnet test` to run tests.

## What's What

- **roommate** - business logic
- **roommate.test** - tests for it
- **roommate-tool** - CLI tool for testing and miscellaneous functionality
- **roommate-lambda** - AWS Lambda functions
- **roommate-lambda.Tests** - tests targeting Lambda functions (currently unused)

## Google API Authentication

This project integrates with [Google Calendar](https://developers.google.com/calendar/overview) to view room availability, schedule meetings, etc. To do this locally, you'll need to enable the API on your Google account and grab a pair of credentials. I neglected to document the detailed process for creating them, but it starts here: https://console.cloud.google.com/apis

Put the "Client ID" and "Client Secret" in environment variables so **Roommate-tool** can find them. I keep an unversioned script that looks like this:

```
export googleClientId=asdfasdfasdf
export googleClientSecret=jkljkljkl
```

(then you can e.g. `source env.sh`)

Note that this auth flow is just for developer use; the deployed system uses a [service account](https://developers.google.com/identity/protocols/OAuth2#serviceaccount) instead, with a different kind of credentials.
