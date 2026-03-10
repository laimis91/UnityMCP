#nullable enable

using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityMcp.Editor.UnityMcpParameterHelpers;
using static UnityMcp.Editor.UnityMcpResolvers;
using static UnityMcp.Editor.UnityMcpSnapshotHelpers;

namespace UnityMcp.Editor
{
    internal static class TerrainHandler
    {
        internal static string BuildGetTerrainSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "terrain.getSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var (terrain, _) = ResolveComponentFromInstanceId<Terrain>(instanceId, "terrain.getSettings");
            var td = terrain.terrainData;

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                target = UnityMcpClient.CreateObjectSummary(terrain),
                heightmapResolution = td != null ? td.heightmapResolution : 0,
                size = td != null ? CreateVector3Array(td.size) : null,
                basemapDistance = terrain.basemapDistance,
                drawHeightmap = terrain.drawHeightmap,
                drawInstanced = terrain.drawInstanced,
                detailObjectDistance = terrain.detailObjectDistance,
                treeBillboardDistance = terrain.treeBillboardDistance,
                shadowCastingMode = terrain.shadowCastingMode.ToString()
            });
        }

        internal static string BuildSetTerrainSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "terrain.setSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var (terrain, ownerGo) = ResolveComponentFromInstanceId<Terrain>(instanceId, "terrain.setSettings");
            var td = terrain.terrainData;

            Undo.RecordObject(terrain, "Set Terrain Settings");
            if (td != null)
                Undo.RecordObject(td, "Set Terrain Settings");

            if (td != null && paramsObject.TryGetValue("heightmapResolution", out var hmrToken) && hmrToken.Type == JTokenType.Integer)
                td.heightmapResolution = hmrToken.Value<int>();

            if (td != null && paramsObject.TryGetValue("size", out var sizeToken) && sizeToken is JArray sizeArr && sizeArr.Count == 3)
                td.size = new Vector3(sizeArr[0].Value<float>(), sizeArr[1].Value<float>(), sizeArr[2].Value<float>());

            if (TryGetFloat(paramsObject, "basemapDistance", out var basemap))
                terrain.basemapDistance = basemap;

            if (paramsObject.TryGetValue("drawHeightmap", out var dhToken) && dhToken.Type == JTokenType.Boolean)
                terrain.drawHeightmap = dhToken.Value<bool>();

            if (paramsObject.TryGetValue("drawInstanced", out var diToken) && diToken.Type == JTokenType.Boolean)
                terrain.drawInstanced = diToken.Value<bool>();

            if (TryGetFloat(paramsObject, "detailObjectDistance", out var detailDist))
                terrain.detailObjectDistance = detailDist;

            if (TryGetFloat(paramsObject, "treeBillboardDistance", out var treeDist))
                terrain.treeBillboardDistance = treeDist;

            if (paramsObject.TryGetValue("shadowCastingMode", out var scmToken))
            {
                terrain.shadowCastingMode = ParseEnumToken<ShadowCastingMode>(scmToken, "shadowCastingMode");
            }

            EditorUtility.SetDirty(terrain);
            if (td != null)
                EditorUtility.SetDirty(td);

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                target = UnityMcpClient.CreateObjectSummary(terrain),
                applied = true
            });
        }
    }
}