#nullable enable

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityMcp.Editor
{
    internal sealed partial class UnityMcpClient
    {
        private static string BuildGetMeshRendererSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "meshRenderer.getSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var (mr, ownerGo) = ResolveComponentFromInstanceId<MeshRenderer>(instanceId, "meshRenderer.getSettings");

            var materials = new System.Collections.Generic.List<object>();
            foreach (var mat in mr.sharedMaterials)
                materials.Add(new { name = mat != null ? mat.name : null, shader = mat != null ? mat.shader?.name : null });

            var settings = new
            {
                enabled = mr.enabled,
                shadowCastingMode = mr.shadowCastingMode.ToString(),
                receiveShadows = mr.receiveShadows,
                lightProbeUsage = mr.lightProbeUsage.ToString(),
                reflectionProbeUsage = mr.reflectionProbeUsage.ToString(),
                motionVectorGenerationMode = mr.motionVectorGenerationMode.ToString(),
                staticShadowCaster = mr.staticShadowCaster,
                allowOcclusionWhenDynamic = mr.allowOcclusionWhenDynamic,
                materialCount = mr.sharedMaterials.Length,
                materials
            };

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                target = CreateObjectSummary(ownerGo),
                component = CreateObjectSummary(mr),
                settings
            });
        }

        private static string BuildSetMeshRendererSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "meshRenderer.setSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var (mr, ownerGo) = ResolveComponentFromInstanceId<MeshRenderer>(instanceId, "meshRenderer.setSettings");

            Undo.RecordObject(mr, "Set MeshRenderer Settings");
            var applied = new System.Collections.Generic.List<string>();

            if (paramsObject.TryGetValue("enabled", out var en) && en.Type == JTokenType.Boolean)
            { mr.enabled = en.Value<bool>(); applied.Add("enabled"); }
            if (paramsObject.TryGetValue("shadowCastingMode", out var scm))
            { mr.shadowCastingMode = ParseEnumToken<UnityEngine.Rendering.ShadowCastingMode>(scm, "shadowCastingMode"); applied.Add("shadowCastingMode"); }
            if (paramsObject.TryGetValue("receiveShadows", out var rs) && rs.Type == JTokenType.Boolean)
            { mr.receiveShadows = rs.Value<bool>(); applied.Add("receiveShadows"); }
            if (paramsObject.TryGetValue("lightProbeUsage", out var lpu))
            { mr.lightProbeUsage = ParseEnumToken<UnityEngine.Rendering.LightProbeUsage>(lpu, "lightProbeUsage"); applied.Add("lightProbeUsage"); }
            if (paramsObject.TryGetValue("reflectionProbeUsage", out var rpu))
            { mr.reflectionProbeUsage = ParseEnumToken<UnityEngine.Rendering.ReflectionProbeUsage>(rpu, "reflectionProbeUsage"); applied.Add("reflectionProbeUsage"); }
            if (paramsObject.TryGetValue("motionVectorGenerationMode", out var mvm))
            { mr.motionVectorGenerationMode = ParseEnumToken<MotionVectorGenerationMode>(mvm, "motionVectorGenerationMode"); applied.Add("motionVectorGenerationMode"); }
            if (paramsObject.TryGetValue("staticShadowCaster", out var ssc) && ssc.Type == JTokenType.Boolean)
            { mr.staticShadowCaster = ssc.Value<bool>(); applied.Add("staticShadowCaster"); }
            if (paramsObject.TryGetValue("allowOcclusionWhenDynamic", out var aod) && aod.Type == JTokenType.Boolean)
            { mr.allowOcclusionWhenDynamic = aod.Value<bool>(); applied.Add("allowOcclusionWhenDynamic"); }

            EditorUtility.SetDirty(mr);
            return UnityMcpProtocol.CreateResult(idToken, new
            {
                target = CreateObjectSummary(ownerGo),
                component = CreateObjectSummary(mr),
                applied
            });
        }

        private static string BuildGetSkinnedMeshRendererSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "skinnedMeshRenderer.getSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var (smr, ownerGo) = ResolveComponentFromInstanceId<SkinnedMeshRenderer>(instanceId, "skinnedMeshRenderer.getSettings");

            var materials = new System.Collections.Generic.List<object>();
            foreach (var mat in smr.sharedMaterials)
                materials.Add(new { name = mat != null ? mat.name : null, shader = mat != null ? mat.shader?.name : null });

            var settings = new
            {
                enabled = smr.enabled,
                shadowCastingMode = smr.shadowCastingMode.ToString(),
                receiveShadows = smr.receiveShadows,
                lightProbeUsage = smr.lightProbeUsage.ToString(),
                reflectionProbeUsage = smr.reflectionProbeUsage.ToString(),
                motionVectorGenerationMode = smr.motionVectorGenerationMode.ToString(),
                staticShadowCaster = smr.staticShadowCaster,
                allowOcclusionWhenDynamic = smr.allowOcclusionWhenDynamic,
                materialCount = smr.sharedMaterials.Length,
                materials,
                rootBone = smr.rootBone != null ? smr.rootBone.name : null,
                quality = smr.quality.ToString(),
                updateWhenOffscreen = smr.updateWhenOffscreen,
                sharedMeshName = smr.sharedMesh != null ? smr.sharedMesh.name : null,
                blendShapeCount = smr.sharedMesh != null ? smr.sharedMesh.blendShapeCount : 0
            };

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                target = CreateObjectSummary(ownerGo),
                component = CreateObjectSummary(smr),
                settings
            });
        }

        private static string BuildSetSkinnedMeshRendererSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "skinnedMeshRenderer.setSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var (smr, ownerGo) = ResolveComponentFromInstanceId<SkinnedMeshRenderer>(instanceId, "skinnedMeshRenderer.setSettings");

            Undo.RecordObject(smr, "Set SkinnedMeshRenderer Settings");
            var applied = new System.Collections.Generic.List<string>();

            if (paramsObject.TryGetValue("enabled", out var en) && en.Type == JTokenType.Boolean)
            { smr.enabled = en.Value<bool>(); applied.Add("enabled"); }
            if (paramsObject.TryGetValue("shadowCastingMode", out var scm))
            { smr.shadowCastingMode = ParseEnumToken<UnityEngine.Rendering.ShadowCastingMode>(scm, "shadowCastingMode"); applied.Add("shadowCastingMode"); }
            if (paramsObject.TryGetValue("receiveShadows", out var rs) && rs.Type == JTokenType.Boolean)
            { smr.receiveShadows = rs.Value<bool>(); applied.Add("receiveShadows"); }
            if (paramsObject.TryGetValue("lightProbeUsage", out var lpu))
            { smr.lightProbeUsage = ParseEnumToken<UnityEngine.Rendering.LightProbeUsage>(lpu, "lightProbeUsage"); applied.Add("lightProbeUsage"); }
            if (paramsObject.TryGetValue("reflectionProbeUsage", out var rpu))
            { smr.reflectionProbeUsage = ParseEnumToken<UnityEngine.Rendering.ReflectionProbeUsage>(rpu, "reflectionProbeUsage"); applied.Add("reflectionProbeUsage"); }
            if (paramsObject.TryGetValue("motionVectorGenerationMode", out var mvm))
            { smr.motionVectorGenerationMode = ParseEnumToken<MotionVectorGenerationMode>(mvm, "motionVectorGenerationMode"); applied.Add("motionVectorGenerationMode"); }
            if (paramsObject.TryGetValue("staticShadowCaster", out var ssc) && ssc.Type == JTokenType.Boolean)
            { smr.staticShadowCaster = ssc.Value<bool>(); applied.Add("staticShadowCaster"); }
            if (paramsObject.TryGetValue("allowOcclusionWhenDynamic", out var aod) && aod.Type == JTokenType.Boolean)
            { smr.allowOcclusionWhenDynamic = aod.Value<bool>(); applied.Add("allowOcclusionWhenDynamic"); }
            if (paramsObject.TryGetValue("quality", out var q))
            { smr.quality = ParseEnumToken<SkinQuality>(q, "quality"); applied.Add("quality"); }
            if (paramsObject.TryGetValue("updateWhenOffscreen", out var uwo) && uwo.Type == JTokenType.Boolean)
            { smr.updateWhenOffscreen = uwo.Value<bool>(); applied.Add("updateWhenOffscreen"); }

            EditorUtility.SetDirty(smr);
            return UnityMcpProtocol.CreateResult(idToken, new
            {
                target = CreateObjectSummary(ownerGo),
                component = CreateObjectSummary(smr),
                applied
            });
        }

        private static string BuildGetSpriteRendererSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "spriteRenderer.getSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var sr = ResolveComponentOfTypeTarget<SpriteRenderer>(resolvedObject, "instanceId", "SpriteRenderer");

            var result = new
            {
                target = CreateObjectSummary(sr.gameObject),
                component = CreateComponentSummary(sr),
                settings = new
                {
                    spriteName = sr.sprite != null ? sr.sprite.name : (string?)null,
                    color = CreateColorArray(sr.color),
                    flipX = sr.flipX,
                    flipY = sr.flipY,
                    sortingLayerName = sr.sortingLayerName,
                    sortingOrder = sr.sortingOrder,
                    drawMode = CreateEnumSummary(sr.drawMode),
                    maskInteraction = CreateEnumSummary(sr.maskInteraction)
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildSetSpriteRendererSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "spriteRenderer.setSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var sr = ResolveComponentOfTypeTarget<SpriteRenderer>(resolvedObject, "instanceId", "SpriteRenderer");

            var color = ParseOptionalColorParameter(paramsObject, "color");
            var flipX = ParseOptionalBooleanValueParameter(paramsObject, "flipX");
            var flipY = ParseOptionalBooleanValueParameter(paramsObject, "flipY");
            var sortingLayerName = ParseOptionalStringParameter(paramsObject, "sortingLayerName");
            var sortingOrder = ParseOptionalIntegerParameter(paramsObject, "sortingOrder");

            if (!color.HasValue &&
                !flipX.HasValue &&
                !flipY.HasValue &&
                sortingLayerName == null &&
                !sortingOrder.HasValue)
            {
                throw new ArgumentException(
                    "At least one SpriteRenderer setting must be provided: color, flipX, flipY, sortingLayerName, or sortingOrder.");
            }

            Undo.RecordObject(sr, "UnityMCP Set SpriteRenderer Settings");

            if (color.HasValue)
            {
                sr.color = color.Value;
            }

            if (flipX.HasValue)
            {
                sr.flipX = flipX.Value;
            }

            if (flipY.HasValue)
            {
                sr.flipY = flipY.Value;
            }

            if (sortingLayerName != null)
            {
                sr.sortingLayerName = sortingLayerName;
            }

            if (sortingOrder.HasValue)
            {
                sr.sortingOrder = sortingOrder.Value;
            }

            EditorUtility.SetDirty(sr);

            var result = new
            {
                target = CreateObjectSummary(sr.gameObject),
                component = CreateComponentSummary(sr),
                settings = new
                {
                    spriteName = sr.sprite != null ? sr.sprite.name : (string?)null,
                    color = CreateColorArray(sr.color),
                    flipX = sr.flipX,
                    flipY = sr.flipY,
                    sortingLayerName = sr.sortingLayerName,
                    sortingOrder = sr.sortingOrder,
                    drawMode = CreateEnumSummary(sr.drawMode),
                    maskInteraction = CreateEnumSummary(sr.maskInteraction)
                },
                applied = new
                {
                    color = color.HasValue,
                    flipX = flipX.HasValue,
                    flipY = flipY.HasValue,
                    sortingLayerName = sortingLayerName != null,
                    sortingOrder = sortingOrder.HasValue
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildGetLineRendererSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "lineRenderer.getSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var lr = ResolveComponentOfTypeTarget<LineRenderer>(resolvedObject, "instanceId", "LineRenderer");

            var positions = new List<object>(lr.positionCount);
            for (var i = 0; i < lr.positionCount; i++)
            {
                var pos = lr.GetPosition(i);
                positions.Add(CreateVector3Array(pos));
            }

            var result = new
            {
                target = CreateObjectSummary(lr.gameObject),
                component = CreateComponentSummary(lr),
                settings = new
                {
                    positionCount = lr.positionCount,
                    positions,
                    loop = lr.loop,
                    startWidth = lr.startWidth,
                    endWidth = lr.endWidth,
                    useWorldSpace = lr.useWorldSpace,
                    startColor = CreateColorArray(lr.startColor),
                    endColor = CreateColorArray(lr.endColor)
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildSetLineRendererSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "lineRenderer.setSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var lr = ResolveComponentOfTypeTarget<LineRenderer>(resolvedObject, "instanceId", "LineRenderer");

            var loop = ParseOptionalBooleanValueParameter(paramsObject, "loop");
            var startWidth = ParseOptionalFloatParameter(paramsObject, "startWidth");
            var endWidth = ParseOptionalFloatParameter(paramsObject, "endWidth");
            var useWorldSpace = ParseOptionalBooleanValueParameter(paramsObject, "useWorldSpace");
            var startColor = ParseOptionalColorParameter(paramsObject, "startColor");
            var endColor = ParseOptionalColorParameter(paramsObject, "endColor");

            if (!loop.HasValue &&
                !startWidth.HasValue &&
                !endWidth.HasValue &&
                !useWorldSpace.HasValue &&
                !startColor.HasValue &&
                !endColor.HasValue)
            {
                throw new ArgumentException(
                    "At least one LineRenderer setting must be provided: loop, startWidth, endWidth, useWorldSpace, startColor, or endColor.");
            }

            if (startWidth.HasValue && startWidth.Value < 0f)
            {
                throw new ArgumentException("Parameter 'startWidth' must be greater than or equal to 0.");
            }

            if (endWidth.HasValue && endWidth.Value < 0f)
            {
                throw new ArgumentException("Parameter 'endWidth' must be greater than or equal to 0.");
            }

            Undo.RecordObject(lr, "UnityMCP Set LineRenderer Settings");

            if (loop.HasValue)
            {
                lr.loop = loop.Value;
            }

            if (startWidth.HasValue)
            {
                lr.startWidth = startWidth.Value;
            }

            if (endWidth.HasValue)
            {
                lr.endWidth = endWidth.Value;
            }

            if (useWorldSpace.HasValue)
            {
                lr.useWorldSpace = useWorldSpace.Value;
            }

            if (startColor.HasValue)
            {
                lr.startColor = startColor.Value;
            }

            if (endColor.HasValue)
            {
                lr.endColor = endColor.Value;
            }

            EditorUtility.SetDirty(lr);

            var positionsAfter = new List<object>(lr.positionCount);
            for (var i = 0; i < lr.positionCount; i++)
            {
                positionsAfter.Add(CreateVector3Array(lr.GetPosition(i)));
            }

            var result = new
            {
                target = CreateObjectSummary(lr.gameObject),
                component = CreateComponentSummary(lr),
                settings = new
                {
                    positionCount = lr.positionCount,
                    positions = positionsAfter,
                    loop = lr.loop,
                    startWidth = lr.startWidth,
                    endWidth = lr.endWidth,
                    useWorldSpace = lr.useWorldSpace,
                    startColor = CreateColorArray(lr.startColor),
                    endColor = CreateColorArray(lr.endColor)
                },
                applied = new
                {
                    loop = loop.HasValue,
                    startWidth = startWidth.HasValue,
                    endWidth = endWidth.HasValue,
                    useWorldSpace = useWorldSpace.HasValue,
                    startColor = startColor.HasValue,
                    endColor = endColor.HasValue
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildGetRendererMaterialsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "renderer.getMaterials");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var renderer = ResolveComponentOfTypeTarget<Renderer>(resolvedObject, "instanceId", "Renderer");

            var sharedMaterials = renderer.sharedMaterials;
            var materials = new object[sharedMaterials.Length];
            for (var i = 0; i < sharedMaterials.Length; i++)
            {
                var mat = sharedMaterials[i];
                materials[i] = mat != null
                    ? new { name = (string?)mat.name, instanceId = (int?)mat.GetInstanceID() }
                    : new { name = (string?)null, instanceId = (int?)null };
            }

            var result = new
            {
                target = CreateObjectSummary(renderer.gameObject),
                component = CreateComponentSummary(renderer),
                materialCount = sharedMaterials.Length,
                materials
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildSetRendererMaterialResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "renderer.setMaterial");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var renderer = ResolveComponentOfTypeTarget<Renderer>(resolvedObject, "instanceId", "Renderer");

            var materialIndex = ParseRequiredIntegerParameter(paramsObject, "materialIndex");
            var materialAssetPath = ParseRequiredStringParameter(paramsObject, "materialAssetPath");

            var sharedMaterials = renderer.sharedMaterials;
            if (materialIndex < 0 || materialIndex >= sharedMaterials.Length)
            {
                throw new ArgumentException($"Parameter 'materialIndex' ({materialIndex}) is out of range. Renderer has {sharedMaterials.Length} material slot(s).");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
            if (material == null)
            {
                throw new ArgumentException($"No Material found at asset path '{materialAssetPath}'.");
            }

            Undo.RecordObject(renderer, "UnityMCP Set Renderer Material");

            sharedMaterials[materialIndex] = material;
            renderer.sharedMaterials = sharedMaterials;

            EditorUtility.SetDirty(renderer);

            // Return updated materials list
            var updatedMaterials = renderer.sharedMaterials;
            var materialsResult = new object[updatedMaterials.Length];
            for (var i = 0; i < updatedMaterials.Length; i++)
            {
                var mat = updatedMaterials[i];
                materialsResult[i] = mat != null
                    ? new { name = (string?)mat.name, instanceId = (int?)mat.GetInstanceID() }
                    : new { name = (string?)null, instanceId = (int?)null };
            }

            var result = new
            {
                target = CreateObjectSummary(renderer.gameObject),
                component = CreateComponentSummary(renderer),
                materialIndex,
                materialAssetPath,
                materialCount = updatedMaterials.Length,
                materials = materialsResult
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }
    }
}
