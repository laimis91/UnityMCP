# Project Overrides: `UnityMcp.Server`

These overrides apply when working inside `src/UnityMcp.Server`.

## Scope

- Keep changes in this folder limited to the server project unless the task explicitly requires coordinated cross-project work.
- Treat this project as the owner of:
  - HTTP endpoint behavior
  - WebSocket transport behavior
  - MCP request handling
  - JSON-RPC protocol handling
  - DI wiring and server options

## Verification

- Default verification for server-only changes:
  - `dotnet test UnityMCP.sln`
- If the change is tightly scoped and you need a smaller command:
  - `dotnet test tests/UnityMcp.Server.Tests/UnityMcp.Server.Tests.csproj`

## Documentation Sync

- If server behavior changes affect setup flow, endpoints, MCP methods, or supported tools, update:
  - `README.md`
  - `docs/protocol.md`

## Boundaries

- Do not modify Unity package code from this project boundary unless explicitly required.
- Do not add external Unity consumer-project integration instructions from this project boundary.
