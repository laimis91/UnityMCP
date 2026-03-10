#nullable enable

using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityMcp.Editor.UnityMcpParameterHelpers;

namespace UnityMcp.Editor
{
    internal static class MaterialHandler
    {
        // ── Helper Methods ────────────────────────────

        private static Material LoadMaterialFromAssetPath(string assetPath)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
                throw new ArgumentException($"No Material found at '{assetPath}'.");
            return material;
        }

        private static string GetShaderPropertyTypeName(ShaderPropertyType type)
        {
            return type switch
            {
                ShaderPropertyType.Color => "Color",
                ShaderPropertyType.Vector => "Vector",
                ShaderPropertyType.Float => "Float",
                ShaderPropertyType.Range => "Range",
                ShaderPropertyType.Texture => "Texture",
                _ => type.ToString()
            };
        }

        private static object? GetShaderPropertyValue(Material material, string propName, ShaderPropertyType propType)
        {
            switch (propType)
            {
                case ShaderPropertyType.Color:
                    var c = material.GetColor(propName);
                    return new { r = c.r, g = c.g, b = c.b, a = c.a };
                case ShaderPropertyType.Vector:
                    var v = material.GetVector(propName);
                    return new { x = v.x, y = v.y, z = v.z, w = v.w };
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                    return material.GetFloat(propName);
                case ShaderPropertyType.Texture:
                    var tex = material.GetTexture(propName);
                    return tex != null ? AssetDatabase.GetAssetPath(tex) : null;
                default:
                    // Try Int for newer Unity versions (ShaderPropertyType value 5)
                    if ((int)propType == 5)
                        return material.GetInt(propName);
                    return null;
            }
        }

        // ── Material Handler Methods ────────────────────────────

        internal static string BuildGetMaterialPropertiesResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "material.getProperties");
            var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
            var material = LoadMaterialFromAssetPath(assetPath);
            var shader = material.shader;
            var propertyCount = shader.GetPropertyCount();

            var properties = new object[propertyCount];
            for (var i = 0; i < propertyCount; i++)
            {
                var propName = shader.GetPropertyName(i);
                var propType = shader.GetPropertyType(i);
                properties[i] = new
                {
                    name = propName,
                    type = GetShaderPropertyTypeName(propType),
                    value = GetShaderPropertyValue(material, propName, propType)
                };
            }

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                assetPath,
                shaderName = shader.name,
                propertyCount,
                properties
            });
        }

        internal static string BuildGetMaterialPropertyResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "material.getProperty");
            var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
            var propertyName = ParseRequiredStringParameter(paramsObject, "propertyName");
            var material = LoadMaterialFromAssetPath(assetPath);
            var shader = material.shader;
            var propertyCount = shader.GetPropertyCount();

            for (var i = 0; i < propertyCount; i++)
            {
                var propName = shader.GetPropertyName(i);
                if (propName == propertyName)
                {
                    var propType = shader.GetPropertyType(i);
                    return UnityMcpProtocol.CreateResult(idToken, new
                    {
                        assetPath,
                        name = propName,
                        type = GetShaderPropertyTypeName(propType),
                        value = GetShaderPropertyValue(material, propName, propType)
                    });
                }
            }

            throw new ArgumentException($"Property '{propertyName}' not found on shader '{shader.name}'.");
        }

        internal static string BuildSetMaterialPropertyResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "material.setProperty");
            var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
            var propertyName = ParseRequiredStringParameter(paramsObject, "propertyName");
            var propertyType = ParseRequiredStringParameter(paramsObject, "propertyType").ToLowerInvariant();
            var material = LoadMaterialFromAssetPath(assetPath);

            if (!material.HasProperty(propertyName))
                throw new ArgumentException($"Property '{propertyName}' not found on material at '{assetPath}'.");

            if (!paramsObject.TryGetValue("value", out var valueToken))
                throw new ArgumentException("Parameter 'value' is required.");

            switch (propertyType)
            {
                case "color":
                {
                    var r = valueToken["r"]?.Value<float>() ?? 0f;
                    var g = valueToken["g"]?.Value<float>() ?? 0f;
                    var b = valueToken["b"]?.Value<float>() ?? 0f;
                    var a = valueToken["a"]?.Value<float>() ?? 1f;
                    material.SetColor(propertyName, new Color(r, g, b, a));
                    break;
                }
                case "vector":
                {
                    var x = valueToken["x"]?.Value<float>() ?? 0f;
                    var y = valueToken["y"]?.Value<float>() ?? 0f;
                    var z = valueToken["z"]?.Value<float>() ?? 0f;
                    var w = valueToken["w"]?.Value<float>() ?? 0f;
                    material.SetVector(propertyName, new Vector4(x, y, z, w));
                    break;
                }
                case "float":
                    material.SetFloat(propertyName, valueToken.Value<float>());
                    break;
                case "int":
                    material.SetInt(propertyName, valueToken.Value<int>());
                    break;
                case "texture":
                {
                    var texturePath = valueToken.Type == JTokenType.Null ? null : valueToken.Value<string>();
                    if (string.IsNullOrEmpty(texturePath))
                    {
                        material.SetTexture(propertyName, null);
                    }
                    else
                    {
                        var texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
                        if (texture == null)
                            throw new ArgumentException($"No Texture found at '{texturePath}'.");
                        material.SetTexture(propertyName, texture);
                    }
                    break;
                }
                default:
                    throw new ArgumentException($"Invalid propertyType '{propertyType}'. Expected: color, float, int, vector, texture.");
            }

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                assetPath,
                propertyName,
                propertyType,
                updated = true
            });
        }

        internal static string BuildGetMaterialKeywordsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "material.getKeywords");
            var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
            var material = LoadMaterialFromAssetPath(assetPath);

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                assetPath,
                keywords = material.shaderKeywords
            });
        }

        internal static string BuildSetMaterialKeywordResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "material.setKeyword");
            var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
            var keyword = ParseRequiredStringParameter(paramsObject, "keyword");
            var enabled = ParseRequiredBooleanParameter(paramsObject, "enabled");
            var material = LoadMaterialFromAssetPath(assetPath);

            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                assetPath,
                keyword,
                enabled,
                keywords = material.shaderKeywords
            });
        }

        internal static string BuildGetMaterialShaderResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "material.getShader");
            var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
            var material = LoadMaterialFromAssetPath(assetPath);

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                assetPath,
                shaderName = material.shader.name
            });
        }

        internal static string BuildSetMaterialShaderResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "material.setShader");
            var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
            var shaderName = ParseRequiredStringParameter(paramsObject, "shaderName");
            var material = LoadMaterialFromAssetPath(assetPath);

            var shader = Shader.Find(shaderName);
            if (shader == null)
                throw new ArgumentException($"Shader '{shaderName}' not found.");

            material.shader = shader;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                assetPath,
                shaderName = material.shader.name,
                updated = true
            });
        }

        internal static string BuildGetMaterialRenderQueueResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "material.getRenderQueue");
            var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
            var material = LoadMaterialFromAssetPath(assetPath);

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                assetPath,
                renderQueue = material.renderQueue
            });
        }

        internal static string BuildSetMaterialRenderQueueResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "material.setRenderQueue");
            var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
            var renderQueue = ParseRequiredIntegerParameter(paramsObject, "renderQueue");
            var material = LoadMaterialFromAssetPath(assetPath);

            material.renderQueue = renderQueue;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                assetPath,
                renderQueue = material.renderQueue,
                updated = true
            });
        }
    }
}