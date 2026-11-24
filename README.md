# GitOpsRelay

GitOpsRelay is a universal Git signal bus for GitHub, Azure DevOps, and
unprivileged Git repositories. It normalizes webhooks and outbound Git polling
into one CloudEvents 1.0 contract so workloads can react without coupling to
SCM-specific payloads or inbound connectivity.

## Overview

- **One signal contract:** GitHub and Azure DevOps webhook events and poller
  observations normalize to the same `GitSignal`.
- **Works without installed apps:** outbound Git ref polling supports upstream,
  partner, and locked-down repositories where webhooks are unavailable.
- **Pluggable queues:** consumers can use Azure Storage Queues, Azure Service
  Bus, or local/self-hosted transports through transport-independent APIs.
- **Bring your own compute:** workloads pull messages from their chosen broker;
  they do not need public inbound endpoints.
- **Azure-ready architecture:** the host is designed for stateless deployment
  to Azure App Service or Azure Container Apps, including continuous polling
  and scheduled one-shot poll jobs.

## Architecture

The platform has two layers. The core signal bus normalizes source events,
tracks per-ref state, and publishes queue messages. An optional OpenGitOps
reconciler SDK can consume those messages and combine fast event wake-ups with
periodic drift correction. Signals pin immutable commit IDs; polling can heal
missed webhook deliveries.

The core contracts are provider-neutral. Future Azure Service Bus integration
can promote CloudEvents metadata for broker-side routing and use session keys
for ordering. Azure Storage Queues provide a lower-cost queue option. Durable
ref snapshots and distributed leases are externalized so App Service and
Container Apps replicas do not rely on local disk or process memory.

See [**PLAN.md**](./PLAN.md) for the complete design and phased roadmap.
Architecture decisions are recorded in [the ADRs](./docs/adr/), and the
canonical envelope is defined in
[`gitsignal.cloudevents.schema.json`](./docs/schema/gitsignal.cloudevents.schema.json).

## OpenGitOps alignment

The signal bus supports declarative reconciliation by carrying immutable commit
SHAs, allowing workloads to pull events automatically, and pairing event-driven
wake-ups with periodic reconciliation. The future `GitOpsRelay.OpenGitOps`
package will build on the core consumer contract and remain optional.

## Phase 0

Phase 0 establishes the CloudEvents 1.0 contract, normalization and polling
primitives, and provider-independent queue and state interfaces. SCM
integrations, concrete cloud providers, and deployable hosting are planned for
later phases.

## Build

Requires the .NET 10 SDK.

```sh
dotnet build
dotnet test --configuration Release
```

## License

[MIT](./LICENSE)
