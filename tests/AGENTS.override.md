# Test Folder Overrides

These overrides apply to work under `tests/`.

## Scope

- Treat `tests/` as the shared unit-test boundary for this repository.
- Prefer test-only changes here unless the task explicitly requires coordinated production-code updates.

## Test Rules

- Follow existing xUnit patterns already used in this repo.
- Prefer focused regression coverage for protocol parsing, request handling, routing, and edge cases.
- Avoid rewriting production behavior to satisfy a weak test; verify intent against the server project first.

## Verification

- Default verification command:
  - `dotnet test UnityMCP.sln`
- Targeted verification for test-project-only work:
  - `dotnet test tests/UnityMcp.Server.Tests/UnityMcp.Server.Tests.csproj`

## Boundaries

- Keep test helpers and assertions aligned with real server behavior.
- Do not introduce external integration setup, Unity consumer-project guidance, or unrelated infrastructure from the `tests/` boundary.
