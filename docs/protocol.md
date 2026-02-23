# Unity MCP Protocol (MVP)

## Transport
- WebSocket (text frames)
- JSON-RPC 2.0 message envelopes

## Endpoints
- CLI client -> `ws://127.0.0.1:5001/ws/cli`
- Unity Editor plugin -> `ws://127.0.0.1:5001/ws/unity` (default; configurable in Unity via `Tools/Unity MCP/Settings`)

## Server-handled Methods
- `ping`
  - Returns server heartbeat metadata without contacting Unity.

## Unity-handled Methods (MVP)
- `ping`
  - Returns Unity heartbeat metadata (`unityVersion`).
- `editor.getPlayModeState`
  - Returns editor state:
    - `isPlaying`
    - `isPaused`
    - `isCompiling`
    - `isPlayingOrWillChangePlaymode`
- `editor.getConsoleLogs`
  - Returns a bounded snapshot of recent Unity logs captured by UnityMCP (in-memory buffer).
  - Params:
    - `maxResults` (optional, integer, `1..500`, default `100`)
    - `includeStackTrace` (optional, boolean, default `false`)
    - `contains` (optional, case-insensitive substring filter applied to log messages)
    - `levels` (optional, string array filter; allowed values: `info`, `warning`, `error`, `assert`, `exception`; aliases `log`->`info`, `warn`->`warning`)
  - Returns:
    - `bufferCapacity`
    - `totalBuffered`
    - `bufferStartSequence`
    - `latestSequence`
    - `nextAfterSequence`
    - `returnedCount`
    - `truncated`
    - `contains`
    - `items[]` (log entries with `sequence`, `timestampUtc`, `logType`, `level`, `message`, optional `stackTrace`)
- `editor.consoleTail`
  - Returns captured log entries after a given sequence cursor (poll-based tail).
  - Params:
    - `afterSequence` (required, non-negative integer)
    - `maxResults` (optional, integer, `1..500`, default `100`)
    - `includeStackTrace` (optional, boolean, default `false`)
    - `contains` (optional, case-insensitive substring filter applied to log messages)
    - `levels` (optional, string array filter; allowed values: `info`, `warning`, `error`, `assert`, `exception`; aliases `log`->`info`, `warn`->`warning`)
  - Returns:
    - `afterSequence`
    - `nextAfterSequence`
    - `cursorBehindBuffer`
    - `returnedCount`
    - `truncated`
    - `items[]` (log entries)
- `editor.enterPlayMode`
  - Requests transition into play mode.
  - Returns immediate editor state and whether a change was requested (`changed`).
- `editor.exitPlayMode`
  - Requests transition back to edit mode.
  - Returns immediate editor state and whether a change was requested (`changed`).
- `scene.createGameObject`
  - Creates a `GameObject` in the active scene.
  - Params:
    - `name` (optional, string)
    - `position` (optional, number array `[x, y, z]`)
  - Returns:
    - `instanceId`
    - `name`
    - `sceneName`
    - `scenePath`
    - `hierarchyPath`
    - `position`
- `scene.getActiveScene`
  - Returns metadata for the currently active scene.
  - Returns:
    - `isValid`
    - `isLoaded`
    - `isActive`
    - `handle`
    - `buildIndex`
    - `name`
    - `path`
    - `rootCount`
- `scene.listOpenScenes`
  - Returns metadata for all currently open scenes.
  - Returns:
    - `count`
    - `activeSceneHandle`
    - `items[]` (scene summaries)
- `scene.getSelection`
  - Returns metadata for the current Unity Editor selection (`Selection.objects` / active selection).
  - Returns:
    - `count`
    - `activeObject`
    - `activeGameObject`
    - `items[]` (selection object summaries)
- `scene.selectObject`
  - Selects a single Unity object by `instanceId`.
  - Params:
    - `instanceId` (required, integer)
    - `ping` (optional, boolean)
    - `focus` (optional, boolean)
  - Returns:
    - same payload shape as `scene.getSelection`
- `scene.selectByPath`
  - Selects a single Unity scene object by hierarchy path (same format as returned `hierarchyPath`).
  - Params:
    - `path` (required, string; example `Cube/Main Camera`)
    - `scenePath` (optional, string; Unity scene path for disambiguation)
    - `ping` (optional, boolean)
    - `focus` (optional, boolean)
  - Returns:
    - same payload shape as `scene.getSelection`
