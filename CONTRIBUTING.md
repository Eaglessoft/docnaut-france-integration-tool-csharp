# Contributing

Contributions should keep this project small and useful as an integration client. Read the
[project structure](docs/architecture.md) and [development guide](docs/development.md) before making changes.

## Discussing and reporting work

Use [GitHub Issues](https://github.com/Eaglessoft/docnaut-france-integration-tool-csharp/issues) for bug reports and proposals. Include the project/package
version, .NET SDK and operating system, expected/actual behavior and the smallest reproducible example.
Sanitize identifiers, documents, response bodies and logs. Security issues follow [SECURITY.md](SECURITY.md).

For a new dependency, major API change or new responsibility, discuss the proposal with maintainers first.
This project favors .NET/ASP.NET Core facilities and a server-rendered UI without a JavaScript build pipeline.

## Submitting changes

1. Keep a change focused on one problem and preserve the SDK/web presentation boundary.
2. Use clear English names and documentation. Keep nullable analysis and warnings-as-errors passing.
3. Extend the existing HTTP feature journey for changed behavior; consolidate related negative cases.
4. Run the relevant verification commands from the development guide. For UI changes, also check the actual
   browser flow, keyboard access and readable error states.
5. Update the affected guide, SDK examples and Unreleased changelog entry.
6. In the pull request, explain the problem, resulting behavior and tests run. State any test that could not run.

Never include credentials, customer documents or generated build output. Preserve MIT and third-party notices.
Keep feedback respectful and specific; explain technical disagreements with reproducible examples.
