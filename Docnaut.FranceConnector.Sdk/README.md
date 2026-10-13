# Docnaut France Connector SDK

An MIT-licensed .NET 10 library for calling the France Connector Client API from C# applications.
It handles OAuth client credentials, request construction, file transfers and response disposal.
It depends only on .NET libraries: no additional NuGet packages or ASP.NET runtime are required.

The SDK exposes 26 named operations for accounts, documents, invoice decisions, payments, reports,
Peppol facts and webhook subscriptions/deliveries. It preserves JSON responses so applications can
read the fields they need. Request body schemas and business outcomes come from the connected
service's API documentation; the SDK does not define replacement business models.

## Install from source or a local package

Install the .NET 10 SDK. From the standalone project's root, add a project reference to your application:

```sh
dotnet add /path/to/MyApp.csproj reference ./Docnaut.FranceConnector.Sdk/Docnaut.FranceConnector.Sdk.csproj
```

Alternatively, build and consume a local NuGet package:

```sh
dotnet pack ./Docnaut.FranceConnector.Sdk/Docnaut.FranceConnector.Sdk.csproj -c Release -o ./artifacts/packages
dotnet add /path/to/MyApp.csproj package Docnaut.FranceConnector.Sdk --version 0.1.0 --source ./artifacts/packages
```

Use the version of the package you built. These commands use the source checkout and a local package
feed; they do not assume that a package has been published to NuGet.org. The package contains this
README and generated XML API documentation.

## Connect and send a request

Supply a Client API base URL ending in `/client/api/v1/`, an OAuth token URL, and your service-client
credentials. The example reads them from environment variables; use your application's secret provider
when appropriate.

```csharp
using Docnaut.FranceConnector.Sdk;

static string Required(string name) => Environment.GetEnvironmentVariable(name)
    ?? throw new InvalidOperationException($"Set {name} before running this example.");

using var client = new ConnectorClient(
    new ConnectorEndpoint(Required("DOCNAUT_API_BASE_URL"), Required("DOCNAUT_TOKEN_URL")),
    Required("DOCNAUT_CLIENT_ID"),
    Required("DOCNAUT_CLIENT_SECRET"));

using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
CancellationToken cancellationToken = cancellation.Token;

// Optional: ExecuteAsync authenticates automatically when needed.
await client.AuthenticateAsync(cancellationToken);

await using var profile = await client.ExecuteAsync(
    new ApiRequest("getClientProfile"), cancellationToken);
if (!profile.IsSuccess)
{
    // Handle profile.StatusCode and the API problem details in profile.Body.
    // Do not log response bodies: some operations return one-time secrets.
}
```

The SDK caches the access token and renews it shortly before its advertised expiry. Reuse one client
per service-client identity and dispose it when the session or application ends. Authentication does
not return the token to your application.

Both URLs must use HTTPS without embedded credentials, query parameters or fragments. For explicit
loopback development only, `new ConnectorEndpoint(apiUrl, tokenUrl, AllowInsecureHttp: true)` permits
HTTP to a loopback host. TLS certificate validation remains enabled. Redirects and cookies are disabled.

The remaining examples reuse `client` and `cancellationToken` from above. Supply IDs and request data
from your own workflow.

## Public API

| Type | Purpose |
|---|---|
| `ConnectorEndpoint` | Holds `ApiBaseUrl`, `TokenUrl` and the optional loopback HTTP flag. |
| `ConnectorClient` | Owns HTTP connections and cached authentication; provides `AuthenticateAsync`, `ExecuteAsync` and `Dispose`. |
| `ApiOperation` | Read-only catalogue. `All` lists operations; `Get(id)` rejects unknown IDs. |
| `BodyKind` | Describes the operation's transfer shape: `None`, `Json`, `Xml`, `Multipart` or `Download`. The current catalogue has no XML-only operation. |
| `ApiRequest` | Selects an operation and supplies path/query values, JSON, upload stream and required headers. |
| `ApiResponse` | Provides `StatusCode`, `IsSuccess`, `Body`, `Headers` and optional streamed `Content`; supports `using` and `await using`. |
| `ConnectorException` | SDK validation, authentication, size-limit or transport failure with a safe message and nullable `StatusCode`, `Code` and `CorrelationId`. |

