# ADR 0001: Layered signal bus and OpenGitOps

- **Status:** Accepted
- **Date:** 2026-11-22

## Context

GitOpsRelay needs to normalize source-control changes independently of the
workloads that consume them. Reconciliation is useful but should not be required
to use the event bus.

## Decision

Separate the core signal bus from the optional OpenGitOps reconciliation SDK.
The core publishes canonical CloudEvents 1.0 `GitSignal` messages and exposes
portable consumer-facing contracts. A future reconciler will consume those
signals and combine event wake-ups with periodic drift checks.

## Consequences

The wire contract and core queue abstractions remain usable without importing a
reconciler or a specific SCM provider. Reconciliation-specific APIs belong in a
separate package and are out of scope for Phase 0.
