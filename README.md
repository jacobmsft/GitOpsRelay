# GitOpsRelay

Universal Git Signal Bus and OpenGitOps SDK for **GitHub**, **Azure DevOps**, and **unprivileged Git repositories**.

## Overview

`GitOpsRelay` decouples Git event ingestion from workload execution across cloud and self-hosted source control:

- **Webhook Ingestion Where Permitted:** Plugs into GitHub Apps and Azure DevOps Service Hooks to capture `push`, `branch`, `tag`, and `pull_request` transitions in real time.
- **Polling Parity Where Apps Can't Be Installed:** Uses zero-API-quota Git wire-protocol ref checks (`ls-remote`) plus conditional ETag/watermark PR polling to synthesize the **exact same CloudEvents 1.0 signals** for unprivileged or external repositories.
- **Pluggable Queue Abstraction Layer:** Emits normalized `GitSignal` messages to **Azure Storage Queues**, **Azure Service Bus**, or local/self-hosted queues so downstream workloads bring their own compute (via `GitOpsRelay.Client`, KEDA scalers, or Azure Functions).
- **Cloud-Native Hosting:** Packaged as a stateless-friendly .NET host ready for **Azure App Service** and **Azure Container Apps** (Web + Scheduled Jobs), backed by Azure Table Storage watermarks and Managed Identity.
- **Optional OpenGitOps Layer:** Ships `GitOpsRelay.OpenGitOps` to combine instant queue wake-ups with periodic drift reconciliation in alignment with [OpenGitOps v1.0.0](https://opengitops.dev/).

## Architecture & Project Plan

See [**PLAN.md**](./PLAN.md) for the full architecture, CloudEvents signal specification, `.NET` solution layout, and phased open-source roadmap.

## License

[MIT](./LICENSE)