- `scene.findByPath`
  - Finds Unity scene objects by hierarchy path without changing selection.
  - Params:
    - `path` (required, string; example `Cube/Main Camera`)
    - `scenePath` (optional, string; Unity scene path for scoping/disambiguation)
  - Returns:
    - `path`
    - `scenePath`
    - `count`
    - `items[]` (object summaries)
- `scene.getComponents`
  - Returns component metadata for a target `GameObject` (or a `Component` target's owner `GameObject`).
  - Params:
    - `instanceId` (required, integer)
  - Returns:
    - `target`
    - `componentCount`
    - `missingComponentCount`
    - `items[]` (component summaries)
- `scene.destroyObject`
  - Destroys a scene `GameObject` or `Component` by `instanceId` using Unity Undo.
  - Params:
    - `instanceId` (required, integer)
  - Notes:
    - `Transform` component targets are rejected (destroy the `GameObject` instead).
    - Scene objects/components only (asset/prefab targets are rejected).
  - Returns:
    - `destroyed`
    - `destroyedKind` (`gameObject` or `component`)
    - `destroyedInstanceId`
    - `target` (pre-destroy object summary)
- `scene.getComponentProperties`
  - Reads a constrained set of serialized properties for a `Component` by `instanceId`.
  - Params:
    - `componentInstanceId` (required, integer; must reference a `Component`)
  - Returns:
    - `component`
    - `target`
    - `visiblePropertyCount`
    - `propertyCount` (supported/readable)
    - `unsupportedPropertyCount`
    - `properties` (property-path/value map)
    - `unsupportedProperties[]`
- `scene.setComponentProperties`
  - Writes a constrained set of serialized `Component` properties by property path.
  - Params:
    - `componentInstanceId` (required, integer; must reference a `Component`)
    - `properties` (required, object; property-path/value map)
  - Notes:
    - Rejects `m_Script`, non-editable properties, and unsupported property types.
    - Uses Unity Undo (`Undo.RecordObject`) before applying serialized changes.
  - Returns:
    - `component`
    - `target`
    - `appliedModifiedProperties`
    - `appliedCount`
    - `updated[]`
- `scene.setTransform`
  - Mutates basic transform properties on a `GameObject`/`Component` target.
  - Params:
    - `instanceId` (required, integer)
    - `position` (optional `[x,y,z]`, world-space)
    - `localPosition` (optional `[x,y,z]`)
    - `rotationEuler` (optional `[x,y,z]`, world-space euler)
    - `localRotationEuler` (optional `[x,y,z]`)
    - `localScale` (optional `[x,y,z]`)
  - Notes:
    - At least one transform field is required.
    - `position` and `localPosition` cannot be set together.
    - `rotationEuler` and `localRotationEuler` cannot be set together.
  - Returns:
    - `instanceId`
    - `target`
    - `transform` (world/local transform snapshot)
    - `applied`
- `scene.addComponent`
  - Adds a component to a `GameObject` (or a `Component` target's owner `GameObject`) by type name.
  - Params:
    - `instanceId` (required, integer)
    - `typeName` (required, string; short/full/assembly-qualified)
  - Returns:
    - `target`
    - `addedComponent`
    - `componentCount`
- `scene.setSelection`
  - Replaces the current Unity Editor selection with the specified `instanceId`s.
  - Params:
    - `instanceIds` (required, integer array; duplicates ignored)
    - `ping` (optional, boolean)
    - `focus` (optional, boolean)
  - Returns:
    - same payload shape as `scene.getSelection`
- `scene.pingObject`
  - Pings/highlights a Unity object in the Editor without changing selection.
  - Params:
    - `instanceId` (required, integer)
  - Returns:
    - `pinged`
    - `instanceId`
    - `target` (object summary)
- `scene.frameSelection`
  - Best-effort frames the current selection in the Scene view.
  - Returns:
    - `framed`
    - `selectionCount`
    - `hasSceneSelection`
    - `sceneViewAvailable`
    - `activeObject`
- `scene.frameObject`
  - Best-effort frames a specific Unity scene object in the Scene view by `instanceId`.
  - Params:
    - `instanceId` (required, integer)
  - Returns:
    - `framed`
    - `selectionPreserved`
    - `sceneViewAvailable`
    - `hasSceneTarget`
    - `instanceId`
    - `target` (object summary)
- `scene.findByTag`
  - Finds active loaded `GameObject`s matching a tag.
  - Params:
    - `tag` (required, string)
  - Returns:
    - `tag`
    - `count`
    - `items[]` (object summaries)
- `assets.import`
  - Imports/reimports an existing Unity project asset or folder.
  - Params:
    - `assetPath` (required, string, must be project-relative under `Assets/`)
  - Returns:
    - `assetPath`
    - `guid`
    - `isFolder`
    - `exists`
    - `mainAssetType`
    - `mainAssetName`
    - `imported`
- `assets.ping`
  - Pings/highlights an existing asset in the Project window.
  - Params:
    - `assetPath` (required, string, must be project-relative under `Assets/`)
  - Returns:
    - `pinged`
    - `assetPath`
    - `guid`
    - `isFolder`
    - `target`
- `assets.reveal`
  - Focuses the Project window, selects the asset, and pings it.
  - Params:
    - `assetPath` (required, string, must be project-relative under `Assets/`)
  - Returns:
    - `revealed`
    - `focusedProjectWindow`
    - `assetPath`
    - `guid`
    - `isFolder`
    - `target`
- `assets.find`
  - Searches Unity assets using `AssetDatabase.FindAssets(query)`.
  - Params:
    - `query` (required, string)
    - `maxResults` (optional, integer, `1..500`, default `100`)
    - `searchInFolders` (optional, string array, Unity folders under `Assets/`)
    - `types` (optional, string array, appended as `t:<type>` filters)
    - `labels` (optional, string array, appended as `l:<label>` filters)
  - Returns:
    - `query`
    - `effectiveQuery`
    - `searchInFolders`
    - `types`
    - `labels`
    - `totalMatched`
    - `returnedCount`
    - `maxResults`
    - `truncated`
    - `items[]` (asset summaries)
      - each item additionally includes:
        - `assetImporterType`
        - `labels`
        - `fileExtension`

## Request Example
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "editor.getPlayModeState"
}
```

## Success Response Example
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "isPlaying": false,
    "isPaused": false,
    "isCompiling": false
  }
}
```

## `scene.createGameObject` Example
Request:
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "scene.createGameObject",
  "params": {
    "name": "EnemySpawnPoint",
    "position": [0, 1.5, 0]
  }
}
```

`scene.frameSelection` success response (example):
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "instanceId": 12345,
    "name": "EnemySpawnPoint",
    "sceneName": "SampleScene",
    "scenePath": "Assets/Scenes/SampleScene.unity",
    "hierarchyPath": "EnemySpawnPoint",
    "position": [0, 1.5, 0]
  }
}
```

