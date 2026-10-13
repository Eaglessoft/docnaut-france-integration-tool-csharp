# Configuration

The console reads [appsettings.json](../Docnaut.FranceConnector.TestTool/appsettings.json), standard ASP.NET Core
environment-specific configuration, environment variables and command-line arguments. Later providers override
earlier ones. A file named `appsettings.Local.json` is not loaded automatically unless the ASP.NET environment is `Local`.

## Environment defaults

| Setting | Sandbox | Production |
|---|---|---|
| `ApiBaseUrl` | `https://france-staging.docnaut.com/client/api/v1/` | `https://france.docnaut.com/client/api/v1/` |
| `TokenUrl` | `https://france.docnaut.com/sso/realms/docnaut/protocol/openid-connect/token` | Same default token URL |
| `AllowInsecureHttp` | `false` | `false` |

These are client configuration defaults, not an availability guarantee. Login selects one of these two configured
sets; it does not accept an arbitrary URL. The API base URL must end in `/client/api/v1/`.

Override one value using a double underscore for each configuration separator:

```sh
# POSIX shell; example custom Sandbox deployment
export Environments__Sandbox__ApiBaseUrl=https://api.example.com/client/api/v1/
export Environments__Sandbox__TokenUrl=https://identity.example.com/oauth/token
dotnet run --project Docnaut.FranceConnector.TestTool
```

In PowerShell, use `$env:Environments__Sandbox__ApiBaseUrl = 'https://api.example.com/client/api/v1/'`.
Set the matching token URL as well. Example domains are placeholders, not working services.

HTTPS is required for remote API/token endpoints. `AllowInsecureHttp=true` permits HTTP only for explicit
loopback fixtures; it does not disable certificate validation or enable remote HTTP endpoints.

## Listener and hosting

Normal startup listens on `http://127.0.0.1:8093`. Override it with a command-line option:

```sh
dotnet run --project Docnaut.FranceConnector.TestTool -- --urls http://127.0.0.1:8094
```

`run-dev.sh` listens on `0.0.0.0:8083` for container development. This binds all network interfaces; control network exposure in your development environment.
The packaged image listens on port 8080; see [container instructions](container.md) for host port mapping.

`AllowedHosts` defaults to `localhost;127.0.0.1`. A hosted deployment needs the actual host allowlist and HTTPS.
The console does not configure reverse-proxy trust, forwarded headers, durable sessions or multiple instances.
Account for these explicitly before hosting beyond local development; the shipped configuration is a local test tool.

## Credentials and sessions

Enter credentials at login; do not put them in tracked configuration. The standalone SDK takes credentials from
its caller, so your application should read them from its own secret provider. The console retains credentials
only in server process memory, and never repopulates the secret input after submission.

The session lasts for 30 minutes of inactivity. Restarting the console invalidates existing sessions.
Switch environments by signing out and signing in again. There is no credential fallback between environments.

[Back to README](../README.md)
