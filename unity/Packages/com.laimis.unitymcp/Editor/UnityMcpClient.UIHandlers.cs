#nullable enable

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMcp.Editor
{
    internal sealed partial class UnityMcpClient
    {
        // ── RectTransform ───────────────────────────────────────────────────

        private static string BuildGetRectTransformSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "rectTransform.getSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var (rt, ownerGo) = ResolveComponentFromInstanceId<RectTransform>(instanceId, "rectTransform.getSettings");

            var settings = new
            {
                anchorMin = CreateVector2Array(rt.anchorMin),
                anchorMax = CreateVector2Array(rt.anchorMax),
                anchoredPosition = CreateVector2Array(rt.anchoredPosition),
                sizeDelta = CreateVector2Array(rt.sizeDelta),
                pivot = CreateVector2Array(rt.pivot),
                offsetMin = CreateVector2Array(rt.offsetMin),
                offsetMax = CreateVector2Array(rt.offsetMax),
                rect = new { x = rt.rect.x, y = rt.rect.y, width = rt.rect.width, height = rt.rect.height }
            };

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                target = CreateObjectSummary(ownerGo),
                component = CreateObjectSummary(rt),
                settings
            });
        }

        private static string BuildSetRectTransformSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "rectTransform.setSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var (rt, ownerGo) = ResolveComponentFromInstanceId<RectTransform>(instanceId, "rectTransform.setSettings");

            Undo.RecordObject(rt, "Set RectTransform Settings");
            var applied = new System.Collections.Generic.List<string>();

            if (paramsObject.TryGetValue("anchorMin", out var amin) && amin is JArray aminArr && aminArr.Count == 2)
            { rt.anchorMin = new Vector2(aminArr[0].Value<float>(), aminArr[1].Value<float>()); applied.Add("anchorMin"); }
            if (paramsObject.TryGetValue("anchorMax", out var amax) && amax is JArray amaxArr && amaxArr.Count == 2)
            { rt.anchorMax = new Vector2(amaxArr[0].Value<float>(), amaxArr[1].Value<float>()); applied.Add("anchorMax"); }
            if (paramsObject.TryGetValue("anchoredPosition", out var ap) && ap is JArray apArr && apArr.Count == 2)
            { rt.anchoredPosition = new Vector2(apArr[0].Value<float>(), apArr[1].Value<float>()); applied.Add("anchoredPosition"); }
            if (paramsObject.TryGetValue("sizeDelta", out var sd) && sd is JArray sdArr && sdArr.Count == 2)
            { rt.sizeDelta = new Vector2(sdArr[0].Value<float>(), sdArr[1].Value<float>()); applied.Add("sizeDelta"); }
            if (paramsObject.TryGetValue("pivot", out var pv) && pv is JArray pvArr && pvArr.Count == 2)
            { rt.pivot = new Vector2(pvArr[0].Value<float>(), pvArr[1].Value<float>()); applied.Add("pivot"); }
            if (paramsObject.TryGetValue("offsetMin", out var omin) && omin is JArray ominArr && ominArr.Count == 2)
            { rt.offsetMin = new Vector2(ominArr[0].Value<float>(), ominArr[1].Value<float>()); applied.Add("offsetMin"); }
            if (paramsObject.TryGetValue("offsetMax", out var omax) && omax is JArray omaxArr && omaxArr.Count == 2)
            { rt.offsetMax = new Vector2(omaxArr[0].Value<float>(), omaxArr[1].Value<float>()); applied.Add("offsetMax"); }

            EditorUtility.SetDirty(rt);
            return UnityMcpProtocol.CreateResult(idToken, new
            {
                target = CreateObjectSummary(ownerGo),
                component = CreateObjectSummary(rt),
                applied
            });
        }

        // ── Canvas ──────────────────────────────────────────────────────────

        private static string BuildGetCanvasSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "canvas.getSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var (canvas, ownerGo) = ResolveComponentFromInstanceId<Canvas>(instanceId, "canvas.getSettings");

            var settings = new
            {
                renderMode = canvas.renderMode.ToString(),
                sortingOrder = canvas.sortingOrder,
                targetDisplay = canvas.targetDisplay,
                pixelPerfect = canvas.pixelPerfect,
                planeDistance = canvas.planeDistance,
                overrideSorting = canvas.overrideSorting,
                enabled = canvas.enabled
            };

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                target = CreateObjectSummary(ownerGo),
                component = CreateObjectSummary(canvas),
                settings
            });
        }

        private static string BuildSetCanvasSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "canvas.setSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var (canvas, ownerGo) = ResolveComponentFromInstanceId<Canvas>(instanceId, "canvas.setSettings");

            Undo.RecordObject(canvas, "Set Canvas Settings");
            var applied = new System.Collections.Generic.List<string>();

            if (paramsObject.TryGetValue("renderMode", out var rm))
            { canvas.renderMode = ParseEnumToken<RenderMode>(rm, "renderMode"); applied.Add("renderMode"); }
            if (paramsObject.TryGetValue("sortingOrder", out var so) && so.Type == JTokenType.Integer)
            { canvas.sortingOrder = so.Value<int>(); applied.Add("sortingOrder"); }
            if (paramsObject.TryGetValue("targetDisplay", out var td) && td.Type == JTokenType.Integer)
            { canvas.targetDisplay = td.Value<int>(); applied.Add("targetDisplay"); }
            if (paramsObject.TryGetValue("pixelPerfect", out var pp) && pp.Type == JTokenType.Boolean)
            { canvas.pixelPerfect = pp.Value<bool>(); applied.Add("pixelPerfect"); }
            if (TryGetFloat(paramsObject, "planeDistance", out var pd))
            { canvas.planeDistance = pd; applied.Add("planeDistance"); }
            if (paramsObject.TryGetValue("overrideSorting", out var os) && os.Type == JTokenType.Boolean)
            { canvas.overrideSorting = os.Value<bool>(); applied.Add("overrideSorting"); }
            if (paramsObject.TryGetValue("enabled", out var en) && en.Type == JTokenType.Boolean)
            { canvas.enabled = en.Value<bool>(); applied.Add("enabled"); }

            EditorUtility.SetDirty(canvas);
            return UnityMcpProtocol.CreateResult(idToken, new
            {
                target = CreateObjectSummary(ownerGo),
                component = CreateObjectSummary(canvas),
                applied
            });
        }

        // ── CanvasGroup ─────────────────────────────────────────────────────

        private static string BuildGetCanvasGroupSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "canvasGroup.getSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var canvasGroup = ResolveComponentOfTypeTarget<CanvasGroup>(resolvedObject, "instanceId", "CanvasGroup");

            var result = new
            {
                target = CreateObjectSummary(canvasGroup.gameObject),
                component = CreateComponentSummary(canvasGroup),
                settings = new
                {
                    alpha = canvasGroup.alpha,
                    interactable = canvasGroup.interactable,
                    blocksRaycasts = canvasGroup.blocksRaycasts,
                    ignoreParentGroups = canvasGroup.ignoreParentGroups
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildSetCanvasGroupSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "canvasGroup.setSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var canvasGroup = ResolveComponentOfTypeTarget<CanvasGroup>(resolvedObject, "instanceId", "CanvasGroup");

            var alpha = ParseOptionalFloatParameter(paramsObject, "alpha");
            var interactable = ParseOptionalBooleanValueParameter(paramsObject, "interactable");
            var blocksRaycasts = ParseOptionalBooleanValueParameter(paramsObject, "blocksRaycasts");
            var ignoreParentGroups = ParseOptionalBooleanValueParameter(paramsObject, "ignoreParentGroups");

            if (!alpha.HasValue &&
                !interactable.HasValue &&
                !blocksRaycasts.HasValue &&
                !ignoreParentGroups.HasValue)
            {
                throw new ArgumentException(
                    "At least one CanvasGroup setting must be provided: alpha, interactable, blocksRaycasts, or ignoreParentGroups.");
            }

            if (alpha.HasValue && (alpha.Value < 0f || alpha.Value > 1f))
            {
                throw new ArgumentException("Parameter 'alpha' must be between 0 and 1.");
            }

            Undo.RecordObject(canvasGroup, "UnityMCP Set CanvasGroup Settings");

            if (alpha.HasValue)
            {
                canvasGroup.alpha = alpha.Value;
            }

            if (interactable.HasValue)
            {
                canvasGroup.interactable = interactable.Value;
            }

            if (blocksRaycasts.HasValue)
            {
                canvasGroup.blocksRaycasts = blocksRaycasts.Value;
            }

            if (ignoreParentGroups.HasValue)
            {
                canvasGroup.ignoreParentGroups = ignoreParentGroups.Value;
            }

            EditorUtility.SetDirty(canvasGroup);

            var result = new
            {
                target = CreateObjectSummary(canvasGroup.gameObject),
                component = CreateComponentSummary(canvasGroup),
                settings = new
                {
                    alpha = canvasGroup.alpha,
                    interactable = canvasGroup.interactable,
                    blocksRaycasts = canvasGroup.blocksRaycasts,
                    ignoreParentGroups = canvasGroup.ignoreParentGroups
                },
                applied = new
                {
                    alpha = alpha.HasValue,
                    interactable = interactable.HasValue,
                    blocksRaycasts = blocksRaycasts.HasValue,
                    ignoreParentGroups = ignoreParentGroups.HasValue
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }
    }
}