## `editor.enterPlayMode` Example
Request:
```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "method": "editor.enterPlayMode"
}
```

`scene.selectObject` / `scene.setSelection` success response (example):
```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "result": {
    "isPlaying": false,
    "isPaused": false,
    "isCompiling": false,
    "isPlayingOrWillChangePlaymode": true,
    "requestedState": "playing",
    "changed": true
  }
}
```

## `editor.getConsoleLogs` Example
Request:
```json
{
  "jsonrpc": "2.0",
  "id": 11,
  "method": "editor.getConsoleLogs",
  "params": {
    "maxResults": 50,
    "includeStackTrace": false,
    "contains": "Reference",
    "levels": ["warning", "error"]
  }
}
```

Success response (example):
```json
{
  "jsonrpc": "2.0",
  "id": 11,
  "result": {
    "bufferCapacity": 2000,
    "totalBuffered": 12,
    "bufferStartSequence": 31,
    "latestSequence": 42,
    "afterSequence": null,
    "nextAfterSequence": 42,
    "cursorBehindBuffer": false,
    "returnedCount": 12,
    "truncated": false,
    "includeStackTrace": false,
    "contains": "Reference",
    "levels": ["warning", "error"],
    "items": [
      {
        "sequence": 42,
        "timestampUtc": "2026-02-23T21:55:00.0000000+00:00",
        "logType": "Warning",
        "level": "warning",
        "message": "Sample warning from Unity",
        "stackTrace": null
      }
    ]
  }
}
```

## `editor.consoleTail` Example
Request:
```json
{
  "jsonrpc": "2.0",
  "id": 12,
  "method": "editor.consoleTail",
  "params": {
    "afterSequence": 42,
    "maxResults": 20
  }
}
```

