# Security

This client handles API credentials and document contents. Do not put secrets or real documents in issue
reports, screenshots, sample files or logs. API responses can include one-time secrets after rotation.

The SDK requires HTTPS for remote endpoints, keeps certificate validation enabled, rejects redirects and
uses an allowlisted operation catalogue. The console uses server-side in-memory credentials, anti-forgery
protection, encoded response text and a policy that disallows browser scripts. These controls do not make
an arbitrary public deployment safe without appropriate hosting configuration.

Use [configuration guidance](docs/configuration.md) before hosting the console beyond your local machine.
Do not expose local fixture mode as a shared service. A process restart clears console sessions; there is no
persistent credential store or shared session infrastructure.

## Reporting a vulnerability

Do not open a public issue with exploit details, secrets or sensitive data. Use the hosting repository's private
security-reporting feature if enabled. Otherwise contact a maintainer privately using an existing verified
channel. If no private channel is available, request one without disclosing vulnerability details.
This source distribution does not declare a dedicated security mailbox or a response-time commitment.

Provide the affected version, impact, minimal reproduction with synthetic data and any suggested mitigation.
Coordinate disclosure with maintainers. No long-term security-support window is currently declared; consult
the release notes of the distribution you use.
