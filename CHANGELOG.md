# Changelog

All notable changes to UnityMCP are documented in this file.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Batch 7] — 2026-03-08

### Added — Test Runner (4 tools)

> Requires `com.unity.test-framework` package installed in the Unity project.

- `testRunner.listTests` — lists all discovered Edit Mode / Play Mode tests.
  - Params: `mode` (required: `"editMode"` / `"playMode"`)
  - Returns: `{mode, count, tests: [{fullName, name, className, assembly}]}`
- `testRunner.run` — starts a test run **asynchronously** and returns immediately.
  - Params: `mode` (required), `testFilter` (optional)
  - Returns: `{started: true, mode, message}`
  - Design: poll `testRunner.getResults` to retrieve results once the run completes.
- `testRunner.getResults` — retrieves results of the last completed test run.
  - Params: none
  - Returns: `{status, summary: {total, passed, failed, skipped}, tests: [{name, fullName, result, duration, message?, stackTrace?}]}`
- `testRunner.cancel` — cancels an in-progress test run.
  - Params: none
  - Returns: `{cancelled: bool, message}`

---

## [Batch 6] — 2026-03-08

### Added — Audio (11 tools)

- `audio.getSourceSettings` / `audio.setSourceSettings`
- `audio.play` / `audio.stop` / `audio.pause` / `audio.unpause` / `audio.getIsPlaying`
- `audio.getMixerSettings` / `audio.setMixerParameter`
- `audio.getListenerSettings` / `audio.setListenerSettings`
- `audioSource.getSettings` / `audioSource.setSettings` (component wrappers)

---

## [Batch 5] — 2026-03-07

### Added — Physics, Time, Joints, Renderer (28 tools)

- **Physics 3D — Queries:** `physics.raycast`, `physics.overlapSphere`
- **Time:** `time.getSettings`, `time.setSettings`
- **Base Joint:** `joint.getSettings`, `joint.setSettings`
- **3D Joints:** `hingeJoint.*`, `springJoint.*`, `fixedJoint.*`, `characterJoint.*`, `configurableJoint.*`
- **Renderer:** `renderer.getMaterials`, `renderer.setMaterial`

---

## [Batch 4] — 2026-03-06

### Added — Camera Projection, SpriteRenderer, LineRenderer, LODGroup, CanvasGroup, Recompile, Scene Utilities (12 tools)

- `camera.getProjection` / `camera.setProjection`
- `spriteRenderer.getSettings` / `spriteRenderer.setSettings`
- `lineRenderer.getSettings` / `lineRenderer.setSettings`
- `lodGroup.getSettings` / `lodGroup.setSettings`
- `canvasGroup.getSettings` / `canvasGroup.setSettings`
- `editor.recompileScripts`
- `scene.instantiatePrefab`

---

## [Batch 3] — 2026-03-06

### Added — 2D Physics: Rigidbody, Colliders, Joints (36 tools)

- `rigidbody2D.getSettings` / `rigidbody2D.setSettings`
- `collider2D.getSettings` / `collider2D.setSettings`
- `boxCollider2D.*`, `circleCollider2D.*`, `capsuleCollider2D.*`
- `polygonCollider2D.*`, `edgeCollider2D.*`, `compositeCollider2D.*`
- `hingeJoint2D.*`, `springJoint2D.*`, `distanceJoint2D.*`, `fixedJoint2D.*`
- `sliderJoint2D.*`, `wheelJoint2D.*`, `targetJoint2D.*`

---

## [Batch 2] — 2026-03-06

### Added — Editor Utilities, Scene Management, Asset Creation, Components (46 tools)

