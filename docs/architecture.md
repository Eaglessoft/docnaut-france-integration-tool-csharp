# Project structure

This project has three small projects with separate responsibilities:

```text
Docnaut.FranceConnector.Sdk/           Reusable .NET library
Docnaut.FranceConnector.TestTool/
  Pages/                             Razor views and form handlers
  Services/ConnectorConsole.cs       Session and request orchestration
  wwwroot/                           Local styles and branding
Docnaut.FranceConnector.FeatureTests/ Executable HTTP feature journey
contracts/client-api-v1.yaml          Public wire-format reference
```

The SDK has no dependency on the web console. You can reference or package it without deploying the console.
The console references the SDK; it does not duplicate HTTP authentication and request construction.
The feature-test executable starts controlled HTTP fixtures and the real console as a child process.

## A request through the console

1. A browser submits an HTML form to a Razor Page handler. ASP.NET Core validates the anti-forgery token.
2. `ConnectorConsole` resolves the server-side session and the selected SDK operation.
3. `ConnectorClient` validates inputs, obtains or reuses an access token, and sends the HTTP request.
4. `ApiResponse` carries status, selected headers and text, or a stream for a successful download.
5. The Page handler renders the response as encoded text, or returns a file download.

There is no browser-to-API AJAX call. The operation catalogue (`ApiOperation.All`) defines the supported
operation IDs, path/query fields, transfer types and required headers. It is used by both request validation
and the console forms. Arbitrary routes and HTTP methods cannot be supplied through a form.

## Authentication and state

On login, the backend authenticates the selected environment's credentials. It stores the SDK client in
process memory and stores an opaque reference in the server-side session. The browser gets a session cookie,
not a bearer token or client secret. The SDK caches its token and refreshes it before expiry when needed.

Each session serializes console requests. Sign-out removes the connection; inactivity expires it after
30 minutes. The console uses in-memory caches and ephemeral data-protection keys: restarting the process
ends sessions. There is no persistent request history, credential database or multi-instance session store.

## Error boundaries

The SDK returns API HTTP failures as `ApiResponse`, allowing callers to examine status and body.
Local validation, authentication and transport failures use `ConnectorException`; caller cancellation uses
`OperationCanceledException`. The console translates failures into visible messages and has a generic
exception page for unexpected failures. See the [SDK guide](../Docnaut.FranceConnector.Sdk/README.md).

The client does not automatically retry mutations or choose a business route. Its purpose is to send the
caller's explicitly selected request and expose the outcome.

[Back to README](../README.md)
