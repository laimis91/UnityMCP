#nullable enable

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using static UnityMcp.Editor.UnityMcpParameterHelpers;
using static UnityMcp.Editor.UnityMcpResolvers;
using static UnityMcp.Editor.UnityMcpSnapshotHelpers;

namespace UnityMcp.Editor
{

internal static class Physics2DHandler
{
    // Rigidbody2D Methods
    internal static string BuildGetRigidbody2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "rigidbody2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var rigidbody = ResolveComponentOfTypeTarget<Rigidbody2D>(resolvedObject, "instanceId", "Rigidbody2D");

        var result = new
        {
            target = CreateObjectSummary(rigidbody.gameObject),
            component = CreateComponentSummary(rigidbody),
            settings = CreateRigidbody2DSettingsSnapshot(rigidbody)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    internal static string BuildSetRigidbody2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "rigidbody2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var rigidbody = ResolveComponentOfTypeTarget<Rigidbody2D>(resolvedObject, "instanceId", "Rigidbody2D");

        var bodyType = ParseOptionalEnumParameter<RigidbodyType2D>(paramsObject, "bodyType");
        var simulated = ParseOptionalBooleanValueParameter(paramsObject, "simulated");
        var useAutoMass = ParseOptionalBooleanValueParameter(paramsObject, "useAutoMass");
        var mass = ParseOptionalFloatParameter(paramsObject, "mass");
        var gravityScale = ParseOptionalFloatParameter(paramsObject, "gravityScale");
        var constraints = ParseOptionalEnumParameter<RigidbodyConstraints2D>(paramsObject, "constraints");
        var interpolation = ParseOptionalEnumParameter<RigidbodyInterpolation2D>(paramsObject, "interpolation");
        var collisionDetectionMode = ParseOptionalEnumParameter<CollisionDetectionMode2D>(paramsObject, "collisionDetectionMode");
        var sleepMode = ParseOptionalEnumParameter<RigidbodySleepMode2D>(paramsObject, "sleepMode");

        if (!bodyType.HasValue &&
            !simulated.HasValue &&
            !useAutoMass.HasValue &&
            !mass.HasValue &&
            !gravityScale.HasValue &&
            !constraints.HasValue &&
            !interpolation.HasValue &&
            !collisionDetectionMode.HasValue &&
            !sleepMode.HasValue)
        {
            throw new ArgumentException("At least one Rigidbody2D setting must be provided: bodyType, simulated, useAutoMass, mass, gravityScale, constraints, interpolation, collisionDetectionMode, or sleepMode.");
        }

        if (mass.HasValue && mass.Value <= 0f)
        {
            throw new ArgumentException("Parameter 'mass' must be greater than 0.");
        }

        Undo.RecordObject(rigidbody, "UnityMCP Set Rigidbody2D Settings");

        if (bodyType.HasValue)
        {
            rigidbody.bodyType = bodyType.Value;
        }

        if (simulated.HasValue)
        {
            rigidbody.simulated = simulated.Value;
        }

        if (useAutoMass.HasValue)
        {
            rigidbody.useAutoMass = useAutoMass.Value;
        }

        if (mass.HasValue)
        {
            rigidbody.mass = mass.Value;
        }

        if (gravityScale.HasValue)
        {
            rigidbody.gravityScale = gravityScale.Value;
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

        if (sleepMode.HasValue)
        {
            rigidbody.sleepMode = sleepMode.Value;
        }

        EditorUtility.SetDirty(rigidbody);

        var result = new
        {
            target = CreateObjectSummary(rigidbody.gameObject),
            component = CreateComponentSummary(rigidbody),
            settings = CreateRigidbody2DSettingsSnapshot(rigidbody),
            applied = new
            {
                bodyType = bodyType.HasValue,
                simulated = simulated.HasValue,
                useAutoMass = useAutoMass.HasValue,
                mass = mass.HasValue,
                gravityScale = gravityScale.HasValue,
                constraints = constraints.HasValue,
                interpolation = interpolation.HasValue,
                collisionDetectionMode = collisionDetectionMode.HasValue,
                sleepMode = sleepMode.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    // Generic Collider2D Methods
    internal static string BuildGetCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "collider2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<Collider2D>(resolvedObject, "instanceId", "Collider2D");

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateCollider2DSettingsSnapshot(collider)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    internal static string BuildSetCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "collider2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<Collider2D>(resolvedObject, "instanceId", "Collider2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
        var usedByEffector = ParseOptionalBooleanValueParameter(paramsObject, "usedByEffector");
        var offset = ParseOptionalVector2Parameter(paramsObject, "offset");
        var density = ParseOptionalFloatParameter(paramsObject, "density");

        if (!enabled.HasValue &&
            !isTrigger.HasValue &&
            !usedByEffector.HasValue &&
            !offset.HasValue &&
            !density.HasValue)
        {
            throw new ArgumentException("At least one Collider2D setting must be provided: enabled, isTrigger, usedByEffector, offset, or density.");
        }

        ValidateCommonCollider2DSettingValues(density);

        Undo.RecordObject(collider, "UnityMCP Set Collider2D Settings");
        ApplyCommonCollider2DSettings(collider, enabled, isTrigger, usedByEffector, offset, density);
        EditorUtility.SetDirty(collider);

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateCollider2DSettingsSnapshot(collider),
            applied = new
            {
                enabled = enabled.HasValue,
                isTrigger = isTrigger.HasValue,
                usedByEffector = usedByEffector.HasValue,
                offset = offset.HasValue,
                density = density.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    // BoxCollider2D Methods
    internal static string BuildGetBoxCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "boxCollider2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<BoxCollider2D>(resolvedObject, "instanceId", "BoxCollider2D");

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateBoxCollider2DSettingsSnapshot(collider)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    internal static string BuildSetBoxCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "boxCollider2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<BoxCollider2D>(resolvedObject, "instanceId", "BoxCollider2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
        var usedByEffector = ParseOptionalBooleanValueParameter(paramsObject, "usedByEffector");
        var offset = ParseOptionalVector2Parameter(paramsObject, "offset");
        var density = ParseOptionalFloatParameter(paramsObject, "density");
        var size = ParseOptionalVector2Parameter(paramsObject, "size");
        var edgeRadius = ParseOptionalFloatParameter(paramsObject, "edgeRadius");

        if (!enabled.HasValue &&
            !isTrigger.HasValue &&
            !usedByEffector.HasValue &&
            !offset.HasValue &&
            !density.HasValue &&
            !size.HasValue &&
            !edgeRadius.HasValue)
        {
            throw new ArgumentException("At least one BoxCollider2D setting must be provided: enabled, isTrigger, usedByEffector, offset, density, size, or edgeRadius.");
        }

        ValidateCommonCollider2DSettingValues(density);
        ValidatePositiveVector2(size, "size", "Parameter 'size' must contain positive values for BoxCollider2D width and height.");
        if (edgeRadius.HasValue && edgeRadius.Value < 0f)
        {
            throw new ArgumentException("Parameter 'edgeRadius' must be greater than or equal to 0.");
        }

        Undo.RecordObject(collider, "UnityMCP Set BoxCollider2D Settings");
        ApplyCommonCollider2DSettings(collider, enabled, isTrigger, usedByEffector, offset, density);

        if (size.HasValue)
        {
            collider.size = size.Value;
        }

        if (edgeRadius.HasValue)
        {
            collider.edgeRadius = edgeRadius.Value;
        }

        EditorUtility.SetDirty(collider);

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateBoxCollider2DSettingsSnapshot(collider),
            applied = new
            {
                enabled = enabled.HasValue,
                isTrigger = isTrigger.HasValue,
                usedByEffector = usedByEffector.HasValue,
                offset = offset.HasValue,
                density = density.HasValue,
                size = size.HasValue,
                edgeRadius = edgeRadius.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    // CircleCollider2D Methods (used by both circleCollider2D and sphereCollider2D)
    internal static string BuildGetCircleCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "circleCollider2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<CircleCollider2D>(resolvedObject, "instanceId", "CircleCollider2D");

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateCircleCollider2DSettingsSnapshot(collider)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    internal static string BuildSetCircleCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "circleCollider2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<CircleCollider2D>(resolvedObject, "instanceId", "CircleCollider2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
        var usedByEffector = ParseOptionalBooleanValueParameter(paramsObject, "usedByEffector");
        var offset = ParseOptionalVector2Parameter(paramsObject, "offset");
        var density = ParseOptionalFloatParameter(paramsObject, "density");
        var radius = ParseOptionalFloatParameter(paramsObject, "radius");

        if (!enabled.HasValue &&
            !isTrigger.HasValue &&
            !usedByEffector.HasValue &&
            !offset.HasValue &&
            !density.HasValue &&
            !radius.HasValue)
        {
            throw new ArgumentException("At least one CircleCollider2D setting must be provided: enabled, isTrigger, usedByEffector, offset, density, or radius.");
        }

        ValidateCommonCollider2DSettingValues(density);
        if (radius.HasValue && radius.Value <= 0f)
        {
            throw new ArgumentException("Parameter 'radius' must be greater than 0.");
        }

        Undo.RecordObject(collider, "UnityMCP Set CircleCollider2D Settings");
        ApplyCommonCollider2DSettings(collider, enabled, isTrigger, usedByEffector, offset, density);

        if (radius.HasValue)
        {
            collider.radius = radius.Value;
        }

        EditorUtility.SetDirty(collider);

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateCircleCollider2DSettingsSnapshot(collider),
            applied = new
            {
                enabled = enabled.HasValue,
                isTrigger = isTrigger.HasValue,
                usedByEffector = usedByEffector.HasValue,
                offset = offset.HasValue,
                density = density.HasValue,
                radius = radius.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    // CapsuleCollider2D Methods
    internal static string BuildGetCapsuleCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "capsuleCollider2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<CapsuleCollider2D>(resolvedObject, "instanceId", "CapsuleCollider2D");

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateCapsuleCollider2DSettingsSnapshot(collider)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    internal static string BuildSetCapsuleCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "capsuleCollider2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<CapsuleCollider2D>(resolvedObject, "instanceId", "CapsuleCollider2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
        var usedByEffector = ParseOptionalBooleanValueParameter(paramsObject, "usedByEffector");
        var offset = ParseOptionalVector2Parameter(paramsObject, "offset");
        var density = ParseOptionalFloatParameter(paramsObject, "density");
        var size = ParseOptionalVector2Parameter(paramsObject, "size");
        var direction = ParseOptionalEnumParameter<CapsuleDirection2D>(paramsObject, "direction");

        if (!enabled.HasValue &&
            !isTrigger.HasValue &&
            !usedByEffector.HasValue &&
            !offset.HasValue &&
            !density.HasValue &&
            !size.HasValue &&
            !direction.HasValue)
        {
            throw new ArgumentException("At least one CapsuleCollider2D setting must be provided: enabled, isTrigger, usedByEffector, offset, density, size, or direction.");
        }

        ValidateCommonCollider2DSettingValues(density);
        ValidatePositiveVector2(size, "size", "Parameter 'size' must contain positive values for CapsuleCollider2D width and height.");

        Undo.RecordObject(collider, "UnityMCP Set CapsuleCollider2D Settings");
        ApplyCommonCollider2DSettings(collider, enabled, isTrigger, usedByEffector, offset, density);

        if (size.HasValue)
        {
            collider.size = size.Value;
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
            settings = CreateCapsuleCollider2DSettingsSnapshot(collider),
            applied = new
            {
                enabled = enabled.HasValue,
                isTrigger = isTrigger.HasValue,
                usedByEffector = usedByEffector.HasValue,
                offset = offset.HasValue,
                density = density.HasValue,
                size = size.HasValue,
                direction = direction.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    // PolygonCollider2D Methods
    internal static string BuildGetPolygonCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "polygonCollider2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<PolygonCollider2D>(resolvedObject, "instanceId", "PolygonCollider2D");

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreatePolygonCollider2DSettingsSnapshot(collider)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    internal static string BuildSetPolygonCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "polygonCollider2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<PolygonCollider2D>(resolvedObject, "instanceId", "PolygonCollider2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
        var usedByEffector = ParseOptionalBooleanValueParameter(paramsObject, "usedByEffector");
        var offset = ParseOptionalVector2Parameter(paramsObject, "offset");
        var density = ParseOptionalFloatParameter(paramsObject, "density");

        if (!enabled.HasValue &&
            !isTrigger.HasValue &&
            !usedByEffector.HasValue &&
            !offset.HasValue &&
            !density.HasValue)
        {
            throw new ArgumentException("At least one PolygonCollider2D setting must be provided: enabled, isTrigger, usedByEffector, offset, or density.");
        }

        ValidateCommonCollider2DSettingValues(density);

        Undo.RecordObject(collider, "UnityMCP Set PolygonCollider2D Settings");
        ApplyCommonCollider2DSettings(collider, enabled, isTrigger, usedByEffector, offset, density);
        EditorUtility.SetDirty(collider);

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreatePolygonCollider2DSettingsSnapshot(collider),
            applied = new
            {
                enabled = enabled.HasValue,
                isTrigger = isTrigger.HasValue,
                usedByEffector = usedByEffector.HasValue,
                offset = offset.HasValue,
                density = density.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    // EdgeCollider2D Methods
    internal static string BuildGetEdgeCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "edgeCollider2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<EdgeCollider2D>(resolvedObject, "instanceId", "EdgeCollider2D");

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateEdgeCollider2DSettingsSnapshot(collider)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    internal static string BuildSetEdgeCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "edgeCollider2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<EdgeCollider2D>(resolvedObject, "instanceId", "EdgeCollider2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
        var usedByEffector = ParseOptionalBooleanValueParameter(paramsObject, "usedByEffector");
        var offset = ParseOptionalVector2Parameter(paramsObject, "offset");
        var density = ParseOptionalFloatParameter(paramsObject, "density");
        var edgeRadius = ParseOptionalFloatParameter(paramsObject, "edgeRadius");

        if (!enabled.HasValue &&
            !isTrigger.HasValue &&
            !usedByEffector.HasValue &&
            !offset.HasValue &&
            !density.HasValue &&
            !edgeRadius.HasValue)
        {
            throw new ArgumentException("At least one EdgeCollider2D setting must be provided: enabled, isTrigger, usedByEffector, offset, density, or edgeRadius.");
        }

        ValidateCommonCollider2DSettingValues(density);
        if (edgeRadius.HasValue && edgeRadius.Value < 0f)
        {
            throw new ArgumentException("Parameter 'edgeRadius' must be greater than or equal to 0.");
        }

        Undo.RecordObject(collider, "UnityMCP Set EdgeCollider2D Settings");
        ApplyCommonCollider2DSettings(collider, enabled, isTrigger, usedByEffector, offset, density);

        if (edgeRadius.HasValue)
        {
            collider.edgeRadius = edgeRadius.Value;
        }

        EditorUtility.SetDirty(collider);

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateEdgeCollider2DSettingsSnapshot(collider),
            applied = new
            {
                enabled = enabled.HasValue,
                isTrigger = isTrigger.HasValue,
                usedByEffector = usedByEffector.HasValue,
                offset = offset.HasValue,
                density = density.HasValue,
                edgeRadius = edgeRadius.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    // CompositeCollider2D Methods
    internal static string BuildGetCompositeCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "compositeCollider2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<CompositeCollider2D>(resolvedObject, "instanceId", "CompositeCollider2D");

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateCompositeCollider2DSettingsSnapshot(collider)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    internal static string BuildSetCompositeCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "compositeCollider2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<CompositeCollider2D>(resolvedObject, "instanceId", "CompositeCollider2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
        var usedByEffector = ParseOptionalBooleanValueParameter(paramsObject, "usedByEffector");
        var offset = ParseOptionalVector2Parameter(paramsObject, "offset");
        var density = ParseOptionalFloatParameter(paramsObject, "density");
        var geometryType = ParseOptionalEnumParameter<CompositeCollider2D.GeometryType>(paramsObject, "geometryType");
        var generationType = ParseOptionalEnumParameter<CompositeCollider2D.GenerationType>(paramsObject, "generationType");
        var vertexDistance = ParseOptionalFloatParameter(paramsObject, "vertexDistance");
        var edgeRadius = ParseOptionalFloatParameter(paramsObject, "edgeRadius");

        if (!enabled.HasValue &&
            !isTrigger.HasValue &&
            !usedByEffector.HasValue &&
            !offset.HasValue &&
            !density.HasValue &&
            !geometryType.HasValue &&
            !generationType.HasValue &&
            !vertexDistance.HasValue &&
            !edgeRadius.HasValue)
        {
            throw new ArgumentException("At least one CompositeCollider2D setting must be provided: enabled, isTrigger, usedByEffector, offset, density, geometryType, generationType, vertexDistance, or edgeRadius.");
        }

        ValidateCommonCollider2DSettingValues(density);
        if (vertexDistance.HasValue && vertexDistance.Value < 0f)
        {
            throw new ArgumentException("Parameter 'vertexDistance' must be greater than or equal to 0.");
        }
        if (edgeRadius.HasValue && edgeRadius.Value < 0f)
        {
            throw new ArgumentException("Parameter 'edgeRadius' must be greater than or equal to 0.");
        }

        Undo.RecordObject(collider, "UnityMCP Set CompositeCollider2D Settings");
        ApplyCommonCollider2DSettings(collider, enabled, isTrigger, usedByEffector, offset, density);

        if (geometryType.HasValue)
        {
            collider.geometryType = geometryType.Value;
        }

        if (generationType.HasValue)
        {
            collider.generationType = generationType.Value;
        }

        if (vertexDistance.HasValue)
        {
            collider.vertexDistance = vertexDistance.Value;
        }

        if (edgeRadius.HasValue)
        {
            collider.edgeRadius = edgeRadius.Value;
        }

        EditorUtility.SetDirty(collider);

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateCompositeCollider2DSettingsSnapshot(collider),
            applied = new
            {
                enabled = enabled.HasValue,
                isTrigger = isTrigger.HasValue,
                usedByEffector = usedByEffector.HasValue,
                offset = offset.HasValue,
                density = density.HasValue,
                geometryType = geometryType.HasValue,
                generationType = generationType.HasValue,
                vertexDistance = vertexDistance.HasValue,
                edgeRadius = edgeRadius.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    // Physics2D Methods
    internal static string BuildGetPhysics2DSettingsResponse(JToken idToken)
    {
        var gravity = Physics2D.gravity;

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            gravity = new[] { gravity.x, gravity.y },
            defaultContactOffset = Physics2D.defaultContactOffset,
            velocityIterations = Physics2D.velocityIterations,
            positionIterations = Physics2D.positionIterations,
            bounceThreshold = Physics2D.bounceThreshold,
            maxLinearCorrection = Physics2D.maxLinearCorrection,
            maxAngularCorrection = Physics2D.maxAngularCorrection,
            maxTranslationSpeed = Physics2D.maxTranslationSpeed,
            maxRotationSpeed = Physics2D.maxRotationSpeed,
            baumgarteScale = Physics2D.baumgarteScale,
            baumgarteTOIScale = Physics2D.baumgarteTOIScale,
            timeToSleep = Physics2D.timeToSleep,
            linearSleepTolerance = Physics2D.linearSleepTolerance,
            angularSleepTolerance = Physics2D.angularSleepTolerance,
            autoSimulation = Physics2D.simulationMode == SimulationMode2D.FixedUpdate,
            autoSyncTransforms = true, // Physics2D.SyncTransforms() is now a method call
            callbacksOnDisable = Physics2D.callbacksOnDisable,
            reuseCollisionCallbacks = Physics2D.reuseCollisionCallbacks
        });
    }

    internal static string BuildSetPhysics2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "physics2D.setSettings");
        var updated = new List<string>();

        if (paramsObject.TryGetValue("gravity", out var gravityToken) && gravityToken.Type == JTokenType.Array)
        {
            var gravityArray = ParseFloatArrayToken(gravityToken, "gravity", 2);
            Physics2D.gravity = new Vector2(gravityArray[0], gravityArray[1]);
            updated.Add("gravity");
        }

        if (paramsObject.TryGetValue("defaultContactOffset", out var contactOffsetToken) && contactOffsetToken.Type == JTokenType.Float)
        {
            var value = contactOffsetToken.Value<float>();
            if (value >= 0)
            {
                Physics2D.defaultContactOffset = value;
                updated.Add("defaultContactOffset");
            }
        }

        if (paramsObject.TryGetValue("velocityIterations", out var velIterToken) && velIterToken.Type == JTokenType.Integer)
        {
            var value = velIterToken.Value<int>();
            if (value >= 1)
            {
                Physics2D.velocityIterations = value;
                updated.Add("velocityIterations");
            }
        }

        if (paramsObject.TryGetValue("positionIterations", out var posIterToken) && posIterToken.Type == JTokenType.Integer)
        {
            var value = posIterToken.Value<int>();
            if (value >= 1)
            {
                Physics2D.positionIterations = value;
                updated.Add("positionIterations");
            }
        }

        if (paramsObject.TryGetValue("velocityThreshold", out var velThresholdToken) && velThresholdToken.Type == JTokenType.Float)
        {
            var value = velThresholdToken.Value<float>();
            if (value >= 0)
            {
                Physics2D.bounceThreshold = value;
                updated.Add("bounceThreshold");
            }
        }

        if (paramsObject.TryGetValue("maxLinearCorrection", out var maxLinearToken) && maxLinearToken.Type == JTokenType.Float)
        {
            var value = maxLinearToken.Value<float>();
            if (value >= 0)
            {
                Physics2D.maxLinearCorrection = value;
                updated.Add("maxLinearCorrection");
            }
        }

        if (paramsObject.TryGetValue("maxAngularCorrection", out var maxAngularToken) && maxAngularToken.Type == JTokenType.Float)
        {
            var value = maxAngularToken.Value<float>();
            if (value >= 0)
            {
                Physics2D.maxAngularCorrection = value;
                updated.Add("maxAngularCorrection");
            }
        }

        if (paramsObject.TryGetValue("maxTranslationSpeed", out var maxTransToken) && maxTransToken.Type == JTokenType.Float)
        {
            var value = maxTransToken.Value<float>();
            if (value >= 0)
            {
                Physics2D.maxTranslationSpeed = value;
                updated.Add("maxTranslationSpeed");
            }
        }

        if (paramsObject.TryGetValue("maxRotationSpeed", out var maxRotToken) && maxRotToken.Type == JTokenType.Float)
        {
            var value = maxRotToken.Value<float>();
            if (value >= 0)
            {
                Physics2D.maxRotationSpeed = value;
                updated.Add("maxRotationSpeed");
            }
        }

        if (paramsObject.TryGetValue("autoSimulation", out var autoSimToken) && autoSimToken.Type == JTokenType.Boolean)
        {
            Physics2D.simulationMode = autoSimToken.Value<bool>() ? SimulationMode2D.FixedUpdate : SimulationMode2D.Script;
            updated.Add("autoSimulation");
        }

        if (paramsObject.TryGetValue("autoSyncTransforms", out var autoSyncToken) && autoSyncToken.Type == JTokenType.Boolean)
        {
            if (autoSyncToken.Value<bool>()) Physics2D.SyncTransforms(); // Call sync method if true
            updated.Add("autoSyncTransforms");
        }

        if (paramsObject.TryGetValue("reuseCollisionCallbacks", out var reuseToken) && reuseToken.Type == JTokenType.Boolean)
        {
            Physics2D.reuseCollisionCallbacks = reuseToken.Value<bool>();
            updated.Add("reuseCollisionCallbacks");
        }

        if (updated.Count > 0)
        {
            AssetDatabase.SaveAssets();
        }

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            updated = updated.ToArray(),
            count = updated.Count
        });
    }

    // 2D Physics-specific Helper Methods
    private static void ValidateCommonCollider2DSettingValues(float? density)
    {
        if (density.HasValue && density.Value < 0f)
        {
            throw new ArgumentException("Parameter 'density' must be greater than or equal to 0.");
        }
    }

    private static void ValidatePositiveVector2(Vector2? value, string parameterName, string errorMessage)
    {
        if (!value.HasValue)
        {
            return;
        }

        var vector = value.Value;
        if (vector.x <= 0f || vector.y <= 0f)
        {
            throw new ArgumentException(errorMessage);
        }
    }

    private static void ApplyCommonCollider2DSettings(
        Collider2D collider,
        bool? enabled,
        bool? isTrigger,
        bool? usedByEffector,
        Vector2? offset,
        float? density)
    {
        if (enabled.HasValue)
        {
            collider.enabled = enabled.Value;
        }

        if (isTrigger.HasValue)
        {
            collider.isTrigger = isTrigger.Value;
        }

        if (usedByEffector.HasValue)
        {
            collider.usedByEffector = usedByEffector.Value;
        }

        if (offset.HasValue)
        {
            collider.offset = offset.Value;
        }

        if (density.HasValue)
        {
            collider.density = density.Value;
        }
    }

    private static bool IsCollider2DUsedByComposite(Collider2D collider)
    {
        return collider.compositeOperation != Collider2D.CompositeOperation.None;
    }

    private static object CreateRigidbody2DSettingsSnapshot(Rigidbody2D rigidbody)
    {
        return new
        {
            bodyType = CreateEnumSummary(rigidbody.bodyType),
            simulated = rigidbody.simulated,
            useAutoMass = rigidbody.useAutoMass,
            mass = rigidbody.mass,
            gravityScale = rigidbody.gravityScale,
            constraints = CreateEnumSummary(rigidbody.constraints),
            interpolation = CreateEnumSummary(rigidbody.interpolation),
            collisionDetectionMode = CreateEnumSummary(rigidbody.collisionDetectionMode),
            sleepMode = CreateEnumSummary(rigidbody.sleepMode)
        };
    }

    private static object CreateCollider2DSettingsSnapshot(Collider2D collider)
    {
        var sharedMaterial = collider.sharedMaterial;
        var attachedRigidbody = collider.attachedRigidbody;

        object? subtype = null;
        if (collider is BoxCollider2D boxCollider)
        {
            subtype = new
            {
                kind = "BoxCollider2D",
                size = CreateVector2Array(boxCollider.size),
                edgeRadius = boxCollider.edgeRadius
            };
        }
        else if (collider is CircleCollider2D circleCollider)
        {
            subtype = new
            {
                kind = "CircleCollider2D",
                radius = circleCollider.radius
            };
        }
        else if (collider is CapsuleCollider2D capsuleCollider)
        {
            subtype = new
            {
                kind = "CapsuleCollider2D",
                size = CreateVector2Array(capsuleCollider.size),
                direction = CreateEnumSummary(capsuleCollider.direction)
            };
        }

        return new
        {
            colliderType = collider.GetType().FullName,
            enabled = collider.enabled,
            isTrigger = collider.isTrigger,
            usedByEffector = collider.usedByEffector,
            usedByComposite = IsCollider2DUsedByComposite(collider),
            compositeOperation = CreateEnumSummary(collider.compositeOperation),
            offset = CreateVector2Array(collider.offset),
            density = collider.density,
            shapeCount = collider.shapeCount,
            boundsCenter = CreateVector3Array(collider.bounds.center),
            boundsSize = CreateVector3Array(collider.bounds.size),
            sharedMaterial = sharedMaterial != null ? CreateObjectSummary(sharedMaterial) : null,
            attachedRigidbody = attachedRigidbody != null ? CreateObjectSummary(attachedRigidbody) : null,
            subtype
        };
    }

    private static object CreateBoxCollider2DSettingsSnapshot(BoxCollider2D collider)
    {
        return new
        {
            colliderType = collider.GetType().FullName,
            enabled = collider.enabled,
            isTrigger = collider.isTrigger,
            usedByEffector = collider.usedByEffector,
            usedByComposite = IsCollider2DUsedByComposite(collider),
            compositeOperation = CreateEnumSummary(collider.compositeOperation),
            offset = CreateVector2Array(collider.offset),
            density = collider.density,
            shapeCount = collider.shapeCount,
            boundsCenter = CreateVector3Array(collider.bounds.center),
            boundsSize = CreateVector3Array(collider.bounds.size),
            sharedMaterial = collider.sharedMaterial != null ? CreateObjectSummary(collider.sharedMaterial) : null,
            attachedRigidbody = collider.attachedRigidbody != null ? CreateObjectSummary(collider.attachedRigidbody) : null,
            size = CreateVector2Array(collider.size),
            edgeRadius = collider.edgeRadius
        };
    }

    private static object CreateCircleCollider2DSettingsSnapshot(CircleCollider2D collider)
    {
        return new
        {
            colliderType = collider.GetType().FullName,
            enabled = collider.enabled,
            isTrigger = collider.isTrigger,
            usedByEffector = collider.usedByEffector,
            usedByComposite = IsCollider2DUsedByComposite(collider),
            compositeOperation = CreateEnumSummary(collider.compositeOperation),
            offset = CreateVector2Array(collider.offset),
            density = collider.density,
            shapeCount = collider.shapeCount,
            boundsCenter = CreateVector3Array(collider.bounds.center),
            boundsSize = CreateVector3Array(collider.bounds.size),
            sharedMaterial = collider.sharedMaterial != null ? CreateObjectSummary(collider.sharedMaterial) : null,
            attachedRigidbody = collider.attachedRigidbody != null ? CreateObjectSummary(collider.attachedRigidbody) : null,
            radius = collider.radius
        };
    }

    private static object CreateCapsuleCollider2DSettingsSnapshot(CapsuleCollider2D collider)
    {
        return new
        {
            colliderType = collider.GetType().FullName,
            enabled = collider.enabled,
            isTrigger = collider.isTrigger,
            usedByEffector = collider.usedByEffector,
            usedByComposite = IsCollider2DUsedByComposite(collider),
            compositeOperation = CreateEnumSummary(collider.compositeOperation),
            offset = CreateVector2Array(collider.offset),
            density = collider.density,
            shapeCount = collider.shapeCount,
            boundsCenter = CreateVector3Array(collider.bounds.center),
            boundsSize = CreateVector3Array(collider.bounds.size),
            sharedMaterial = collider.sharedMaterial != null ? CreateObjectSummary(collider.sharedMaterial) : null,
            attachedRigidbody = collider.attachedRigidbody != null ? CreateObjectSummary(collider.attachedRigidbody) : null,
            size = CreateVector2Array(collider.size),
            direction = CreateEnumSummary(collider.direction)
        };
    }

    private static object CreatePolygonCollider2DSettingsSnapshot(PolygonCollider2D collider)
    {
        return new
        {
            colliderType = collider.GetType().FullName,
            enabled = collider.enabled,
            isTrigger = collider.isTrigger,
            usedByEffector = collider.usedByEffector,
            usedByComposite = IsCollider2DUsedByComposite(collider),
            compositeOperation = CreateEnumSummary(collider.compositeOperation),
            offset = CreateVector2Array(collider.offset),
            density = collider.density,
            shapeCount = collider.shapeCount,
            boundsCenter = CreateVector3Array(collider.bounds.center),
            boundsSize = CreateVector3Array(collider.bounds.size),
            sharedMaterial = collider.sharedMaterial != null ? CreateObjectSummary(collider.sharedMaterial) : null,
            attachedRigidbody = collider.attachedRigidbody != null ? CreateObjectSummary(collider.attachedRigidbody) : null,
            pathCount = collider.pathCount,
            pointCount = collider.points?.Length ?? 0
        };
    }

    private static object CreateEdgeCollider2DSettingsSnapshot(EdgeCollider2D collider)
    {
        return new
        {
            colliderType = collider.GetType().FullName,
            enabled = collider.enabled,
            isTrigger = collider.isTrigger,
            usedByEffector = collider.usedByEffector,
            usedByComposite = IsCollider2DUsedByComposite(collider),
            compositeOperation = CreateEnumSummary(collider.compositeOperation),
            offset = CreateVector2Array(collider.offset),
            density = collider.density,
            shapeCount = collider.shapeCount,
            boundsCenter = CreateVector3Array(collider.bounds.center),
            boundsSize = CreateVector3Array(collider.bounds.size),
            sharedMaterial = collider.sharedMaterial != null ? CreateObjectSummary(collider.sharedMaterial) : null,
            attachedRigidbody = collider.attachedRigidbody != null ? CreateObjectSummary(collider.attachedRigidbody) : null,
            edgeRadius = collider.edgeRadius,
            pointCount = collider.points?.Length ?? 0
        };
    }

    private static object CreateCompositeCollider2DSettingsSnapshot(CompositeCollider2D collider)
    {
        return new
        {
            colliderType = collider.GetType().FullName,
            enabled = collider.enabled,
            isTrigger = collider.isTrigger,
            usedByEffector = collider.usedByEffector,
            usedByComposite = IsCollider2DUsedByComposite(collider),
            compositeOperation = CreateEnumSummary(collider.compositeOperation),
            offset = CreateVector2Array(collider.offset),
            density = collider.density,
            shapeCount = collider.shapeCount,
            boundsCenter = CreateVector3Array(collider.bounds.center),
            boundsSize = CreateVector3Array(collider.bounds.size),
            sharedMaterial = collider.sharedMaterial != null ? CreateObjectSummary(collider.sharedMaterial) : null,
            attachedRigidbody = collider.attachedRigidbody != null ? CreateObjectSummary(collider.attachedRigidbody) : null,
            geometryType = CreateEnumSummary(collider.geometryType),
            generationType = CreateEnumSummary(collider.generationType),
            pathCount = collider.pathCount,
            pointCount = collider.pointCount
        };
    }
}

}