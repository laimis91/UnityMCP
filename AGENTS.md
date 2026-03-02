# Project Agent Instructions

> This file supplements the global `~/.codex/AGENTS.md`.
> Follow the global rules unless this file explicitly overrides them.

---

## 1. Project Summary

- **Project name:** `UnityMCP`
- **Short description:** Local MCP bridge between Codex and the Unity Editor, split between a .NET relay server and a Unity Editor package.
- **Main app type(s):**
  - [x] Minimal API
  - [x] Other: Unity Editor package
  - [ ] Blazor WebAssembly
  - [ ] .NET MAUI + Blazor
  - [ ] WPF (MVVM)

The repo is organized around these top-level areas:
- `src/UnityMcp.Server`: relay server, MCP endpoint, JSON-RPC routing, WebSocket transport
- `tests/UnityMcp.Server.Tests`: xUnit regression/unit coverage for the server
- `unity/Packages/com.laimis.unitymcp`: Unity package containing the Editor bridge
- `docs/` and `README.md`: setup and protocol documentation
- `Plans/`: implementation plans for non-trivial work

Instruction file hierarchy for this repo:
- repo-wide shared guidance lives in `AGENTS.md`
- project-local overrides live next to each `*.csproj`
- test-wide overrides live under `tests/` for shared unit-test rules
- the closest matching `AGENTS.override.md` in the folder tree should be treated as the most specific override

---

## 2. Tech Stack & Targets

- **.NET version:** `net10.0`
- **Key libraries / frameworks already in use:**
  - ASP.NET Core (`Microsoft.NET.Sdk.Web`) for the relay server
  - xUnit for test coverage
  - Unity package dependency: `com.unity.nuget.newtonsoft-json`
- **UI library notes (if any):**
  - Not applicable. This repo does not contain a web UI or desktop UI stack.

Additional verified repo facts:
- Nullable reference types are enabled.
- Implicit usings are enabled.
- Unity package: `com.laimis.unitymcp`
- Unity target: `6000.3.9f1`

When changing existing code, match the repo’s current patterns before introducing anything new.

---

## 3. Run & Debug Commands

How to run and verify this repo locally:

- **Backend dev run command:**
  - `dotnet run --project src/UnityMcp.Server`
- **Frontend dev run command (if separate):**
  - Not applicable.
- **Desktop app run (MAUI/WPF):**
  - Not applicable.
- **Docker / docker-compose (if used):**
  - Not applicable. No repo-local Docker configuration was found during inspection.

Verification commands:
- **Main verification command:**
  - `dotnet test UnityMCP.sln`
- **Targeted test project:**
  - `dotnet test tests/UnityMcp.Server.Tests/UnityMcp.Server.Tests.csproj`

Unity-specific note:
- Manual Unity Editor verification applies only when changes touch `unity/Packages/com.laimis.unitymcp`.
- Do not add or describe integration steps for external Unity consumer projects unless explicitly requested.

---

## 4. Testing for This Repo

- **Unit test framework(s):**
  - [x] xUnit
  - [ ] NUnit
  - [ ] Other
- **Main test command:**
  - `dotnet test UnityMCP.sln`
- **Test projects of interest:**
  - `tests/UnityMcp.Server.Tests`
- **Notes:**
  - Prefer unit/regression tests for server-side behavior.
  - Add or update tests when changing JSON-RPC parsing, MCP request handling, routing, or server-side protocol behavior.
  - Use manual Unity verification only when package or Editor behavior changes.
  - Follow the existing test naming and style already present in this repo rather than inventing a new convention for this project.

---

## 5. Data & Infrastructure (Repo-Specific)

- **Primary database:**
  - Not applicable.
- **Connection config keys:**
  - `UnityMcp:Port`
  - `UnityMcp:UnityRequestTimeoutSeconds`
- **Migrations / schema commands (if any):**
  - Not applicable.
- **External services / APIs:**
  - Local MCP HTTP endpoint exposed by the relay server
  - Local WebSocket endpoints for CLI and Unity Editor connections

Never hardcode secrets here. Prefer checked-in config defaults, environment variables, or local development overrides as needed.

---

## 6. Architecture & Conventions (Repo-Specific)

- **Project layout pattern:**
  - `src/UnityMcp.Server`: server entrypoint, transport, protocol, routing, MCP handlers, options
  - `tests/UnityMcp.Server.Tests`: unit and regression tests for server behavior
  - `unity/Packages/com.laimis.unitymcp/Editor`: Unity Editor bridge code
  - `docs/protocol.md` and `README.md`: protocol and setup docs
- **Important patterns in heavy use:**
  - Preserve the existing ASP.NET Core entrypoint and DI composition style from `src/UnityMcp.Server/Program.cs`.
  - Keep public MCP and JSON-RPC behavior synchronized with the docs when changed.
  - Keep server logic inside `src/UnityMcp.Server` and test logic inside `tests/UnityMcp.Server.Tests`.
- **Anything to avoid in this repo:**
  - Do not add external Unity consumer-project wiring or setup steps unless explicitly requested.
  - Do not modify `obj/`, `bin/`, `.DS_Store`, or other build/output artifacts.
  - Do not invent repo infrastructure that is not already present, such as databases, Docker workflows, or CI pipelines.

If existing code conflicts with general preferences, follow this repo’s existing conventions.

### Repo sub-agent playbook (Codex-style roles)

Use sub-agents when a task spans more than one repo area or benefits from independent verification. Keep one orchestrator responsible for user communication and final integration.

#### How this playbook maps to Codex multi-agent roles

