#nullable enable

using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using static UnityMcp.Editor.UnityMcpParameterHelpers;
using static UnityMcp.Editor.UnityMcpResolvers;
using static UnityMcp.Editor.UnityMcpSnapshotHelpers;

namespace UnityMcp.Editor
{
    internal static class PhysicsHandler
    {
        // ── Rigidbody Methods ──────────────────────────────────────────────────

        internal static string BuildGetRigidbodySettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "rigidbody.getSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var rigidbody = ResolveComponentOfTypeTarget<Rigidbody>(resolvedObject, "instanceId", "Rigidbody");

            var result = new
            {
                target = CreateObjectSummary(rigidbody.gameObject),
                component = CreateComponentSummary(rigidbody),
                settings = CreateRigidbodySettingsSnapshot(rigidbody)
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildSetRigidbodySettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "rigidbody.setSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var rigidbody = ResolveComponentOfTypeTarget<Rigidbody>(resolvedObject, "instanceId", "Rigidbody");

            var mass = ParseOptionalFloatParameter(paramsObject, "mass");
            var drag = ParseOptionalFloatParameter(paramsObject, "drag");
            var angularDrag = ParseOptionalFloatParameter(paramsObject, "angularDrag");
            var useGravity = ParseOptionalBooleanValueParameter(paramsObject, "useGravity");
            var isKinematic = ParseOptionalBooleanValueParameter(paramsObject, "isKinematic");
            var detectCollisions = ParseOptionalBooleanValueParameter(paramsObject, "detectCollisions");
            var constraints = ParseOptionalEnumParameter<RigidbodyConstraints>(paramsObject, "constraints");
            var interpolation = ParseOptionalEnumParameter<RigidbodyInterpolation>(paramsObject, "interpolation");
            var collisionDetectionMode = ParseOptionalEnumParameter<CollisionDetectionMode>(paramsObject, "collisionDetectionMode");

            if (!mass.HasValue &&
                !drag.HasValue &&
                !angularDrag.HasValue &&
                !useGravity.HasValue &&
                !isKinematic.HasValue &&
                !detectCollisions.HasValue &&
                !constraints.HasValue &&
                !interpolation.HasValue &&
                !collisionDetectionMode.HasValue)
            {
                throw new ArgumentException(
                    "At least one rigidbody setting must be provided: mass, drag, angularDrag, useGravity, isKinematic, detectCollisions, constraints, interpolation, or collisionDetectionMode.");
            }

            if (mass.HasValue && mass.Value <= 0f)
            {
                throw new ArgumentException("Parameter 'mass' must be greater than 0.");
            }

            if (drag.HasValue && drag.Value < 0f)
            {
                throw new ArgumentException("Parameter 'drag' must be greater than or equal to 0.");
            }

            if (angularDrag.HasValue && angularDrag.Value < 0f)
            {
                throw new ArgumentException("Parameter 'angularDrag' must be greater than or equal to 0.");
            }

            Undo.RecordObject(rigidbody, "UnityMCP Set Rigidbody Settings");

            if (mass.HasValue)
            {
                rigidbody.mass = mass.Value;
            }

            #pragma warning disable CS0618
            if (drag.HasValue)
            {
                rigidbody.drag = drag.Value;
            }

            if (angularDrag.HasValue)
            {
                rigidbody.angularDrag = angularDrag.Value;
            }
            #pragma warning restore CS0618

            if (useGravity.HasValue)
            {
                rigidbody.useGravity = useGravity.Value;
            }

            if (isKinematic.HasValue)
            {
                rigidbody.isKinematic = isKinematic.Value;
            }

            if (detectCollisions.HasValue)
            {
                rigidbody.detectCollisions = detectCollisions.Value;
            }

            if (constraints.HasValue)
            {
                rigidbody.constraints = constraints.Value;
            }

            if (interpolation.HasValue)
            {
                rigidbody.interpolation = interpolation.Value;
            }

            if (collisionDetectionMode.HasValue)
            {
                rigidbody.collisionDetectionMode = collisionDetectionMode.Value;
            }

            EditorUtility.SetDirty(rigidbody);

            var result = new
            {
                target = CreateObjectSummary(rigidbody.gameObject),
                component = CreateComponentSummary(rigidbody),
                settings = CreateRigidbodySettingsSnapshot(rigidbody),
                applied = new
                {
                    mass = mass.HasValue,
                    drag = drag.HasValue,
                    angularDrag = angularDrag.HasValue,
                    useGravity = useGravity.HasValue,
                    isKinematic = isKinematic.HasValue,
                    detectCollisions = detectCollisions.HasValue,
                    constraints = constraints.HasValue,
                    interpolation = interpolation.HasValue,
                    collisionDetectionMode = collisionDetectionMode.HasValue
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        // ── Generic Collider Methods ──────────────────────────────────────────

        internal static string BuildGetColliderSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "collider.getSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var collider = ResolveComponentOfTypeTarget<Collider>(resolvedObject, "instanceId", "Collider");

            var result = new
            {
                target = CreateObjectSummary(collider.gameObject),
                component = CreateComponentSummary(collider),
                settings = CreateColliderSettingsSnapshot(collider)
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildSetColliderSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "collider.setSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var collider = ResolveComponentOfTypeTarget<Collider>(resolvedObject, "instanceId", "Collider");

            var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
            var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
            var contactOffset = ParseOptionalFloatParameter(paramsObject, "contactOffset");
            var center = ParseOptionalVector3Parameter(paramsObject, "center");
            var size = ParseOptionalVector3Parameter(paramsObject, "size");

            if (!enabled.HasValue &&
                !isTrigger.HasValue &&
                !contactOffset.HasValue &&
                !center.HasValue &&
                !size.HasValue)
            {
                throw new ArgumentException(
                    "At least one collider setting must be provided: enabled, isTrigger, contactOffset, center, or size.");
            }

            if (contactOffset.HasValue && contactOffset.Value < 0f)
            {
                throw new ArgumentException("Parameter 'contactOffset' must be greater than or equal to 0.");
            }

            if (size.HasValue &&
                (size.Value.x <= 0f || size.Value.y <= 0f || size.Value.z <= 0f))
            {
                throw new ArgumentException("Parameter 'size' must contain positive values for all BoxCollider axes.");
            }

            var boxCollider = collider as BoxCollider;
            if ((center.HasValue || size.HasValue) && boxCollider == null)
            {
                throw new ArgumentException("Parameters 'center' and 'size' are only supported for BoxCollider in the MVP.");
            }

            Undo.RecordObject(collider, "UnityMCP Set Collider Settings");

            if (enabled.HasValue)
            {
                collider.enabled = enabled.Value;
            }

            if (isTrigger.HasValue)
            {
                collider.isTrigger = isTrigger.Value;
            }

            if (contactOffset.HasValue)
            {
                collider.contactOffset = contactOffset.Value;
            }

            if (boxCollider != null)
            {
                if (center.HasValue)
                {
                    boxCollider.center = center.Value;
                }

                if (size.HasValue)
                {
                    boxCollider.size = size.Value;
                }
            }

            EditorUtility.SetDirty(collider);

            var result = new
            {
                target = CreateObjectSummary(collider.gameObject),
                component = CreateComponentSummary(collider),
                settings = CreateColliderSettingsSnapshot(collider),
                applied = new
                {
                    enabled = enabled.HasValue,
                    isTrigger = isTrigger.HasValue,
                    contactOffset = contactOffset.HasValue,
                    center = center.HasValue,
                    size = size.HasValue
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        // ── BoxCollider Methods ────────────────────────────────────────────────

        internal static string BuildGetBoxColliderSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "boxCollider.getSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var collider = ResolveComponentOfTypeTarget<BoxCollider>(resolvedObject, "instanceId", "BoxCollider");

            var result = new
            {
                target = CreateObjectSummary(collider.gameObject),
                component = CreateComponentSummary(collider),
                settings = CreateBoxColliderSettingsSnapshot(collider)
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildSetBoxColliderSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "boxCollider.setSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var collider = ResolveComponentOfTypeTarget<BoxCollider>(resolvedObject, "instanceId", "BoxCollider");

            var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
            var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
            var contactOffset = ParseOptionalFloatParameter(paramsObject, "contactOffset");
            var center = ParseOptionalVector3Parameter(paramsObject, "center");
            var size = ParseOptionalVector3Parameter(paramsObject, "size");

            if (!enabled.HasValue && !isTrigger.HasValue && !contactOffset.HasValue && !center.HasValue && !size.HasValue)
            {
                throw new ArgumentException("At least one BoxCollider setting must be provided: enabled, isTrigger, contactOffset, center, or size.");
            }

            ValidateCommonColliderSettingValues(contactOffset);
            ValidatePositiveVector3(size, "size", "Parameter 'size' must contain positive values for all BoxCollider axes.");

            Undo.RecordObject(collider, "UnityMCP Set BoxCollider Settings");
            ApplyCommonColliderSettings(collider, enabled, isTrigger, contactOffset);

            if (center.HasValue)
            {
                collider.center = center.Value;
            }

            if (size.HasValue)
            {
                collider.size = size.Value;
            }

            EditorUtility.SetDirty(collider);

            var result = new
            {
                target = CreateObjectSummary(collider.gameObject),
                component = CreateComponentSummary(collider),
                settings = CreateBoxColliderSettingsSnapshot(collider),
                applied = new
                {
                    enabled = enabled.HasValue,
                    isTrigger = isTrigger.HasValue,
                    contactOffset = contactOffset.HasValue,
                    center = center.HasValue,
                    size = size.HasValue
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        // ── SphereCollider Methods ─────────────────────────────────────────────

        internal static string BuildGetSphereColliderSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "sphereCollider.getSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var collider = ResolveComponentOfTypeTarget<SphereCollider>(resolvedObject, "instanceId", "SphereCollider");

            var result = new
            {
                target = CreateObjectSummary(collider.gameObject),
                component = CreateComponentSummary(collider),
                settings = CreateSphereColliderSettingsSnapshot(collider)
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildSetSphereColliderSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "sphereCollider.setSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var collider = ResolveComponentOfTypeTarget<SphereCollider>(resolvedObject, "instanceId", "SphereCollider");

            var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
            var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
            var contactOffset = ParseOptionalFloatParameter(paramsObject, "contactOffset");
            var center = ParseOptionalVector3Parameter(paramsObject, "center");
            var radius = ParseOptionalFloatParameter(paramsObject, "radius");

            if (!enabled.HasValue && !isTrigger.HasValue && !contactOffset.HasValue && !center.HasValue && !radius.HasValue)
            {
                throw new ArgumentException("At least one SphereCollider setting must be provided: enabled, isTrigger, contactOffset, center, or radius.");
            }

            ValidateCommonColliderSettingValues(contactOffset);
            if (radius.HasValue && radius.Value <= 0f)
            {
                throw new ArgumentException("Parameter 'radius' must be greater than 0.");
            }

            Undo.RecordObject(collider, "UnityMCP Set SphereCollider Settings");
            ApplyCommonColliderSettings(collider, enabled, isTrigger, contactOffset);

            if (center.HasValue)
            {
                collider.center = center.Value;
            }

            if (radius.HasValue)
            {
                collider.radius = radius.Value;
            }

            EditorUtility.SetDirty(collider);

            var result = new
            {
                target = CreateObjectSummary(collider.gameObject),
                component = CreateComponentSummary(collider),
                settings = CreateSphereColliderSettingsSnapshot(collider),
                applied = new
                {
                    enabled = enabled.HasValue,
                    isTrigger = isTrigger.HasValue,
                    contactOffset = contactOffset.HasValue,
                    center = center.HasValue,
                    radius = radius.HasValue
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        // ── CapsuleCollider Methods ────────────────────────────────────────────

        internal static string BuildGetCapsuleColliderSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "capsuleCollider.getSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var collider = ResolveComponentOfTypeTarget<CapsuleCollider>(resolvedObject, "instanceId", "CapsuleCollider");

            var result = new
            {
                target = CreateObjectSummary(collider.gameObject),
                component = CreateComponentSummary(collider),
                settings = CreateCapsuleColliderSettingsSnapshot(collider)
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildSetCapsuleColliderSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "capsuleCollider.setSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var collider = ResolveComponentOfTypeTarget<CapsuleCollider>(resolvedObject, "instanceId", "CapsuleCollider");

            var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
            var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
            var contactOffset = ParseOptionalFloatParameter(paramsObject, "contactOffset");
            var center = ParseOptionalVector3Parameter(paramsObject, "center");
            var radius = ParseOptionalFloatParameter(paramsObject, "radius");
            var height = ParseOptionalFloatParameter(paramsObject, "height");
            var direction = ParseOptionalCapsuleDirectionParameter(paramsObject, "direction");

            if (!enabled.HasValue &&
                !isTrigger.HasValue &&
                !contactOffset.HasValue &&
                !center.HasValue &&
                !radius.HasValue &&
                !height.HasValue &&
                !direction.HasValue)
            {
                throw new ArgumentException("At least one CapsuleCollider setting must be provided: enabled, isTrigger, contactOffset, center, radius, height, or direction.");
            }

            ValidateCommonColliderSettingValues(contactOffset);
            if (radius.HasValue && radius.Value <= 0f)
            {
                throw new ArgumentException("Parameter 'radius' must be greater than 0.");
            }

            if (height.HasValue && height.Value <= 0f)
            {
                throw new ArgumentException("Parameter 'height' must be greater than 0.");
            }

            if (direction.HasValue && !IsValidCapsuleDirection(direction.Value))
            {
                throw new ArgumentException("Parameter 'direction' must be a valid CapsuleDirection value (X, Y, Z or 0, 1, 2).");
            }

            Undo.RecordObject(collider, "UnityMCP Set CapsuleCollider Settings");
            ApplyCommonColliderSettings(collider, enabled, isTrigger, contactOffset);

            if (center.HasValue)
            {
                collider.center = center.Value;
            }

            if (radius.HasValue)
            {
                collider.radius = radius.Value;
            }

            if (height.HasValue)
            {
                collider.height = height.Value;
            }

            if (direction.HasValue)
            {
                collider.direction = direction.Value;
            }

            EditorUtility.SetDirty(collider);

            var result = new
            {
                target = CreateObjectSummary(collider.gameObject),
                component = CreateComponentSummary(collider),
                settings = CreateCapsuleColliderSettingsSnapshot(collider),
                applied = new
                {
                    enabled = enabled.HasValue,
                    isTrigger = isTrigger.HasValue,
                    contactOffset = contactOffset.HasValue,
                    center = center.HasValue,
                    radius = radius.HasValue,
                    height = height.HasValue,
                    direction = direction.HasValue
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        // ── MeshCollider Methods ───────────────────────────────────────────────

        internal static string BuildGetMeshColliderSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "meshCollider.getSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var collider = ResolveComponentOfTypeTarget<MeshCollider>(resolvedObject, "instanceId", "MeshCollider");

            var result = new
            {
                target = CreateObjectSummary(collider.gameObject),
                component = CreateComponentSummary(collider),
                settings = CreateMeshColliderSettingsSnapshot(collider)
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildSetMeshColliderSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "meshCollider.setSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var collider = ResolveComponentOfTypeTarget<MeshCollider>(resolvedObject, "instanceId", "MeshCollider");

            var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
            var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
            var contactOffset = ParseOptionalFloatParameter(paramsObject, "contactOffset");
            var convex = ParseOptionalBooleanValueParameter(paramsObject, "convex");
            var cookingOptions = ParseOptionalEnumParameter<MeshColliderCookingOptions>(paramsObject, "cookingOptions");

            if (!enabled.HasValue &&
                !isTrigger.HasValue &&
                !contactOffset.HasValue &&
                !convex.HasValue &&
                !cookingOptions.HasValue)
            {
                throw new ArgumentException("At least one MeshCollider setting must be provided: enabled, isTrigger, contactOffset, convex, or cookingOptions.");
            }

            ValidateCommonColliderSettingValues(contactOffset);

            var effectiveConvex = convex ?? collider.convex;
            var effectiveIsTrigger = isTrigger ?? collider.isTrigger;
            if (effectiveIsTrigger && !effectiveConvex)
            {
                throw new ArgumentException("MeshCollider triggers must be convex. Set 'convex' to true when enabling 'isTrigger'.");
            }

            Undo.RecordObject(collider, "UnityMCP Set MeshCollider Settings");
            ApplyCommonColliderSettings(collider, enabled, isTrigger, contactOffset);

            if (convex.HasValue)
            {
                collider.convex = convex.Value;
            }

            if (cookingOptions.HasValue)
            {
                collider.cookingOptions = cookingOptions.Value;
            }

            EditorUtility.SetDirty(collider);

            var result = new
            {
                target = CreateObjectSummary(collider.gameObject),
                component = CreateComponentSummary(collider),
                settings = CreateMeshColliderSettingsSnapshot(collider),
                applied = new
                {
                    enabled = enabled.HasValue,
                    isTrigger = isTrigger.HasValue,
                    contactOffset = contactOffset.HasValue,
                    convex = convex.HasValue,
                    cookingOptions = cookingOptions.HasValue
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        // ── CharacterController Methods ────────────────────────────────────────

        internal static string BuildGetCharacterControllerSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "characterController.getSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var (cc, ownerGo) = ResolveComponentFromInstanceId<CharacterController>(instanceId, "characterController.getSettings");

            var settings = new
            {
                height = cc.height,
                radius = cc.radius,
                center = CreateVector3Array(cc.center),
                slopeLimit = cc.slopeLimit,
                stepOffset = cc.stepOffset,
                skinWidth = cc.skinWidth,
                minMoveDistance = cc.minMoveDistance,
                enableOverlapRecovery = cc.enableOverlapRecovery,
                isGrounded = cc.isGrounded
            };

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                target = CreateObjectSummary(ownerGo),
                component = CreateObjectSummary(cc),
                settings
            });
        }

        internal static string BuildSetCharacterControllerSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "characterController.setSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var (cc, ownerGo) = ResolveComponentFromInstanceId<CharacterController>(instanceId, "characterController.setSettings");

            Undo.RecordObject(cc, "Set CharacterController Settings");
            var applied = new System.Collections.Generic.List<string>();

            if (TryGetFloat(paramsObject, "height", out var h))
            { cc.height = h; applied.Add("height"); }
            if (TryGetFloat(paramsObject, "radius", out var r))
            { cc.radius = r; applied.Add("radius"); }
            if (paramsObject.TryGetValue("center", out var ctr) && ctr is JArray ctrArr && ctrArr.Count == 3)
            { cc.center = new Vector3(ctrArr[0].Value<float>(), ctrArr[1].Value<float>(), ctrArr[2].Value<float>()); applied.Add("center"); }
            if (TryGetFloat(paramsObject, "slopeLimit", out var sl))
            { cc.slopeLimit = sl; applied.Add("slopeLimit"); }
            if (TryGetFloat(paramsObject, "stepOffset", out var so))
            { cc.stepOffset = so; applied.Add("stepOffset"); }
            if (TryGetFloat(paramsObject, "skinWidth", out var sw))
            { cc.skinWidth = sw; applied.Add("skinWidth"); }
            if (TryGetFloat(paramsObject, "minMoveDistance", out var mmd))
            { cc.minMoveDistance = mmd; applied.Add("minMoveDistance"); }
            if (paramsObject.TryGetValue("enableOverlapRecovery", out var eor) && eor.Type == JTokenType.Boolean)
            { cc.enableOverlapRecovery = eor.Value<bool>(); applied.Add("enableOverlapRecovery"); }

            EditorUtility.SetDirty(cc);
            return UnityMcpProtocol.CreateResult(idToken, new
            {
                target = CreateObjectSummary(ownerGo),
                component = CreateObjectSummary(cc),
                applied
            });
        }

        // ── Physics Methods ────────────────────────────────────────────────────

        internal static string BuildPhysicsRaycastResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "physics.raycast");

            if (!paramsObject.TryGetValue("origin", out var originToken))
                throw new ArgumentException("Parameter 'origin' is required.");
            var origin = ParseVector3Parameter(originToken, "origin");

            if (!paramsObject.TryGetValue("direction", out var directionToken))
                throw new ArgumentException("Parameter 'direction' is required.");
            var direction = ParseVector3Parameter(directionToken, "direction");

            var maxDistance = Mathf.Infinity;
            if (paramsObject.TryGetValue("maxDistance", out var maxDistToken) &&
                (maxDistToken.Type == JTokenType.Float || maxDistToken.Type == JTokenType.Integer))
            {
                maxDistance = maxDistToken.Value<float>();
            }

            var layerMask = Physics.DefaultRaycastLayers;
            if (paramsObject.TryGetValue("layerMask", out var layerMaskToken) &&
                layerMaskToken.Type == JTokenType.Integer)
            {
                layerMask = layerMaskToken.Value<int>();
            }

            object result;
            if (Physics.Raycast(origin, direction, out var hit, maxDistance, layerMask))
            {
                result = new
                {
                    hit = true,
                    point = CreateVector3Array(hit.point),
                    normal = CreateVector3Array(hit.normal),
                    distance = hit.distance,
                    gameObjectName = hit.collider.gameObject.name,
                    instanceId = hit.collider.gameObject.GetInstanceID()
                };
            }
            else
            {
                result = new
                {
                    hit = false,
                    point = (object?)null,
                    normal = (object?)null,
                    distance = (object?)null,
                    gameObjectName = (object?)null,
                    instanceId = (object?)null
                };
            }

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildPhysicsOverlapSphereResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "physics.overlapSphere");

            if (!paramsObject.TryGetValue("center", out var centerToken))
                throw new ArgumentException("Parameter 'center' is required.");
            var center = ParseVector3Parameter(centerToken, "center");

            if (!paramsObject.TryGetValue("radius", out var radiusToken) ||
                (radiusToken.Type != JTokenType.Float && radiusToken.Type != JTokenType.Integer))
                throw new ArgumentException("Parameter 'radius' is required and must be a number.");
            var radius = radiusToken.Value<float>();
            if (radius <= 0f)
                throw new ArgumentException("Parameter 'radius' must be greater than 0.");

            var layerMask = Physics.AllLayers;
            if (paramsObject.TryGetValue("layerMask", out var layerMaskToken) &&
                layerMaskToken.Type == JTokenType.Integer)
            {
                layerMask = layerMaskToken.Value<int>();
            }

            var colliders = Physics.OverlapSphere(center, radius, layerMask);
            var items = new object[colliders.Length];
            for (var i = 0; i < colliders.Length; i++)
            {
                items[i] = new
                {
                    gameObjectName = colliders[i].gameObject.name,
                    instanceId = colliders[i].gameObject.GetInstanceID()
                };
            }

            var result = new
            {
                count = colliders.Length,
                colliders = items
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        // ── Physics Helper Methods ─────────────────────────────────────────────

        private static void ValidateCommonColliderSettingValues(float? contactOffset)
        {
            if (contactOffset.HasValue && contactOffset.Value < 0f)
            {
                throw new ArgumentException("Parameter 'contactOffset' must be greater than or equal to 0.");
            }
        }

        private static void ValidatePositiveVector3(Vector3? value, string parameterName, string errorMessage)
        {
            if (!value.HasValue)
            {
                return;
            }

            var vector = value.Value;
            if (vector.x <= 0f || vector.y <= 0f || vector.z <= 0f)
            {
                throw new ArgumentException(errorMessage);
            }
        }

        private static void ApplyCommonColliderSettings(Collider collider, bool? enabled, bool? isTrigger, float? contactOffset)
        {
            if (enabled.HasValue)
            {
                collider.enabled = enabled.Value;
            }

            if (isTrigger.HasValue)
            {
                collider.isTrigger = isTrigger.Value;
            }

            if (contactOffset.HasValue)
            {
                collider.contactOffset = contactOffset.Value;
            }
        }

        private static bool IsValidCapsuleDirection(int value)
        {
            return value >= 0 && value <= 2;
        }

        #pragma warning disable CS0618
        private static object CreateRigidbodySettingsSnapshot(Rigidbody rigidbody)
        {
            return new
            {
                mass = rigidbody.mass,
                drag = rigidbody.drag,
                angularDrag = rigidbody.angularDrag,
                useGravity = rigidbody.useGravity,
                isKinematic = rigidbody.isKinematic,
                detectCollisions = rigidbody.detectCollisions,
                constraints = CreateEnumSummary(rigidbody.constraints),
                interpolation = CreateEnumSummary(rigidbody.interpolation),
                collisionDetectionMode = CreateEnumSummary(rigidbody.collisionDetectionMode)
            };
        }
        #pragma warning restore CS0618

        private static object CreateColliderSettingsSnapshot(Collider collider)
        {
            var boxCollider = collider as BoxCollider;
            var sharedMaterial = collider.sharedMaterial;
            var attachedRigidbody = collider.attachedRigidbody;

            object? subtype = null;
            if (boxCollider != null)
            {
                subtype = new
                {
                    kind = "BoxCollider",
                    center = CreateVector3Array(boxCollider.center),
                    size = CreateVector3Array(boxCollider.size)
                };
            }

            return new
            {
                colliderType = collider.GetType().FullName,
                enabled = collider.enabled,
                isTrigger = collider.isTrigger,
                contactOffset = collider.contactOffset,
                boundsCenter = CreateVector3Array(collider.bounds.center),
                boundsSize = CreateVector3Array(collider.bounds.size),
                sharedMaterial = sharedMaterial != null ? CreateObjectSummary(sharedMaterial) : null,
                attachedRigidbody = attachedRigidbody != null ? CreateObjectSummary(attachedRigidbody) : null,
                subtype
            };
        }

        private static object CreateBoxColliderSettingsSnapshot(BoxCollider collider)
        {
            return new
            {
                colliderType = collider.GetType().FullName,
                enabled = collider.enabled,
                isTrigger = collider.isTrigger,
                contactOffset = collider.contactOffset,
                boundsCenter = CreateVector3Array(collider.bounds.center),
                boundsSize = CreateVector3Array(collider.bounds.size),
                sharedMaterial = collider.sharedMaterial != null ? CreateObjectSummary(collider.sharedMaterial) : null,
                attachedRigidbody = collider.attachedRigidbody != null ? CreateObjectSummary(collider.attachedRigidbody) : null,
                center = CreateVector3Array(collider.center),
                size = CreateVector3Array(collider.size)
            };
        }

        private static object CreateSphereColliderSettingsSnapshot(SphereCollider collider)
        {
            return new
            {
                colliderType = collider.GetType().FullName,
                enabled = collider.enabled,
                isTrigger = collider.isTrigger,
                contactOffset = collider.contactOffset,
                boundsCenter = CreateVector3Array(collider.bounds.center),
                boundsSize = CreateVector3Array(collider.bounds.size),
                sharedMaterial = collider.sharedMaterial != null ? CreateObjectSummary(collider.sharedMaterial) : null,
                attachedRigidbody = collider.attachedRigidbody != null ? CreateObjectSummary(collider.attachedRigidbody) : null,
                center = CreateVector3Array(collider.center),
                radius = collider.radius
            };
        }

        private static object CreateCapsuleColliderSettingsSnapshot(CapsuleCollider collider)
        {
            return new
            {
                colliderType = collider.GetType().FullName,
                enabled = collider.enabled,
                isTrigger = collider.isTrigger,
                contactOffset = collider.contactOffset,
                boundsCenter = CreateVector3Array(collider.bounds.center),
                boundsSize = CreateVector3Array(collider.bounds.size),
                sharedMaterial = collider.sharedMaterial != null ? CreateObjectSummary(collider.sharedMaterial) : null,
                attachedRigidbody = collider.attachedRigidbody != null ? CreateObjectSummary(collider.attachedRigidbody) : null,
                center = CreateVector3Array(collider.center),
                radius = collider.radius,
                height = collider.height,
                direction = CreateCapsuleDirectionSummary(collider.direction)
            };
        }

        private static object CreateMeshColliderSettingsSnapshot(MeshCollider collider)
        {
            return new
            {
                colliderType = collider.GetType().FullName,
                enabled = collider.enabled,
                isTrigger = collider.isTrigger,
                contactOffset = collider.contactOffset,
                boundsCenter = CreateVector3Array(collider.bounds.center),
                boundsSize = CreateVector3Array(collider.bounds.size),
                sharedMaterial = collider.sharedMaterial != null ? CreateObjectSummary(collider.sharedMaterial) : null,
                attachedRigidbody = collider.attachedRigidbody != null ? CreateObjectSummary(collider.attachedRigidbody) : null,
                convex = collider.convex,
                cookingOptions = CreateEnumSummary(collider.cookingOptions),
                sharedMesh = collider.sharedMesh != null ? CreateObjectSummary(collider.sharedMesh) : null
            };
        }

        private static object CreateCapsuleDirectionSummary(int direction)
        {
            var name = direction switch
            {
                0 => "X",
                1 => "Y",
                2 => "Z",
                _ => "Unknown"
            };

            return new
            {
                name,
                value = direction
            };
        }
    }
}