# Development and verification

All paths and commands below are relative to this client project's root. Install a .NET 10 SDK compatible
with `global.json`. Windows, Linux and macOS use the same `dotnet` commands; the convenience scripts require
a POSIX shell. No Java runtime, database or live API account is needed to build and test this project.

## Build and test

```sh
dotnet build Docnaut.FranceConnector.slnx -c Release
dotnet run --project Docnaut.FranceConnector.FeatureTests -c Release --no-build
```

The feature suite is an executable project, not a `dotnet test` adapter. A failed assertion exits unsuccessfully.
It starts local HTTP fixtures and the real web application, then verifies authentication, environment selection,
CSRF, operation catalogue coverage, requests, upload/download behavior, error display and sign-out.
It does not establish live API integration acceptance. Keep its fixtures free of real credentials and documents.

`NuGet.Config` clears external feeds because no external packages are required. `Directory.Build.props` enables
nullable reference types, implicit usings and warnings-as-errors for all three projects.

## Develop with automatic restart

```sh
sh run-dev.sh
```

Open [http://localhost:8083](http://localhost:8083). The script uses polling file watching for bind mounts and
rebuilds/restarts after source changes. Browser-refresh injection is disabled; refresh the page manually.
Restarting clears in-memory sessions. Ctrl+C stops a foreground development process.

For a packaged runtime independent of your installed SDK, use the [container guide](container.md).
The development script and Docker image are separate startup options.

## Local browser fixture

After the Release build above:

```sh
dotnet run --project Docnaut.FranceConnector.FeatureTests -c Release --no-build -- --serve-browser
```

Open [http://localhost:8083](http://localhost:8083) and use the local fixture credentials printed by the process.
Both environment choices target fixtures in this mode. No live requests are made. This is an alternative to the
normal console, so stop any existing process using port 8083 first. Stop the fixture process when finished.

## Package the SDK

```sh
dotnet pack Docnaut.FranceConnector.Sdk -c Release --no-build -o artifacts
```

The current package is `artifacts/Docnaut.FranceConnector.Sdk.0.1.0.nupkg`. It includes the SDK assembly,
XML API documentation, package README and MIT license metadata. Packaging does not publish anything.
`sh verify.sh` performs the Release build, HTTP journey and package build in order.

For an external application, see the project-reference and local-package instructions in the
[SDK guide](../Docnaut.FranceConnector.Sdk/README.md). Use that application's package feed configuration;
this project's cleared feed list is not a requirement for SDK consumers.

## Maintaining the contract and releasing

The bundled [OpenAPI contract](../contracts/client-api-v1.yaml) documents the public request/response shapes.
When updating the supported API, reconcile `ApiOperation.All`, request validation, examples and the feature
journey together. The catalogue test checks operation coverage; it is not a complete schema conformance test.

Before a release, run verification, review [CHANGELOG.md](../CHANGELOG.md), set the SDK package version,
check the package contents and preserve license notices. Distribute this directory as the source root; exclude
`bin`, `obj`, `artifacts`, credentials and local environment files. The [container workflow](../.github/workflows/container.yml) validates changes and publishes GHCR images.
NuGet publication is separate and is not performed by that workflow.

[Back to README](../README.md)