The spawnable Codex roles are `explorer`, `worker`, `reviewer`, and `monitor`.
The focus areas below are repo-specific responsibilities, not separate tool-defined agent types.

When using sub-agents:
- `explorer`: inspect relevant paths, symbols, call flows, and protocol/docs impact
- `worker`: implement changes inside one assigned repo area
- `reviewer`: validate correctness, cross-area boundaries, regressions, and doc/test coverage
- `monitor`: run longer verification and summarize results when helpful

Override placement for sub-agents:
- server-project overrides belong in `src/UnityMcp.Server/AGENTS.override.md`
- shared unit-test overrides belong in `tests/AGENTS.override.md`
- test-project overrides belong in `tests/UnityMcp.Server.Tests/AGENTS.override.md`
- do not place repo-wide override rules at repo root for this repository

#### Server Agent

Scope:
- `src/UnityMcp.Server`

Responsibilities:
- HTTP and WebSocket endpoints
- MCP request handling
- JSON-RPC protocol handling
- DI wiring and options

Inputs:
- Requested behavior change, relevant protocol details, existing server patterns, affected tests, affected docs

Outputs:
- Server-side implementation changes constrained to `src/UnityMcp.Server`
- Notes about test and documentation impact

Do / Don’t:
- Do keep changes inside server boundaries unless the task explicitly requires coordinated cross-area work.
- Do flag required doc updates when public behavior changes.
- Don’t edit Unity package code or tests opportunistically.

#### Test Agent

Scope:
- `tests/UnityMcp.Server.Tests`

Responsibilities:
- Add or update regression coverage
- Verify request/response and protocol edge cases
- Recommend focused verification commands

Inputs:
- Intended behavior, affected server code paths, known regressions, expected protocol semantics

Outputs:
- Test changes in `tests/UnityMcp.Server.Tests`
- Verification notes tied to the changed behavior

Do / Don’t:
- Do mirror real server behavior and existing test patterns.
- Do isolate regression intent clearly.
- Don’t change production behavior just to satisfy a weak or incorrect test without evidence.

#### Unity Package Agent

Scope:
- `unity/Packages/com.laimis.unitymcp/Editor`

Responsibilities:
- Unity Editor bridge behavior inside this repo
- Main-thread dispatch, editor lifecycle, connection behavior, and package settings

Inputs:
- Requested Unity-side behavior, package constraints, existing bridge lifecycle behavior, manual verification expectations

Outputs:
- Unity package changes limited to the package folder in this repo
- Notes for manual Unity verification when needed

Do / Don’t:
- Do keep work inside the package folder.
- Do preserve Unity Editor safety and lifecycle correctness.
- Don’t add consumer-project installation steps or external project integrations by default.

#### Docs / Plans Agent

Scope:
- `README.md`
- `docs/`
- `Plans/`

Responsibilities:
- Keep setup docs and protocol docs synchronized with verified behavior
- Create or update plan files for non-trivial work
- Capture documentation fallout from code changes

Inputs:
- Verified behavior from code or tests, changed MCP tool surface, transport/setup changes, planning requirements

Outputs:
- Documentation and plan updates grounded in actual repo behavior

Do / Don’t:
- Do update docs when tool surface, transport behavior, or setup flow changes.
- Do keep plan files explicit about touched paths and verification.
- Don’t invent runtime behavior that is not backed by the code, tests, or direct verification.

#### Sub-agent handoff template

When handing off work to a sub-agent, provide:
- `Context`: what is already known and what changed
- `Goal`: definition of done for that sub-area
- `Allowed paths`: where it may work
- `Out-of-scope paths`: what it must not touch
- `Verification`: tests or manual checks expected
- `Risks / open questions`: unresolved concerns to keep visible

---

## 7. Plan-Review Loop (Repo Details)

Plan files are stored in the `Plans/` folder.

For this repo:
- **Preferred plan file naming:**
  - `Plans/YYYYMMDD-HHMM-<task>.md`
- **Typical verification for plans:**
  - `dotnet test UnityMCP.sln`
  - manual Unity verification only when changes affect `unity/Packages/com.laimis.unitymcp`
- **Known risk areas to call out in plans:**
  - protocol compatibility
  - request/response routing correctness
  - Unity Editor lifecycle and reconnect behavior
  - documentation drift between `README.md` and `docs/protocol.md`

When writing a plan, list touched paths explicitly and state whether the work is server-only, tests-only, Unity-package-only, docs-only, or cross-area.

---

## 8. Git & CI Notes (Repo-Specific)

- **Default branch:**
  - `main`
- **Branch strategy notes:**
  - No repo-local CI or branch policy file was found during inspection.
  - Follow the repo’s current environment and requested workflow rather than inventing a new branching scheme in this repo-local file.
- **CI (GitHub Actions or other):**
  - Not applicable in current repo state. No repo-local CI configuration was found in the inspected locations.

Keep changes small and reviewable. Do not add or redesign CI workflows unless explicitly requested.

---

## 9. Non-Functional Priorities (Repo-Specific)

- **Performance priorities:**
  - Keep the bridge simple and avoid unnecessary complexity in routing or protocol handling.
- **Security concerns / requirements:**
  - Do not add secrets, credentials, or environment-specific sensitive values.
  - Be careful with externally supplied JSON-RPC payload handling and timeout-related behavior.
- **Other notes:**
  - Correctness of MCP and JSON-RPC transport behavior matters more than clever abstractions.
  - Deterministic request routing and timeout handling are important on the server side.
  - Safe Unity Editor interactions matter on the package side.
  - Documentation accuracy for setup and supported methods is part of the deliverable when behavior changes.
