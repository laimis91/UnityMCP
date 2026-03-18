#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMcp.Editor
{
    internal sealed partial class UnityMcpClient
    {
        private static string BuildFindAssetsResponse(JToken idToken, JObject root)
        {
            if (!root.TryGetValue("params", out var paramsToken) || paramsToken is not JObject paramsObject)
            {
                throw new ArgumentException("Method 'assets.find' expects params to be an object.");
            }

            if (!paramsObject.TryGetValue("query", out var queryToken) || queryToken.Type != JTokenType.String)
            {
                throw new ArgumentException("Parameter 'query' is required and must be a string.");
            }

            var query = queryToken.Value<string>();
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("Parameter 'query' cannot be empty.");
            }

            var maxResults = 100;
            if (paramsObject.TryGetValue("maxResults", out var maxResultsToken))
            {
                if (maxResultsToken.Type != JTokenType.Integer)
                {
                    throw new ArgumentException("Parameter 'maxResults' must be an integer.");
                }

                var parsedMaxResults = maxResultsToken.Value<int?>();
                if (!parsedMaxResults.HasValue)
                {
                    throw new ArgumentException("Parameter 'maxResults' must be an integer.");
                }

                if (parsedMaxResults.Value < 1 || parsedMaxResults.Value > 500)
                {
                    throw new ArgumentException("Parameter 'maxResults' must be between 1 and 500.");
                }

                maxResults = parsedMaxResults.Value;
            }

            var searchInFolders = ParseOptionalStringArrayParameter(paramsObject, "searchInFolders");
            if (searchInFolders != null)
            {
                for (var index = 0; index < searchInFolders.Count; index++)
                {
                    var normalizedFolder = NormalizeAndValidateAssetPath(searchInFolders[index]);
                    if (!AssetDatabase.IsValidFolder(normalizedFolder))
                    {
                        throw new ArgumentException($"Search folder '{normalizedFolder}' does not exist or is not a valid Unity folder.");
                    }

                    searchInFolders[index] = normalizedFolder;
                }
            }

            var types = ParseOptionalStringArrayParameter(paramsObject, "types");
            var labels = ParseOptionalStringArrayParameter(paramsObject, "labels");
            var effectiveQuery = BuildEffectiveAssetsFindQuery(query!, types, labels);

            var guids = searchInFolders is { Count: > 0 }
                ? AssetDatabase.FindAssets(effectiveQuery, searchInFolders.ToArray())
                : AssetDatabase.FindAssets(effectiveQuery);
            var takeCount = Math.Min(maxResults, guids.Length);
            var items = new List<object>(takeCount);
            for (var index = 0; index < takeCount; index++)
            {
                var guid = guids[index];
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var mainAssetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);

                items.Add(new
                {
                    guid,
                    assetPath,
                    isFolder = AssetDatabase.IsValidFolder(assetPath),
                    mainAssetType = mainAssetType != null ? mainAssetType.FullName : null,
                    mainAssetName = mainAsset != null ? mainAsset.name : null
                });
            }

            var result = new
            {
                query,
                effectiveQuery,
                searchInFolders,
                types,
                labels,
                totalMatched = guids.Length,
                returnedCount = items.Count,
                maxResults,
                truncated = guids.Length > takeCount,
                items
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildImportAssetResponse(JToken idToken, JObject root)
        {
            if (!root.TryGetValue("params", out var paramsToken) || paramsToken is not JObject paramsObject)
            {
                throw new ArgumentException("Method 'assets.import' expects params to be an object.");
            }

            if (!paramsObject.TryGetValue("assetPath", out var assetPathToken) || assetPathToken.Type != JTokenType.String)
            {
                throw new ArgumentException("Parameter 'assetPath' is required and must be a string.");
            }

            var rawAssetPath = assetPathToken.Value<string>();
            var assetPath = NormalizeAndValidateAssetPath(rawAssetPath);

            var absoluteAssetPath = GetAbsoluteProjectPath(assetPath);
            var isFolder = Directory.Exists(absoluteAssetPath);
            var isFile = File.Exists(absoluteAssetPath);
            if (!isFolder && !isFile)
            {
                throw new ArgumentException($"Asset path '{assetPath}' does not exist in the Unity project.");
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh(ImportAssetOptions.Default);

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(guid))
            {
                throw new InvalidOperationException($"Unity did not return a GUID for imported asset '{assetPath}'.");
            }

            var mainAssetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);

            var result = new
            {
                assetPath,
                guid,
                isFolder,
                exists = true,
                mainAssetType = mainAssetType?.FullName,
                mainAssetName = mainAsset != null ? mainAsset.name : null,
                imported = true
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildPingAssetResponse(JToken idToken, JObject root)
        {
            var (assetPath, guid, targetObject, isFolder) = ResolveAssetNavigationTarget(root, "assets.ping");
            EditorGUIUtility.PingObject(targetObject);

            var result = new
            {
                pinged = true,
                assetPath,
                guid,
                isFolder,
                target = CreateObjectSummary(targetObject)
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildRevealAssetResponse(JToken idToken, JObject root)
        {
            var (assetPath, guid, targetObject, isFolder) = ResolveAssetNavigationTarget(root, "assets.reveal");

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = targetObject;
            EditorGUIUtility.PingObject(targetObject);

            var result = new
            {
                revealed = true,
                focusedProjectWindow = true,
                assetPath,
                guid,
                isFolder,
                target = CreateObjectSummary(targetObject)
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildCreateFolderResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "assets.createFolder");
            if (!paramsObject.TryGetValue("parentFolder", out var parentToken) || parentToken.Type != JTokenType.String)
                throw new ArgumentException("Parameter 'parentFolder' is required and must be a string.");
            if (!paramsObject.TryGetValue("folderName", out var nameToken) || nameToken.Type != JTokenType.String)
                throw new ArgumentException("Parameter 'folderName' is required and must be a string.");

            var parentFolder = parentToken.Value<string>()!;
            var folderName = nameToken.Value<string>()!;

            if (!AssetDatabase.IsValidFolder(parentFolder))
                throw new ArgumentException($"Parent folder '{parentFolder}' does not exist.");

            var guid = AssetDatabase.CreateFolder(parentFolder, folderName);
            var createdPath = AssetDatabase.GUIDToAssetPath(guid);

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                created = true,
                assetPath = createdPath,
                guid
            });
        }

        private static string BuildCreateScriptResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "assets.createScript");
            if (!paramsObject.TryGetValue("assetPath", out var pathToken) || pathToken.Type != JTokenType.String)
                throw new ArgumentException("Parameter 'assetPath' is required and must be a string.");

            var assetPath = pathToken.Value<string>()!;
            if (!assetPath.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Parameter 'assetPath' must end with '.cs'.");

            string content;
            if (paramsObject.TryGetValue("content", out var contentToken) && contentToken.Type == JTokenType.String)
            {
                content = contentToken.Value<string>()!;
            }
            else
            {
                var className = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                content = $"using UnityEngine;\n\npublic class {className} : MonoBehaviour\n{{\n    void Start()\n    {{\n    }}\n\n    void Update()\n    {{\n    }}\n}}\n";
            }

            var fullPath = System.IO.Path.Combine(Application.dataPath, "..", assetPath);
            fullPath = System.IO.Path.GetFullPath(fullPath);
            var dir = System.IO.Path.GetDirectoryName(fullPath)!;
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            System.IO.File.WriteAllText(fullPath, content);
            AssetDatabase.Refresh();

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            return UnityMcpProtocol.CreateResult(idToken, new
            {
                created = true,
                assetPath,
                guid
            });
        }

        private static string BuildCreateMaterialResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "assets.createMaterial");
            if (!paramsObject.TryGetValue("assetPath", out var pathToken) || pathToken.Type != JTokenType.String)
                throw new ArgumentException("Parameter 'assetPath' is required and must be a string.");

            var assetPath = pathToken.Value<string>()!;
            var shaderName = "Standard";
            if (paramsObject.TryGetValue("shaderName", out var shaderToken) && shaderToken.Type == JTokenType.String)
                shaderName = shaderToken.Value<string>()!;

            var shader = Shader.Find(shaderName);
            if (shader == null)
                throw new ArgumentException($"Shader '{shaderName}' not found.");

            var material = new Material(shader);
            AssetDatabase.CreateAsset(material, assetPath);
            AssetDatabase.SaveAssets();

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            return UnityMcpProtocol.CreateResult(idToken, new
            {
                created = true,
                assetPath,
                guid,
                shaderName
            });
        }

        private static string BuildCreatePrefabResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "assets.createPrefab");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            if (!paramsObject.TryGetValue("assetPath", out var pathToken) || pathToken.Type != JTokenType.String)
                throw new ArgumentException("Parameter 'assetPath' is required and must be a string.");

            var assetPath = pathToken.Value<string>()!;
            var gameObject = ResolveGameObjectFromInstanceId(instanceId, "assets.createPrefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(gameObject, assetPath);
            if (prefab == null)
                throw new System.Exception($"Failed to save prefab at '{assetPath}'.");

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            return UnityMcpProtocol.CreateResult(idToken, new
            {
                created = true,
                assetPath,
                guid,
                source = CreateObjectSummary(gameObject)
            });
        }

        private static string BuildCreateScriptableObjectResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "assets.createScriptableObject");
            var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
            var typeName = ParseRequiredStringParameter(paramsObject, "typeName");

            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/"))
                throw new ArgumentException("Parameter 'assetPath' must be a project-relative path starting with 'Assets/'.");

            if (!assetPath.EndsWith(".asset"))
                assetPath += ".asset";

            // Ensure directory exists
            var dir = System.IO.Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                // Create directory hierarchy
                var parts = dir.Split('/');
                var current = parts[0];
                for (var i = 1; i < parts.Length; i++)
                {
                    var next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }

            // Find the type by name
            System.Type? soType = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                soType = asm.GetType(typeName);
                if (soType != null) break;
            }

            if (soType == null)
            {
                // Try short name search
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t.Name == typeName && t.IsSubclassOf(typeof(ScriptableObject)))
                        {
                            soType = t;
                            break;
                        }
                    }
                    if (soType != null) break;
                }
            }

            if (soType == null || !soType.IsSubclassOf(typeof(ScriptableObject)))
                throw new ArgumentException($"Type '{typeName}' was not found or is not a ScriptableObject subclass.");

            var instance = ScriptableObject.CreateInstance(soType);
            AssetDatabase.CreateAsset(instance, assetPath);
            AssetDatabase.SaveAssets();

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            return UnityMcpProtocol.CreateResult(idToken, new
            {
                assetPath,
                typeName = soType.FullName,
                guid,
                created = true
            });
        }

        private static string BuildDeleteAssetResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "assets.delete");
            if (!paramsObject.TryGetValue("assetPath", out var pathToken) || pathToken.Type != JTokenType.String)
                throw new ArgumentException("Parameter 'assetPath' is required and must be a string.");

            var assetPath = pathToken.Value<string>()!;
            var deleted = AssetDatabase.DeleteAsset(assetPath);
            if (!deleted)
                throw new ArgumentException($"Failed to delete asset at '{assetPath}'. Check the path exists.");

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                deleted = true,
                assetPath
            });
        }

        private static string BuildMoveAssetResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "assets.move");
            if (!paramsObject.TryGetValue("sourcePath", out var srcToken) || srcToken.Type != JTokenType.String)
                throw new ArgumentException("Parameter 'sourcePath' is required and must be a string.");
            if (!paramsObject.TryGetValue("destinationPath", out var dstToken) || dstToken.Type != JTokenType.String)
                throw new ArgumentException("Parameter 'destinationPath' is required and must be a string.");

            var sourcePath = srcToken.Value<string>()!;
            var destinationPath = dstToken.Value<string>()!;

            var error = AssetDatabase.MoveAsset(sourcePath, destinationPath);
            if (!string.IsNullOrEmpty(error))
                throw new ArgumentException($"Move failed: {error}");

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                moved = true,
                sourcePath,
                destinationPath,
                guid = AssetDatabase.AssetPathToGUID(destinationPath)
            });
        }

        private static List<string>? ParseOptionalStringArrayParameter(JObject paramsObject, string parameterName)
        {
            if (!paramsObject.TryGetValue(parameterName, out var token))
            {
                return null;
            }

            if (token.Type != JTokenType.Array || token is not JArray array)
            {
                throw new ArgumentException($"Parameter '{parameterName}' must be an array of strings.");
            }

            var values = new List<string>(array.Count);
            foreach (var item in array)
            {
                if (item.Type != JTokenType.String)
                {
                    throw new ArgumentException($"Parameter '{parameterName}' must contain only strings.");
                }

                var value = item.Value<string>();
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException($"Parameter '{parameterName}' cannot contain empty values.");
                }

                values.Add(value!.Trim());
            }

            return values;
        }

        private static string BuildEffectiveAssetsFindQuery(string query, List<string>? types, List<string>? labels)
        {
            var parts = new List<string> { query };

            if (types != null)
            {
                foreach (var type in types)
                {
                    parts.Add($"t:{type}");
                }
            }

            if (labels != null)
            {
                foreach (var label in labels)
                {
                    parts.Add($"l:{label}");
                }
            }

            return string.Join(" ", parts);
        }

        private static TextureImporter LoadTextureImporterFromAssetPath(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null)
                throw new ArgumentException($"No asset found at '{assetPath}'.");
            if (importer is not TextureImporter textureImporter)
                throw new ArgumentException($"Asset at '{assetPath}' is not a texture (importer type: {importer.GetType().Name}).");
            return textureImporter;
        }

        private static string TextureImporterTypeToString(TextureImporterType type)
        {
            return type switch
            {
                TextureImporterType.Default => "Default",
                TextureImporterType.NormalMap => "NormalMap",
                TextureImporterType.GUI => "Editor GUI and Legacy GUI",
                TextureImporterType.Sprite => "Sprite",
                TextureImporterType.Cursor => "Cursor",
                TextureImporterType.Cookie => "Cookie",
                TextureImporterType.Lightmap => "Lightmap",
                TextureImporterType.SingleChannel => "Single Channel",
                _ => type.ToString()
            };
        }

        private static TextureImporterType? ParseOptionalTextureImporterType(JObject paramsObject, string paramName)
        {
            var value = ParseOptionalStringParameter(paramsObject, paramName);
            if (value == null) return null;
            return value switch
            {
                "Default" => TextureImporterType.Default,
                "NormalMap" => TextureImporterType.NormalMap,
                "Editor GUI and Legacy GUI" => TextureImporterType.GUI,
                "Sprite" => TextureImporterType.Sprite,
                "Cursor" => TextureImporterType.Cursor,
                "Cookie" => TextureImporterType.Cookie,
                "Lightmap" => TextureImporterType.Lightmap,
                "Single Channel" => TextureImporterType.SingleChannel,
                _ => throw new ArgumentException($"Invalid value for {paramName}: '{value}'. Valid values: Default, NormalMap, Editor GUI and Legacy GUI, Sprite, Cursor, Cookie, Lightmap, Single Channel.")
            };
        }

        private static TextureImporterCompression? ParseOptionalTextureImporterCompression(JObject paramsObject, string paramName)
        {
            var value = ParseOptionalStringParameter(paramsObject, paramName);
            if (value == null) return null;
            return value switch
            {
                "Uncompressed" => TextureImporterCompression.Uncompressed,
                "Compressed" => TextureImporterCompression.Compressed,
                "CompressedHQ" => TextureImporterCompression.CompressedHQ,
                "CompressedLQ" => TextureImporterCompression.CompressedLQ,
                _ => throw new ArgumentException($"Invalid value for {paramName}: '{value}'. Valid values: Uncompressed, Compressed, CompressedHQ, CompressedLQ.")
            };
        }

        private static FilterMode? ParseOptionalFilterModeParameter(JObject paramsObject, string paramName)
        {
            var value = ParseOptionalStringParameter(paramsObject, paramName);
            if (value == null) return null;
            return value switch
            {
                "Point" => FilterMode.Point,
                "Bilinear" => FilterMode.Bilinear,
                "Trilinear" => FilterMode.Trilinear,
                _ => throw new ArgumentException($"Invalid value for {paramName}: '{value}'. Valid values: Point, Bilinear, Trilinear.")
            };
        }

        private static TextureWrapMode? ParseOptionalTextureWrapModeParameter(JObject paramsObject, string paramName)
        {
            var value = ParseOptionalStringParameter(paramsObject, paramName);
            if (value == null) return null;
            return value switch
            {
                "Repeat" => TextureWrapMode.Repeat,
                "Clamp" => TextureWrapMode.Clamp,
                "Mirror" => TextureWrapMode.Mirror,
                "MirrorOnce" => TextureWrapMode.MirrorOnce,
                _ => throw new ArgumentException($"Invalid value for {paramName}: '{value}'. Valid values: Repeat, Clamp, Mirror, MirrorOnce.")
            };
        }

        private static TextureImporterAlphaSource? ParseOptionalTextureImporterAlphaSource(JObject paramsObject, string paramName)
        {
            var value = ParseOptionalStringParameter(paramsObject, paramName);
            if (value == null) return null;
            return value switch
            {
                "None" => TextureImporterAlphaSource.None,
                "FromInput" => TextureImporterAlphaSource.FromInput,
                "FromGrayScale" => TextureImporterAlphaSource.FromGrayScale,
                _ => throw new ArgumentException($"Invalid value for {paramName}: '{value}'. Valid values: None, FromInput, FromGrayScale.")
            };
        }

        private static TextureImporterNPOTScale? ParseOptionalTextureImporterNPOTScale(JObject paramsObject, string paramName)
        {
            var value = ParseOptionalStringParameter(paramsObject, paramName);
            if (value == null) return null;
            return value switch
            {
                "None" => TextureImporterNPOTScale.None,
                "ToNearest" => TextureImporterNPOTScale.ToNearest,
                "ToLarger" => TextureImporterNPOTScale.ToLarger,
                "ToSmaller" => TextureImporterNPOTScale.ToSmaller,
                _ => throw new ArgumentException($"Invalid value for {paramName}: '{value}'. Valid values: None, ToNearest, ToLarger, ToSmaller.")
            };
        }

        private static object BuildTextureImporterSettingsObject(string assetPath, TextureImporter importer)
        {
            return new
            {
                assetPath,
                textureType = TextureImporterTypeToString(importer.textureType),
                maxTextureSize = importer.maxTextureSize,
                textureCompression = importer.textureCompression.ToString(),
                filterMode = importer.filterMode.ToString(),
                wrapMode = importer.wrapMode.ToString(),
                mipmapEnabled = importer.mipmapEnabled,
                isReadable = importer.isReadable,
                sRGBTexture = importer.sRGBTexture,
                alphaSource = importer.alphaSource.ToString(),
                npotScale = importer.npotScale.ToString(),
                anisoLevel = importer.anisoLevel,
                spriteMode = (int)importer.spriteImportMode,
                spritePixelsPerUnit = importer.spritePixelsPerUnit
            };
        }

        private static string BuildGetTextureImporterSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "textureImporter.getSettings");
            var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
            var importer = LoadTextureImporterFromAssetPath(assetPath);

            return UnityMcpProtocol.CreateResult(idToken, BuildTextureImporterSettingsObject(assetPath, importer));
        }

        private static string BuildSetTextureImporterSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "textureImporter.setSettings");
            var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");

            var textureType = ParseOptionalTextureImporterType(paramsObject, "textureType");
            var maxTextureSize = ParseOptionalIntegerParameter(paramsObject, "maxTextureSize");
            var textureCompression = ParseOptionalTextureImporterCompression(paramsObject, "textureCompression");
            var filterMode = ParseOptionalFilterModeParameter(paramsObject, "filterMode");
            var wrapMode = ParseOptionalTextureWrapModeParameter(paramsObject, "wrapMode");
            var mipmapEnabled = ParseOptionalBooleanValueParameter(paramsObject, "mipmapEnabled");
            var isReadable = ParseOptionalBooleanValueParameter(paramsObject, "isReadable");
            var sRGBTexture = ParseOptionalBooleanValueParameter(paramsObject, "sRGBTexture");
            var alphaSource = ParseOptionalTextureImporterAlphaSource(paramsObject, "alphaSource");
            var npotScale = ParseOptionalTextureImporterNPOTScale(paramsObject, "npotScale");
            var anisoLevel = ParseOptionalIntegerParameter(paramsObject, "anisoLevel");
            var spriteMode = ParseOptionalIntegerParameter(paramsObject, "spriteMode");
            var spritePixelsPerUnit = ParseOptionalFloatParameter(paramsObject, "spritePixelsPerUnit");

            var hasAnyParam = textureType.HasValue || maxTextureSize.HasValue || textureCompression.HasValue ||
                              filterMode.HasValue || wrapMode.HasValue || mipmapEnabled.HasValue ||
                              isReadable.HasValue || sRGBTexture.HasValue || alphaSource.HasValue ||
                              npotScale.HasValue || anisoLevel.HasValue || spriteMode.HasValue ||
                              spritePixelsPerUnit.HasValue;

            if (!hasAnyParam)
                throw new ArgumentException("At least one texture import setting must be provided.");

            var importer = LoadTextureImporterFromAssetPath(assetPath);

            if (textureType.HasValue) importer.textureType = textureType.Value;
            if (maxTextureSize.HasValue) importer.maxTextureSize = maxTextureSize.Value;
            if (textureCompression.HasValue) importer.textureCompression = textureCompression.Value;
            if (filterMode.HasValue) importer.filterMode = filterMode.Value;
            if (wrapMode.HasValue) importer.wrapMode = wrapMode.Value;
            if (mipmapEnabled.HasValue) importer.mipmapEnabled = mipmapEnabled.Value;
            if (isReadable.HasValue) importer.isReadable = isReadable.Value;
            if (sRGBTexture.HasValue) importer.sRGBTexture = sRGBTexture.Value;
            if (alphaSource.HasValue) importer.alphaSource = alphaSource.Value;
            if (npotScale.HasValue) importer.npotScale = npotScale.Value;
            if (anisoLevel.HasValue) importer.anisoLevel = anisoLevel.Value;
            if (spriteMode.HasValue) importer.spriteImportMode = (SpriteImportMode)spriteMode.Value;
            if (spritePixelsPerUnit.HasValue) importer.spritePixelsPerUnit = spritePixelsPerUnit.Value;

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                assetPath,
                updated = true,
                settings = BuildTextureImporterSettingsObject(assetPath, importer)
            });
        }
    }
}
