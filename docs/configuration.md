# Configuration

The console reads [appsettings.json](../Docnaut.FranceConnector.TestTool/appsettings.json), standard ASP.NET Core
environment-specific configuration, environment variables and command-line arguments. Later providers override
earlier ones. A file named `appsettings.Local.json` is not loaded automatically unless the ASP.NET environment is `Local`.

## Environment defaults

| Configuration key / environment variable | Default |
|---|---|
| `DOCNAUT_SANDBOX_API_BASE_URL` | `https://france-staging.docnaut.com/client/api/v1/` |
| `DOCNAUT_PRODUCTION_API_BASE_URL` | `https://france.docnaut.com/client/api/v1/` |
| `DOCNAUT_SANDBOX_TOKEN_URL` | `https://france.docnaut.com/sso/realms/docnaut/protocol/openid-connect/token` |
| `DOCNAUT_PRODUCTION_TOKEN_URL` | Same default token URL |
| `DOCNAUT_SANDBOX_ALLOW_INSECURE_HTTP` | `false` |
| `DOCNAUT_PRODUCTION_ALLOW_INSECURE_HTTP` | `false` |

These are client configuration defaults, not an availability guarantee. Login selects one of these two configured
sets; it does not accept an arbitrary URL. The API base URL must end in `/client/api/v1/`.

Integration settings use flat uppercase `DOCNAUT_` keys with single underscores. Override them directly:

```sh
# POSIX shell; example custom Sandbox deployment
export DOCNAUT_SANDBOX_API_BASE_URL=https://api.example.com/client/api/v1/
export DOCNAUT_SANDBOX_TOKEN_URL=https://identity.example.com/oauth/token
dotnet run --project Docnaut.FranceConnector.TestTool
```

In PowerShell, use `$env:DOCNAUT_SANDBOX_API_BASE_URL = 'https://api.example.com/client/api/v1/'`.
Set the matching token URL as well. Example domains are placeholders, not working services.

HTTPS is required for remote API/token endpoints. The selected environment’s `DOCNAUT_*_ALLOW_INSECURE_HTTP=true` permits HTTP only for explicit
loopback fixtures; it does not disable certificate validation or enable remote HTTP endpoints.

## Listener and hosting

Normal startup listens on `http://127.0.0.1:8093`. Override it with a command-line option:

```sh
dotnet run --project Docnaut.FranceConnector.TestTool -- --urls http://127.0.0.1:8094
```

`run-dev.sh` listens on `0.0.0.0:8083` for container development. This binds all network interfaces; control network exposure in your development environment.
The packaged image listens on port 8080; see [container instructions](container.md) for host port mapping.

The framework host setting `AllowedHosts` (environment variable `ALLOWEDHOSTS`) defaults to `localhost;127.0.0.1`. A hosted deployment needs the actual host allowlist and HTTPS.
The console does not configure a trusted proxy allowlist, durable sessions or multiple instances.
Account for these explicitly before hosting beyond local development; the shipped configuration is a local test tool.

## Context path behind a reverse proxy

Set `DOCNAUT_CONTEXT_PATH=/france-integration-test` to host the console below a URL prefix.
The default is empty (site root); `/` also selects the root. A non-root context starts with `/`
and contains slash-separated letters, digits, hyphens or underscores. Trailing slashes are normalized.
Invalid context values fail startup rather than generating inconsistent URLs.

The reverse proxy must **preserve the prefix** when forwarding requests. The application separates
the configured prefix into ASP.NET Core's `Request.PathBase` before static files and Razor routing.
For example, `/france-integration-test/Login` reaches the login page. Navigation, form targets,
static assets and redirects retain that prefix, and the session cookie is scoped to the context.
Do not strip the prefix or rely on an `X-Forwarded-Prefix` header with this setting.

For TLS termination at a trusted reverse proxy, `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` enables
the framework's forwarded scheme handling so `SameAsRequest` session cookies use `Secure` for HTTPS.
This switch accepts forwarded headers from any connecting proxy; use it only when network access to
the backend is controlled and the proxy replaces client-supplied forwarded headers. It does not
configure the context path. Set `ALLOWEDHOSTS` to the actual public host (for example `tools.docnaut.com`),
and configure HTTPS at the edge. Keep probes on an explicitly allowed host.

## Credentials and sessions

Enter credentials at login; do not put them in tracked configuration. The standalone SDK takes credentials from
its caller, so your application should read them from its own secret provider. The console retains credentials
only in server process memory, and never repopulates the secret input after submission.

The session lasts for 30 minutes of inactivity. Restarting the console invalidates existing sessions.
Switch environments by signing out and signing in again. There is no credential fallback between environments.

[Back to README](../README.md)
