# Project Overrides: `UnityMcp.Server.Tests`

These overrides apply when working inside `tests/UnityMcp.Server.Tests`.

## Scope

- Keep this project focused on unit and regression tests for `UnityMcp.Server`.
- Mirror production behavior from `src/UnityMcp.Server` rather than inventing alternate semantics here.

## Verification

- Preferred command for this project:
  - `dotnet test tests/UnityMcp.Server.Tests/UnityMcp.Server.Tests.csproj`
- Use `dotnet test UnityMCP.sln` when the change crosses project boundaries or when requested.

## Boundaries

- Keep changes local to the test project unless the task explicitly requires coordinated server changes.
- Do not add Unity package integration steps or external consumer-project setup instructions from this folder.