An `ApiOperation` exposes `Id`, `Label`, `Group`, `Method`, `PathTemplate`, `BodyKind`,
`PathParameters`, `QueryParameters`, `RequiresIdempotencyKey` and `RequiresIfMatch`.
Paths are relative to the configured API base. Operations cannot be supplied with arbitrary methods
or routes.

| Group | Operation IDs |
|---|---|
| Account | `getClientProfile`, `getClientConfiguration`, `replaceClientConfiguration`, `rotateOwnServiceClientSecret` |
| Documents | `createDocumentIntake`, `listDocuments`, `getDocument`, `getDocumentProcessing`, `listDocumentEvents`, `listDocumentArtifacts`, `createArtifactAccess`, `listPeppolFacts` |
| Decisions | `decideInboundInvoice`, `getInboundInvoiceDecision` |
| Reports | `registerInvoicePayment`, `submitReportingTransmission`, `getReport`, `getReportReceipts` |
| Webhooks | `listClientWebhookSubscriptions`, `createClientWebhookSubscription`, `getClientWebhookSubscription`, `updateClientWebhookSubscription`, `verifyClientWebhookSubscription`, `rotateClientWebhookSecret`, `listClientWebhookDeliveries`, `getClientWebhookDelivery` |

## Request values and pagination

`Path` and `Query` are dictionaries of strings. Their keys must match the selected operation's
catalogue entry. Supply every path parameter, and omit unused query parameters rather than passing
empty values. IDs use UUID format; `period` uses a valid `YYYY-MM` month. `limit` accepts 1–100 and
`logPage` accepts 0–10000.

For both `documentId` and `documentTraceId` path names, use the public document trace ID returned by
submission or document reads.

```csharp
await using var page = await client.ExecuteAsync(new ApiRequest("listDocuments")
{
    Query = new Dictionary<string, string> { ["limit"] = "50" }
}, cancellationToken);

if (page.IsSuccess && page.Body is not null)
{
    using var json = System.Text.Json.JsonDocument.Parse(page.Body);
    string? nextCursor = json.RootElement.TryGetProperty("nextCursor", out var cursor)
        && cursor.ValueKind == System.Text.Json.JsonValueKind.String ? cursor.GetString() : null;
    // If nextCursor is present, pass it unchanged as Query["cursor"] on the next request.
}
```

Responses are raw JSON strings, not SDK-specific envelopes. `Body` is null for empty textual responses
and successful downloads. `Headers` is a case-insensitive map containing available correlation, ETag,
Location, Retry-After, cache and content metadata.

## JSON bodies and version headers

Use `Body` for JSON operations. The SDK checks that it is a JSON object, then sends it unchanged.
For preference replacement and webhook updates, copy the quoted ETag from the previous read into
`IfMatch`:

```csharp
await using var current = await client.ExecuteAsync(
    new ApiRequest("getClientConfiguration"), cancellationToken);
if (current.IsSuccess && current.Headers.TryGetValue("ETag", out string? etag))
{
    string replacementJson = await File.ReadAllTextAsync("preferences.json", cancellationToken);
    await using var updated = await client.ExecuteAsync(new ApiRequest("replaceClientConfiguration")
    {
        IfMatch = etag,
        Body = replacementJson
    }, cancellationToken);
    // Inspect updated.IsSuccess and updated.Body; the API validates the preference fields.
}
```

The file must contain the complete preference request object documented by your service. The same
`Body` property carries invoice decision/payment and webhook request objects.

Operations whose `RequiresIdempotencyKey` is true require a caller-supplied `IdempotencyKey`.
A UUID string is suitable, and invoice decisions specifically require one. Preserve the key and
identical request data only when deliberately repeating the same action. Omit `IfMatch` and
`IdempotencyKey` on operations that do not declare them.

## Upload documents and reporting XML

Document uploads use a `source` multipart part:

```csharp
await using var source = File.OpenRead("invoice.xml");
await using var intake = await client.ExecuteAsync(new ApiRequest("createDocumentIntake")
{
    Upload = source,
    FileName = "invoice.xml",
    ContentType = "application/xml",
    IdempotencyKey = Guid.NewGuid().ToString("D")
}, cancellationToken);
```