- **Editor:** `editor.clearConsole`, `editor.pausePlayMode`, `editor.undo`, `editor.redo`, `editor.getTags`, `editor.getLayers`, `editor.addTag`, `editor.removeTag`, `editor.addLayer`, `editor.removeLayer`, `editor.getUndoHistory`
- **Scene tag/layer:** `scene.setTag`, `scene.setLayer`
- **Scene management:** `scene.save`, `scene.openScene`, `scene.newScene`, `scene.closeScene`, `scene.setActiveScene`
- **Scene objects:** `scene.setParent`, `scene.duplicateObject`, `scene.renameObject`, `scene.setActive`, `scene.getSelectionDetails`, `scene.selectByName`
- **Asset creation:** `assets.createFolder`, `assets.createScript`, `assets.createMaterial`, `assets.createPrefab`, `assets.delete`, `assets.move`, `assets.createScriptableObject`
- **Animator:** `animator.getSettings`, `animator.setSettings`, `animator.getParameters`, `animator.setParameter`
- **Mesh/Skin:** `meshRenderer.getSettings`, `meshRenderer.setSettings`, `skinnedMeshRenderer.getSettings`, `skinnedMeshRenderer.setSettings`
- **Audio:** `audioSource.getSettings`, `audioSource.setSettings`
- **Character:** `characterController.getSettings`, `characterController.setSettings`
- **Particles:** `particleSystem.getSettings`, `particleSystem.setSettings`, `particleSystem.play`, `particleSystem.stop`
- **NavMesh:** `navMesh.bake`, `navMeshAgent.getSettings`, `navMeshAgent.setSettings`, `navMeshObstacle.getSettings`, `navMeshObstacle.setSettings`
- **UI:** `canvas.getSettings`, `canvas.setSettings`, `canvasGroup.getSettings`, `canvasGroup.setSettings`, `rectTransform.getSettings`, `rectTransform.setSettings`
- **Terrain:** `terrain.getSettings`, `terrain.setSettings`
- **Build:** `build.getSettings`, `build.setSettings`, `build.build`

---

## [Batch 1] — 2026-03-06

### Added — Core MVP, Editor, Scene, Camera, Light, Physics 3D, Prefab, Assets (46 tools combined with Batch 2 PR)

- MCP protocol: `initialize`, `notifications/initialized`, `ping`, `tools/list`, `tools/call`, `prompts/list`, `resources/list`, `resources/templates/list`, `resources/read`
- **Editor:** `editor.getPlayModeState`, `editor.getConsoleLogs`, `editor.consoleTail`, `editor.enterPlayMode`, `editor.exitPlayMode`, `editor.pausePlayMode`, `editor.recompileScripts`
- **Scene (hierarchy):** `scene.getActiveScene`, `scene.listOpenScenes`, `scene.newScene`, `scene.openScene`, `scene.closeScene`, `scene.save`, `scene.setActiveScene`
- **Scene (objects):** `scene.getSelection`, `scene.getSelectionDetails`, `scene.selectObject`, `scene.selectByPath`, `scene.selectByName`, `scene.setSelection`, `scene.findByPath`, `scene.findByTag`, `scene.createGameObject`, `scene.instantiatePrefab`, `scene.destroyObject`, `scene.duplicateObject`, `scene.renameObject`, `scene.setActive`, `scene.setParent`, `scene.setLayer`, `scene.setTag`, `scene.pingObject`, `scene.frameSelection`, `scene.frameObject`
- **Scene (components):** `scene.getComponents`, `scene.addComponent`, `scene.getComponentProperties`, `scene.setComponentProperties`, `scene.setTransform`
- **Camera:** `camera.getSettings`, `camera.setSettings`
- **Light:** `light.getSettings`, `light.setSettings`
- **Prefab:** `prefab.instantiate`, `prefab.getSource`, `prefab.applyOverrides`, `prefab.revertOverrides`
- **Assets:** `assets.find`, `assets.import`, `assets.ping`, `assets.reveal`, `assets.move`, `assets.delete`, `assets.createFolder`, `assets.createScript`, `assets.createMaterial`, `assets.createPrefab`, `assets.createScriptableObject`
- **Physics 3D — Rigidbody:** `rigidbody.getSettings`, `rigidbody.setSettings`
- **Physics 3D — Colliders:** `collider.getSettings`, `collider.setSettings`, `boxCollider.*`, `sphereCollider.*`, `capsuleCollider.*`, `meshCollider.*`

---

## [Initial] — 2026-02-23

### Added

- Project bootstrap: WebSocket relay server (`src/UnityMcp.Server`), Unity Editor package (`com.laimis.unitymcp`), MCP HTTP endpoint at `/mcp`, JSON-RPC 2.0 request/response forwarding, main-thread Unity command dispatch via `EditorApplication.update`
- xUnit test project (`tests/UnityMcp.Server.Tests`)
- Protocol documentation (`docs/protocol.md`)
