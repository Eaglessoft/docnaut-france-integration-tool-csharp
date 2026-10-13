# Container image

The image runs the web console on port **8080** as the .NET image's non-root application user.
It contains the published application and ASP.NET Core 10 runtime. Build tools, feature fixtures and
credentials are not copied into the runtime image. No database or persistent volume is required.

## Run a published image

The workflow publishes to:

```text
ghcr.io/eaglessoft/docnaut-france-integration-tool-csharp
```

After the first successful publishing workflow and public package visibility are confirmed:

```sh
docker run --rm --publish 127.0.0.1:8093:8080 \
  ghcr.io/eaglessoft/docnaut-france-integration-tool-csharp:latest
```

Open [http://localhost:8093](http://localhost:8093). Sign in with service-client credentials through the
console; credentials are held in application memory for the session, not baked into the image.
Stopping or replacing the container clears sessions. The loopback port mapping keeps this example local.

## Build locally

Run from the standalone source root:

```sh
docker build --tag docnaut-france-integration-tool:local .
docker run --rm --publish 127.0.0.1:8093:8080 docnaut-france-integration-tool:local
```

The multi-stage Dockerfile restores and publishes the console and its SDK project reference, then
copies only the publish output into the runtime stage. `.dockerignore` excludes local build output,
local settings and common credential file types. Do not put credentials in tracked application settings.

## Runtime settings

The normal Sandbox and Production defaults are included. Override endpoints with ordinary ASP.NET Core
environment variables when running the container; there are no credential build arguments or image-build
secrets. For example, with URLs for your own deployment:

```sh
docker run --rm --publish 127.0.0.1:8093:8080 \
  --env DOCNAUT_SANDBOX_API_BASE_URL=https://api.example.com/client/api/v1/ \
  --env DOCNAUT_SANDBOX_TOKEN_URL=https://identity.example.com/oauth/token \
  docnaut-france-integration-tool:local
```

The corresponding Production keys are `DOCNAUT_PRODUCTION_API_BASE_URL` and
`DOCNAUT_PRODUCTION_TOKEN_URL`. URLs must satisfy the SDK's HTTPS requirements. A loopback URL
inside the container refers to that container, not to the Docker host.

The entrypoint explicitly sets `--urls http://0.0.0.0:8080`, overriding the development loopback setting
in `appsettings.json`. Change the host-side published port as needed; `ASPNETCORE_URLS` does not override
this command-line setting. For an HTTPS reverse proxy, configure `AllowedHosts` for the actual public host
and follow the [configuration guide](configuration.md). Do not expose the console publicly with the
local HTTP example unchanged.

## GitHub Actions publishing

`.github/workflows/container.yml` runs only through **Actions → Validate and publish container → Run workflow**.
Commits, pull requests and Git tags do not trigger it. Select the source branch when dispatching.
Its validation job builds the Release solution, runs the executable feature journeys, builds the image
and checks that it starts as non-root and serves the sign-in page.

Publishing requires validation to pass. Each successful manual run publishes the same image with two tags:

| Tag | Meaning |
|---|---|
| `<build-number>` | This workflow's GitHub `run_number`, for example `12` |
| `latest` | The most recently published manual build |

The OCI image version also contains the build number. Java and C# use the same format with independent
repository counters. A rerun keeps its original build number. Use a registry digest for an immutable deployment
reference. Manual runs share one concurrency group so publication does not overlap.

Only the publish job has `packages: write`. Registry authentication uses the repository's automatically
provided `GITHUB_TOKEN`; no personal access token or registry password is configured. Actions are pinned
to release commit SHAs. OCI labels link the image to this source repository and identify the MIT license.

Repository or organization policies must permit Actions and GHCR package publishing. GitHub container
packages can initially be private even for a public repository. After the first publication, an owner
should confirm the package visibility is **Public** if anonymous pulls are intended.

Workflow actions and container behavior follow the official
[Docker build/push action](https://github.com/docker/build-push-action),
[Docker metadata action](https://github.com/docker/metadata-action),
[.NET setup action](https://github.com/actions/setup-dotnet) and
[Microsoft container documentation](https://learn.microsoft.com/en-us/dotnet/core/docker/build-container).
