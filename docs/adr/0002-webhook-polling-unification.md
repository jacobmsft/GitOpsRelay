# ADR 0002: Webhook and polling unification

- **Status:** Accepted
- **Date:** 2026-11-22

## Context

Webhooks provide low-latency events when installed, while outbound polling can
serve repositories where installation is unavailable and audit missed
deliveries. Different ingestion paths must not produce semantically different
ref transitions.

## Decision

Normalize webhook and poll observations through the same transition rules and
canonical repository identity. Canonical IDs NFC-normalize and case-fold host
and path, IDN-normalize hosts, remove URL scheme, userinfo, and `.git`, and
recognize equivalent GitHub and Azure DevOps HTTPS/SSH URL variants. Normalize
all-zero Git object IDs to absence before comparing states, ignore peeled
annotated-tag advertisements and non-branch/tag refs, and use stable,
versioned transition keys.

CloudEvents `id` is a unique identifier for each event occurrence; the
deterministic transition key is carried separately as `idempotencykey`.
Providers reserve that key before publishing, then commit it only after
successful publication. A poller must not interpret another worker's
uncommitted reservation as a duplicate and advance its snapshot.

Snapshots are stored per ref with compare-and-swap versions and tombstones.
Pollers can stream the active (non-tombstone) refs to reconstruct the previous
snapshot and detect deletions. Baseline seeding records an explicit completion
marker only after the entire streamed baseline is consumed; interrupted seeding
can be retried idempotently and must never be treated as complete. This avoids
repository-wide ETag contention and giant entities. Webhook/poll
implementations should use an outbox or equivalent durable publish-before-CAS
flow: persist pending signals and ref changes atomically where possible, publish
the pending signals, then mark the outbox and advance the snapshot. When an
atomic transaction is unavailable, publish before committing the CAS and rely
on the stable idempotency key for safe retries. Initial snapshot seeding is
silent by default.

## Consequences

Polling and webhooks can converge on the same before/after state and
idempotency key, while each CloudEvent occurrence retains a unique `id`. A
future host can share per-ref state between both paths and use polling as a
repair loop without making local process state authoritative.