Success response (example):
```json
{
  "jsonrpc": "2.0",
  "id": 12,
  "result": {
    "bufferCapacity": 2000,
    "totalBuffered": 14,
    "bufferStartSequence": 31,
    "latestSequence": 44,
    "afterSequence": 42,
    "nextAfterSequence": 44,
    "cursorBehindBuffer": false,
    "returnedCount": 2,
    "truncated": false,
    "includeStackTrace": false,
    "items": [
      {
        "sequence": 43,
        "timestampUtc": "2026-02-23T21:55:05.0000000+00:00",
        "logType": "Log",
        "level": "info",
        "message": "Recompiling scripts...",
        "stackTrace": null
      },
      {
        "sequence": 44,
        "timestampUtc": "2026-02-23T21:55:06.0000000+00:00",
        "logType": "Error",
        "level": "error",
        "message": "MissingReferenceException...",
        "stackTrace": null
      }
    ]
  }
}
```

## `editor.exitPlayMode` Example
Request:
```json
{
  "jsonrpc": "2.0",
  "id": 5,
  "method": "editor.exitPlayMode"
}
```

## `scene.findByTag` Example
Request:
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "scene.findByTag",
  "params": {
    "tag": "Enemy"
  }
}
```

## `scene.getActiveScene` Example
Request:
```json
{
  "jsonrpc": "2.0",
  "id": 7,
  "method": "scene.getActiveScene"
}
```

Success response (example):
```json
{
  "jsonrpc": "2.0",
  "id": 7,
  "result": {
    "isValid": true,
    "isLoaded": true,
    "isActive": true,
    "handle": 123,
    "buildIndex": 0,
    "name": "TestScene",
    "path": "Assets/Scenes/TestScene.unity",
    "rootCount": 4
  }
}
```

## `scene.listOpenScenes` Example
Request:
```json
{
  "jsonrpc": "2.0",
  "id": 8,
  "method": "scene.listOpenScenes"
}
```

Success response (example):
```json
{
  "jsonrpc": "2.0",
  "id": 8,
  "result": {
    "count": 2,
    "activeSceneHandle": 123,
    "items": [
      {
        "isValid": true,
        "isLoaded": true,
        "isActive": true,
        "handle": 123,
        "buildIndex": 0,
        "name": "TestScene",
        "path": "Assets/Scenes/TestScene.unity",
        "rootCount": 10
      },
      {
        "isValid": true,
        "isLoaded": true,
        "isActive": false,
        "handle": 124,
        "buildIndex": 1,
        "name": "UI",
        "path": "Assets/Scenes/UI.unity",
        "rootCount": 3
      }
    ]
  }
}
```

## `scene.getSelection` Example
Request:
```json
{
  "jsonrpc": "2.0",
  "id": 9,
  "method": "scene.getSelection"
}
```

## `scene.selectObject` Example
Optional params:
- `ping` (`boolean`): highlights the selected object in the Unity Editor.
- `focus` (`boolean`): best-effort frames the selection in the Scene view.

Request:
```json
{
  "jsonrpc": "2.0",
  "id": 13,
  "method": "scene.selectObject",
  "params": {
    "instanceId": 45458,
    "ping": true,
    "focus": true
  }
}
```

## `scene.selectByPath` Example
Optional params:
- `scenePath` (`string`): disambiguates duplicate hierarchy paths across open scenes.
- `ping` (`boolean`): highlights the selected object in the Unity Editor.
- `focus` (`boolean`): best-effort frames the selection in the Scene view.

Request:
```json
{
  "jsonrpc": "2.0",
  "id": 13,
  "method": "scene.selectByPath",
  "params": {
    "path": "Cube/Main Camera",
    "scenePath": "Assets/_Game/Scenes/TestScene.unity",
    "ping": true,
    "focus": true
  }
}
```

## `scene.findByPath` Example
Request:
```json
{
  "jsonrpc": "2.0",
  "id": 18,
  "method": "scene.findByPath",
  "params": {
    "path": "Cube/Main Camera",
    "scenePath": "Assets/_Game/Scenes/TestScene.unity"
  }
}
```

## `scene.getComponents` Example
Request:
```json
{
  "jsonrpc": "2.0",
  "id": 21,
  "method": "scene.getComponents",
  "params": {
    "instanceId": 45444
  }
}
```

## `scene.destroyObject` Example
Request:
```json
{
  "jsonrpc": "2.0",
  "id": 22,
  "method": "scene.destroyObject",
  "params": {
    "instanceId": 45458
  }
}
```

## `scene.getComponentProperties` Example
Request:
```json
{
  "jsonrpc": "2.0",
  "id": 23,
  "method": "scene.getComponentProperties",
  "params": {
    "componentInstanceId": 45448
  }
}
```

## `scene.setComponentProperties` Example
Request:
```json
{
  "jsonrpc": "2.0",
  "id": 24,
  "method": "scene.setComponentProperties",
  "params": {
    "componentInstanceId": 45448,
    "properties": {
      "m_Enabled": true
    }
  }
}
```

## `scene.setTransform` Example
Request:
```json
{
  "jsonrpc": "2.0",
  "id": 25,
  "method": "scene.setTransform",
  "params": {
    "instanceId": 45444,
    "position": [7.24, 10.02, -7.99],
    "localScale": [1, 1, 1]
  }
}
```

## `scene.addComponent` Example
Request:
```json
{
  "jsonrpc": "2.0",
  "id": 26,
  "method": "scene.addComponent",
  "params": {
    "instanceId": 45458,
    "typeName": "BoxCollider"
  }
}
```

## `scene.setSelection` Example
Optional params:
- `ping` (`boolean`): highlights the active selected object in the Unity Editor.
- `focus` (`boolean`): best-effort frames the selection in the Scene view.

Request:
```json
{
  "jsonrpc": "2.0",
  "id": 14,
  "method": "scene.setSelection",
  "params": {
    "instanceIds": [45458, 45459],
    "ping": true,
    "focus": true
  }
}
```

## `scene.pingObject` Example
Request:
```json
{
  "jsonrpc": "2.0",
  "id": 15,
  "method": "scene.pingObject",
  "params": {
    "instanceId": 45458
  }
}
```

## `scene.frameSelection` Example
Request:
```json
{
  "jsonrpc": "2.0",
  "id": 16,
  "method": "scene.frameSelection"
}
```

`scene.frameSelection` success response (example):
```json
{
  "jsonrpc": "2.0",
  "id": 16,
  "result": {
    "framed": true,
    "selectionCount": 1,
    "hasSceneSelection": true,
    "sceneViewAvailable": true,
    "activeObject": {
      "instanceId": 45458,
      "name": "Cube",
      "unityType": "UnityEngine.GameObject"
    }
  }
}
```

## `scene.frameObject` Example
Request:
```json
{
  "jsonrpc": "2.0",
  "id": 17,
  "method": "scene.frameObject",
  "params": {
    "instanceId": 45458
  }
}
```

`scene.frameObject` success response (example):
```json
{
  "jsonrpc": "2.0",
  "id": 17,
  "result": {
    "framed": true,
    "selectionPreserved": true,
    "sceneViewAvailable": true,
    "hasSceneTarget": true,
    "instanceId": 45458,
    "target": {
      "instanceId": 45458,
      "name": "Cube",
      "unityType": "UnityEngine.GameObject"
    }
  }
}
```

`scene.selectObject` / `scene.selectByPath` / `scene.setSelection` success response (example):
```json
{
  "jsonrpc": "2.0",
  "id": 9,
  "result": {
    "count": 2,
    "activeObject": {
      "instanceId": 34567,
      "name": "Main Camera",
      "unityType": "UnityEngine.GameObject",
      "isPersistent": false,
      "assetPath": null,
      "sceneName": "TestScene",
      "scenePath": "Assets/Scenes/TestScene.unity",
      "hierarchyPath": "Cube/Main Camera",
      "activeSelf": true,
      "activeInHierarchy": true,
      "componentType": null
    },
    "activeGameObject": {
      "instanceId": 34567,
      "name": "Main Camera",
      "unityType": "UnityEngine.GameObject",
      "isPersistent": false,
      "assetPath": null,
      "sceneName": "TestScene",
      "scenePath": "Assets/Scenes/TestScene.unity",
      "hierarchyPath": "Cube/Main Camera",
      "activeSelf": true,
      "activeInHierarchy": true,
      "componentType": null
    },
    "items": [
      {
        "instanceId": 34567,
        "name": "Main Camera",
        "unityType": "UnityEngine.GameObject",
        "isPersistent": false,
        "assetPath": null,
        "sceneName": "TestScene",
        "scenePath": "Assets/Scenes/TestScene.unity",
        "hierarchyPath": "Cube/Main Camera",
        "activeSelf": true,
        "activeInHierarchy": true,
        "componentType": null
      },
      {
        "instanceId": 45678,
        "name": "PlayerController",
        "unityType": "UnityEngine.MonoScript",
        "isPersistent": true,
        "assetPath": "Assets/Scripts/PlayerController.cs",
        "sceneName": null,
        "scenePath": null,
        "hierarchyPath": null,
        "activeSelf": null,
        "activeInHierarchy": null,
        "componentType": null
      }
    ]
  }
}
```

## `assets.import` Example
Request:
```json
{
  "jsonrpc": "2.0",
  "id": 6,
  "method": "assets.import",
  "params": {
    "assetPath": "Assets/Textures/Test.png"
  }
}
```

## `assets.ping` Example
Request:
```json
{
  "jsonrpc": "2.0",
  "id": 19,
  "method": "assets.ping",
  "params": {
    "assetPath": "Assets/_Game/Scenes/TestScene.unity"
  }
}
```

## `assets.reveal` Example
Request:
```json
{
  "jsonrpc": "2.0",
  "id": 20,
  "method": "assets.reveal",
  "params": {
    "assetPath": "Assets/_Game/Scenes/TestScene.unity"
  }
}
```

## `assets.find` Example
Request:
```json
{
  "jsonrpc": "2.0",
  "id": 10,
  "method": "assets.find",
  "params": {
    "query": "Player",
    "maxResults": 25,
    "searchInFolders": ["Assets/Prefabs"],
    "types": ["Prefab"],
    "labels": ["Gameplay"]
  }
}
```

Success response (example):
```json
{
  "jsonrpc": "2.0",
  "id": 10,
  "result": {
    "query": "Player",
    "effectiveQuery": "Player t:Prefab l:Gameplay",
    "searchInFolders": ["Assets/Prefabs"],
    "types": ["Prefab"],
    "labels": ["Gameplay"],
    "totalMatched": 3,
    "returnedCount": 3,
    "maxResults": 25,
    "truncated": false,
    "items": [
      {
        "guid": "0123456789abcdef0123456789abcdef",
        "assetPath": "Assets/Prefabs/Player.prefab",
        "isFolder": false,
        "mainAssetType": "UnityEngine.GameObject",
        "mainAssetName": "Player",
        "assetImporterType": "UnityEditor.ModelImporter",
        "labels": ["Gameplay"],
        "fileExtension": ".prefab"
      }
    ]
  }
}
```

Success response (example):
```json
{
  "jsonrpc": "2.0",
  "id": 6,
  "result": {
    "assetPath": "Assets/Textures/Test.png",
    "guid": "0123456789abcdef0123456789abcdef",
    "isFolder": false,
    "exists": true,
    "mainAssetType": "UnityEngine.Texture2D",
    "mainAssetName": "Test",
    "imported": true
  }
}
```

## Error Response Example
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "error": {
    "code": -32001,
    "message": "Unity is not connected."
  }
}
```

