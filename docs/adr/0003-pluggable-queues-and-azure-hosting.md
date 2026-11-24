# ADR 0003: Pluggable queues and stateless Azure hosting

- **Status:** Accepted
- **Date:** 2026-11-22

## Context

The bus must support Azure Service Bus, Azure Storage Queues, and local
development transports without coupling normalization to a broker. It must
also run as a scaled-out App Service or Container App, where local disk and
process memory cannot be shared state.

## Decision

Define transport-neutral publisher, consumer, lease, per-ref snapshot-store,
distributed-poll-lease, and deduplication contracts in the core. Publish and
consume operations expose capability discovery, per-item batch results,
routing, bounded ordering/session keys, retry, dead-letter, renewal, and
delivery-count semantics. Lease disposal abandons a message that was not
explicitly completed, abandoned, or dead-lettered. Provider implementations
are responsible for mapping the abstractions to broker capabilities, including
Service Bus session keys and lock expiry.

Polling is exposed through a one-cycle interface. A hosted worker can invoke it
continuously, while a future scheduled Container Apps Job can invoke one cycle
and exit. The hosted worker isolates cycle failures with logged,
configurable exponential backoff and jitter so transient SCM/network failures
do not terminate webhook ingestion. Durable implementations must be external
to the host process for multi-replica deployments. Per-ref snapshots and
streamed baseline seeding avoid storing an unbounded repository map in a
single Azure Table entity.

Snapshot stores stream active refs without tombstones so polling can reconstruct
prior state and detect deletions. Baseline seeding is resumable and marks
completion only after the full stream is consumed; interrupted partial seeds
must remain distinguishable from a completed baseline.

## Consequences

Core and consumers need not depend on Azure SDKs. Concrete Azure providers and
hosting configuration can be added independently while preserving the core
contracts. Queue providers must report unsupported capabilities rather than
silently pretending to provide them. For state publication, use an outbox or a
publish-before-CAS sequence; never mark an idempotency reservation committed
or advance a ref snapshot before the corresponding signal is durably accepted.
