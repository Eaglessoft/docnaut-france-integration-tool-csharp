# Run with Compose or Kubernetes

These definitions run the web console from the published container image. They do not deploy the remote
Client API, provision client accounts or contain login credentials. Use your service-client credentials
in the console. Confirm that the image has been published and is pullable before starting.

## Docker Compose

From this project's root:

```sh
docker compose config --quiet
docker compose up --detach
```

Open [http://localhost:8093](http://localhost:8093). Only host loopback is exposed. The console uses the
normal Sandbox and Production endpoints. Optional Compose variables are:

| Variable | Overrides |
|---|---|
| `DOCNAUT_CLIENT_IMAGE` | Container image; defaults to `ghcr.io/eaglessoft/docnaut-france-integration-tool-csharp:latest` |
| `DOCNAUT_SANDBOX_API_BASE_URL` | Sandbox Client API base URL |
| `DOCNAUT_SANDBOX_TOKEN_URL` | Sandbox OAuth URL |
| `DOCNAUT_PRODUCTION_API_BASE_URL` | Production Client API base URL |
| `DOCNAUT_PRODUCTION_TOKEN_URL` | Production OAuth URL |

Supply URLs with the formats described in [configuration](configuration.md). These variables configure
addresses, not credentials. For a local image build, for example:

```sh
docker build --tag docnaut-france-integration-tool:local .
DOCNAUT_CLIENT_IMAGE=docnaut-france-integration-tool:local docker compose up --detach
```

Stop and remove this Compose application with `docker compose down`. No volumes are declared.

## Kubernetes

The manifests under `infra/kube/` contain one Deployment and one ClusterIP Service. They use the current
kubectl namespace; they do not create a namespace, ingress, certificate, persistent volume or external service.
Render them without contacting a cluster:

```sh
kubectl kustomize infra/kube
```

After reviewing the rendered resources and selecting the intended context/namespace:

```sh
kubectl apply -k infra/kube
kubectl rollout status deployment/docnaut-france-integration-tool
kubectl port-forward service/docnaut-france-integration-tool 8093:80
```

Open [http://localhost:8093](http://localhost:8093) while port forwarding runs. The Service has no public
ingress. The shipped `AllowedHosts` accepts localhost and 127.0.0.1; probes explicitly send `Host: localhost`.
The probes check the sign-in page only. They do not authenticate or call the remote API and do not prove
that the remote environment is available.

The Deployment runs one instance as UID/GID 1654, without a service-account token, privilege escalation
or Linux capabilities. Its memory request is 128 MiB and limit is 512 MiB; adjust these to measured usage.
The container filesystem provides temporary upload space. No durable storage is configured.

The `Recreate` strategy stops the previous instance before replacement. Updates introduce downtime and
clear in-memory sessions. The console has no shared session store; increasing replicas is not supported
by these sample manifests.

Edit the image and endpoint environment values in `infra/kube/deployment.yaml` before applying when needed.
The example uses `latest`; choose a published digest for repeatable deployments. Pulling the image requires
GHCR access; these manifests do not create registry credentials. For a private image, provision the necessary
cluster pull access through your operator.

To remove exactly these resources from the same context/namespace:

```sh
kubectl delete -k infra/kube
```

For public hosting, configure HTTPS, host filtering and proxy trust for your deployment as described in
[configuration](configuration.md). These local-access examples do not provide that hosting setup.