## Current Constraints
- Requests require a string or numeric `id` (notifications are not handled yet).
- Single Unity Editor connection is supported.
- Request forwarding is serialized and correlated by JSON-RPC `id`.
- No authentication yet (local-only MVP).
- `scene.findByTag` currently returns active objects only (`GameObject.FindGameObjectsWithTag`).
- Play mode control responses are immediate and do not wait for transition completion.
- `assets.import` currently supports only existing project-relative paths under `Assets/`.
- Console log history is captured in-memory by the Unity package (bounded buffer) and resets on domain reload/editor restart.

## Resource URI Query Parameters (MVP)
- `resources/read` supports optional query params for console resources:
  - `unitymcp://editor/console-logs?maxResults=20&includeStackTrace=true`
  - `unitymcp://editor/console-tail/125?maxResults=10&includeStackTrace=false`
  - repeated `level` values are supported and normalized:
    - `unitymcp://editor/console-logs?level=warning&level=error`
    - `unitymcp://editor/console-tail/125?level=error`
  - `contains` is supported for case-insensitive message substring filtering:
    - `unitymcp://editor/console-logs?contains=MissingReference`
    - `unitymcp://editor/console-tail/125?contains=NullReference`
- `unitymcp://assets/find/{query}` resource template supports optional query params:
  - `maxResults` (single integer)
  - repeated `folder` (maps to `searchInFolders`)
  - repeated `type` (maps to `types`)
  - repeated `label` (maps to `labels`)
- Additional selection resources/templates:
  - `unitymcp://scene/selection/active`
  - `unitymcp://scene/selection/index/{index}` (`index` is 0-based)
- Invalid `resources/read` resource parameters now return JSON-RPC `InvalidParams` with `error.data` including:
  - `source = "resources/read"`
  - `resourceUri`
  - `parameter` (when applicable)