Documents accept `application/xml`, `text/xml` or `application/pdf`. Filenames must not contain
directory components. Omitted filenames default to `document.xml`; an omitted content type defaults
to `application/xml`, so specify both explicitly for PDF files.

`submitReportingTransmission` sends an XML `source` part plus a JSON `metadata` part supplied through
`Body`:

```csharp
await using var xml = File.OpenRead("report.xml");
await using var transmission = await client.ExecuteAsync(new ApiRequest("submitReportingTransmission")
{
    Path = new Dictionary<string, string> { ["period"] = "2026-10" },
    Upload = xml,
    FileName = "report.xml",
    ContentType = "application/xml",
    Body = """{"category":"SALES","kind":"INITIAL"}""",
    IdempotencyKey = Guid.NewGuid().ToString("D")
}, cancellationToken);
```

Use the period and metadata appropriate to your XML. Reporting uploads require `application/xml`
and default to `report.xml` when no filename is supplied.

Uploads are read from their current position. The caller owns and disposes the input stream;
the SDK neither closes nor rewinds it. Before deliberately repeating an upload, reopen the file or
restore its position yourself.

## Stream a download

```csharp
await using var download = await client.ExecuteAsync(new ApiRequest("createArtifactAccess")
{
    Path = new Dictionary<string, string>
    {
        ["documentTraceId"] = documentTraceId,
        ["artifactId"] = artifactId
    }
}, cancellationToken);

if (download.IsSuccess && download.Content is not null)
{
    await using var destination = File.Create("download.xml");
    await download.Content.CopyToAsync(destination, cancellationToken);
}
```

Successful downloads return `Content` without buffering the file. A failed download returns its
bounded textual error body instead. Dispose the response after copying; it owns the response stream
and connection. Use the returned content headers if your application needs the media type or filename.
Stream reads happen after `ExecuteAsync` returns: pass a cancellation token to the copy/read operation
and handle its normal I/O exceptions there.

## Errors, cancellation and retries

- A completed API request, including HTTP 4xx or 5xx, returns `ApiResponse`. Inspect `IsSuccess`,
  `StatusCode`, `Body` and `Headers`; HTTP API errors are not automatically thrown.
- Invalid SDK input, failed OAuth authentication, transfer failures and size limits raise
  `ConnectorException`. Its optional fields can be null. For a normal API error, read the correlation
  ID from response headers and error details from the response body instead.
- Caller cancellation raises `OperationCanceledException`. An SDK timeout during authentication
  or request execution raises `ConnectorException` with `authentication_timeout` or `transport_timeout`.
- Null required arguments and calls after disposal use standard .NET argument/disposal exceptions.

The SDK never automatically retries a request, including after HTTP 401 or a timeout. An interrupted
write can have an unknown remote outcome; consult the API's query operations and idempotency contract
before deciding to repeat it. Authentication refresh prepares later calls; it does not replay a failed call.

## Limits and lifetime

| Item | SDK limit |
|---|---|
| Document source | 100 MiB |
| Reporting XML source | 10 MiB |
| JSON request body | 1 MiB |
| Textual API response | 2 MiB |
| OAuth response | 64 KiB |
| Connection establishment | 15 seconds |
| OAuth HTTP exchange and body read | 30 seconds |
| API HTTP exchange and textual body read | 120 seconds |

Upload limits apply to source bytes, excluding multipart overhead. Streams with known remaining size
can be rejected before sending; other streams are checked during transfer. Download bytes are streamed
without a total size limit. The API execution timeout does not impose a deadline on download reads
after the response has been returned. A shorter caller cancellation deadline takes precedence.

The SDK does not log credentials, tokens or bodies. Request and response `ToString()` methods omit
their contents. Response bodies can contain one-time secrets; handle them accordingly. Rotating a
service-client secret does not replace credentials in an existing SDK instance: retain the new secret
securely, dispose the old client and create another with the replacement credential.

## License

MIT. The locally built NuGet package declares the MIT license expression and includes the SDK's XML
documentation and this guide.
