# Docnaut France Integration Tool — C#

[![Container](https://github.com/Eaglessoft/docnaut-france-integration-tool-csharp/actions/workflows/container.yml/badge.svg)](https://github.com/Eaglessoft/docnaut-france-integration-tool-csharp/actions/workflows/container.yml)

A .NET SDK and a small web console for building and testing Client API integrations.
Use the SDK in your own backend, or use the console to try requests without writing application code.

The SDK uses .NET libraries only. The console uses ASP.NET Core Razor Pages and bundled CSS.
There are no external NuGet packages, browser JavaScript, Node.js requirements or database requirements.

## Quick start

Install the .NET 10 SDK. Clone the source and enter the project directory:

```sh
git clone https://github.com/Eaglessoft/docnaut-france-integration-tool-csharp.git
cd docnaut-france-integration-tool-csharp
```

Then run:

```sh
dotnet build Docnaut.FranceConnector.slnx
dotnet run --project Docnaut.FranceConnector.TestTool
```

Open [http://localhost:8093](http://localhost:8093), choose **Sandbox** or **Production**, and enter your
service client ID and secret. Select **Read profile**, then **Send request** to make your first read request.
You need credentials and network access for the selected environment. Production operations can change real data.

To explore without an account, use the [local fixture mode](docs/development.md#local-browser-fixture).

## Choose your starting point

| I want to… | Read |
|---|---|
| Start with Docker Compose or Kubernetes | [Deployment guide](docs/deployment.md) |
| Run the ready-to-use container or publish an image | [Container guide](docs/container.md) |
| Call the API from my own C# application | [SDK guide](Docnaut.FranceConnector.Sdk/README.md) |
| Send requests from the web console | [Test tool guide](docs/test-tool.md) |
| Change endpoints or listening ports | [Configuration](docs/configuration.md) |
| Understand the code and request flow | [Project structure](docs/architecture.md) |
| Develop, test or build a package | [Development](docs/development.md) |
| Look up request/response fields | [Bundled OpenAPI contract](contracts/client-api-v1.yaml) |
| Submit a fix or report a problem | [Contributing](CONTRIBUTING.md) |

## What is included

- **SDK:** OAuth client-credentials authentication, cached access tokens, 26 named API operations,
  validated request parameters, JSON responses, upload/download streams and error handling.
- **Test tool:** environment selection, server-side credential storage, forms generated from the SDK operation
  catalogue, and response/status/header display. Every API request runs on the web application's backend.
- **Feature tests:** HTTP journeys with local OAuth/API fixtures. They do not need live credentials.

The operation catalogue covers account settings, documents, decisions, reports and webhook management.
This project is an integration client. It does not host an API service, process invoices or receive webhook callbacks.
The JSON bodies remain visible so you can work directly with the documented wire format.

## Project status

The SDK package version is **0.1.0**. Build a local NuGet package using the development guide; these instructions
make no assumption that a package has been published to a public feed. Check [CHANGELOG.md](CHANGELOG.md) for
changes. No compatibility or support lifetime is promised by the current pre-1.0 version.

## License and security

Source repository: [Eaglessoft/docnaut-france-integration-tool-csharp](https://github.com/Eaglessoft/docnaut-france-integration-tool-csharp).

Source code is available under the [MIT license](LICENSE). Bundled assets are described in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). See [SECURITY.md](SECURITY.md) for credential handling and
security reporting. Use [CONTRIBUTING.md](CONTRIBUTING.md) for ordinary bug reports and contributions.
