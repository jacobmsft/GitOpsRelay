# Contributing

Contributions to GitOpsRelay are welcome. For substantial changes, open an issue
or discussion first so the intended scope and compatibility impact are clear.

## Development

- Install the .NET 10 SDK.
- Build with `dotnet build`.
- Run tests with `dotnet test --configuration Release`.
- Keep consumer-facing wire contracts in `GitOpsRelay.Contracts`; place
  implementation and hosting abstractions in `GitOpsRelay.Core`.
- Add focused tests for behavior changes and update the schema or ADRs when
  changing the signal contract or architecture.

Use conventional, descriptive commit messages. Do not commit credentials,
private keys, or production configuration.
