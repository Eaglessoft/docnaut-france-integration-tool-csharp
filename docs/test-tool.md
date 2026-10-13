# Using the test tool

The console is a manual integration workbench. Forms are server-rendered; every OAuth and API request is
made by the backend. Start it using the [quick start](../README.md#quick-start).

## First request

1. Select Sandbox or Production and enter that environment's client ID and secret.
2. After login, confirm the environment shown beside **Sign out**.
3. Choose **Read profile** and select **Send request**.
4. Inspect the HTTP status, selected response headers and response body below the form.

A successful login confirms authentication; it does not prove permission to execute every operation.
An API error is shown with its HTTP status and returned body. Downloads are returned as attachments, with a
validated media type and sanitized filename. Responses are displayed as text, not executable HTML.

## Operation groups

| Group | What you can test |
|---|---|
| Account | Read profile/preferences, replace preferences, rotate the service client secret |
| Documents | Upload/list/read documents, processing, events, files, downloads and Peppol facts |
| Decisions | Submit or read an invoice decision |
| Reports | Submit payment data or an F10 XML transmission, read reports and receipts |
| Webhooks | Manage subscriptions, request verification, rotate secrets and inspect deliveries |

Webhook operations are outgoing API requests. The console has no incoming callback receiver or webhook
signature-verification endpoint.

## Completing a form

- **Path fields:** copy the exact identifier returned by the relevant API response. Document paths use the public
  document trace ID, including fields named `documentId`. The reporting period uses `YYYY-MM`.
- **Query fields:** leave optional filters blank. For paginated operations, pass the returned cursor unchanged
  into `cursor`; do not construct a cursor yourself.
- **JSON request body:** enter a JSON object conforming to the [OpenAPI contract](../contracts/client-api-v1.yaml).
  The console is intentionally a flexible text editor, not a business-specific form designer.
- **Source file:** choose XML/PDF for document upload (up to 100 MiB), or XML for F10 (up to 10 MiB).
  F10 also uses the JSON body as multipart metadata.
- **Idempotency key:** create a unique key for a new action. Retain it with that action if a retry is needed.
  Reuse it only with identical input. Invoice decision keys must be UUIDs.
- **If-Match:** copy the quoted `ETag` header from the preceding read when updating preferences or a subscription.

Body/file fields are cleared after execution. There is no saved request collection. A secret-rotation response
may contain a one-time secret; store it securely and avoid copying it into screenshots or issue reports.
After rotating the service client secret, sign out and log in using the new value.

## Troubleshooting

| Symptom | Check |
|---|---|
| Login fails | Selected environment, credentials, configured token URL and network reachability |
| API returns 401/403 | Current credentials and permission for that operation; login alone does not establish access |
| Validation message before an API response | Required fields, UUIDs, JSON object syntax, MIME type, sizes and required headers |
| Update fails with a concurrency response | Read the resource again and use its current ETag; review changes before retrying |
| Request times out | Inspect the remote outcome before repeating a mutation; the console does not retry it |
| Session expires during development | A rebuild/restart clears sessions; sign in again |
| Port is occupied | Stop your existing console/fixture, or choose another port as described in configuration |

[Back to README](../README.md)
