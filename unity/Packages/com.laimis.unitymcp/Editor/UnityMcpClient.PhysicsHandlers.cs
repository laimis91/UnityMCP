#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMcp.Editor
{

internal sealed partial class UnityMcpClient
{
    private static string BuildGetRigidbodySettingsResponse(JToken idToken, JObject root)
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

    private static string BuildSetRigidbodySettingsResponse(JToken idToken, JObject root)
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

    private static string BuildGetColliderSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildGetRigidbody2DSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildSetRigidbody2DSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildGetCollider2DSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildSetCollider2DSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildGetBoxCollider2DSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildSetBoxCollider2DSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildGetCircleCollider2DSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildSetCircleCollider2DSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildGetCapsuleCollider2DSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildSetCapsuleCollider2DSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildGetPolygonCollider2DSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildSetPolygonCollider2DSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildGetEdgeCollider2DSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildSetEdgeCollider2DSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildGetCompositeCollider2DSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildSetCompositeCollider2DSettingsResponse(JToken idToken, JObject root)
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

        if (!enabled.HasValue &&
            !isTrigger.HasValue &&
            !usedByEffector.HasValue &&
            !offset.HasValue &&
            !density.HasValue &&
            !geometryType.HasValue &&
            !generationType.HasValue)
        {
            throw new ArgumentException("At least one CompositeCollider2D setting must be provided: enabled, isTrigger, usedByEffector, offset, density, geometryType, or generationType.");
        }

        ValidateCommonCollider2DSettingValues(density);

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
                generationType = generationType.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetColliderSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildGetBoxColliderSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildSetBoxColliderSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildGetSphereColliderSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildSetSphereColliderSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildGetCapsuleColliderSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildSetCapsuleColliderSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildGetMeshColliderSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildSetMeshColliderSettingsResponse(JToken idToken, JObject root)
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

    private static string BuildGetHingeJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "hingeJoint2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<HingeJoint2D>(resolvedObject, "instanceId", "HingeJoint2D");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateHingeJoint2DSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetHingeJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "hingeJoint2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<HingeJoint2D>(resolvedObject, "instanceId", "HingeJoint2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var autoConfigureConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureConnectedAnchor");
        var anchor = ParseOptionalVector2Parameter(paramsObject, "anchor");
        var connectedAnchor = ParseOptionalVector2Parameter(paramsObject, "connectedAnchor");
        var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var connectedBodyInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "connectedBodyInstanceId");
        var connectedAnchorMode = ParseOptionalConnectedAnchorModeParameter(paramsObject, "connectedAnchorMode");
        var useMotor = ParseOptionalBooleanValueParameter(paramsObject, "useMotor");
        var motorSpeed = ParseOptionalFloatParameter(paramsObject, "motorSpeed");
        var maxMotorTorque = ParseOptionalFloatParameter(paramsObject, "maxMotorTorque");
        var useLimits = ParseOptionalBooleanValueParameter(paramsObject, "useLimits");
        var lowerAngle = ParseOptionalFloatParameter(paramsObject, "lowerAngle");
        var upperAngle = ParseOptionalFloatParameter(paramsObject, "upperAngle");
        var useConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "useConnectedAnchor");

        if (!enabled.HasValue &&
            !autoConfigureConnectedAnchor.HasValue &&
            !anchor.HasValue &&
            !connectedAnchor.HasValue &&
            !enableCollision.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !connectedBodyInstanceId.IsSpecified &&
            !connectedAnchorMode.HasValue &&
            !useMotor.HasValue &&
            !motorSpeed.HasValue &&
            !maxMotorTorque.HasValue &&
            !useLimits.HasValue &&
            !lowerAngle.HasValue &&
            !upperAngle.HasValue &&
            !useConnectedAnchor.HasValue)
        {
            throw new ArgumentException("At least one HingeJoint2D setting must be provided.");
        }

        ValidateCommonJoint2DSettingValues(breakForce, breakTorque);
        if (maxMotorTorque.HasValue && maxMotorTorque.Value < 0f)
        {
            throw new ArgumentException("Parameter 'maxMotorTorque' must be greater than or equal to 0.");
        }

        var helperRequiresConnectedAnchor = connectedAnchor.HasValue || connectedAnchorMode.HasValue;
        if (helperRequiresConnectedAnchor && useConnectedAnchor.HasValue && !useConnectedAnchor.Value)
        {
            throw new ArgumentException("Parameter 'useConnectedAnchor' cannot be false when 'connectedAnchor' or 'connectedAnchorMode' is provided.");
        }

        Undo.RecordObject(joint, "UnityMCP Set HingeJoint2D Settings");
        ApplyCommonJoint2DSettings(joint, enabled, autoConfigureConnectedAnchor, anchor, connectedAnchor, enableCollision, breakForce, breakTorque, connectedBodyInstanceId, connectedAnchorMode);

        if (helperRequiresConnectedAnchor)
        {
            joint.useConnectedAnchor = true;
        }
        else if (useConnectedAnchor.HasValue)
        {
            joint.useConnectedAnchor = useConnectedAnchor.Value;
        }

        if (useMotor.HasValue)
        {
            joint.useMotor = useMotor.Value;
        }

        if (motorSpeed.HasValue || maxMotorTorque.HasValue)
        {
            var motor = joint.motor;
            if (motorSpeed.HasValue)
            {
                motor.motorSpeed = motorSpeed.Value;
            }

            if (maxMotorTorque.HasValue)
            {
                motor.maxMotorTorque = maxMotorTorque.Value;
            }

            joint.motor = motor;
        }

        if (useLimits.HasValue)
        {
            joint.useLimits = useLimits.Value;
        }

        if (lowerAngle.HasValue || upperAngle.HasValue)
        {
            var limits = joint.limits;
            if (lowerAngle.HasValue)
            {
                limits.min = lowerAngle.Value;
            }

            if (upperAngle.HasValue)
            {
                limits.max = upperAngle.Value;
            }

            joint.limits = limits;
        }

        EditorUtility.SetDirty(joint);
        var connectionState = CreateJoint2DAppliedConnectionState(joint, connectedAnchorMode);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateHingeJoint2DSettingsSnapshot(joint),
            applied = new
            {
                enabled = enabled.HasValue,
                autoConfigureConnectedAnchor = connectionState.AutoConfigureConnectedAnchor,
                anchor = anchor.HasValue,
                connectedAnchor = connectionState.ConnectedAnchor,
                connectedAnchorMode = connectionState.ConnectedAnchorMode,
                enableCollision = enableCollision.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                connectedBodyInstanceId = connectionState.ConnectedBodyInstanceId,
                useMotor = useMotor.HasValue,
                motorSpeed = motorSpeed.HasValue,
                maxMotorTorque = maxMotorTorque.HasValue,
                useLimits = useLimits.HasValue,
                lowerAngle = lowerAngle.HasValue,
                upperAngle = upperAngle.HasValue,
                useConnectedAnchor = joint.useConnectedAnchor
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetSpringJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "springJoint2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<SpringJoint2D>(resolvedObject, "instanceId", "SpringJoint2D");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateSpringJoint2DSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetSpringJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "springJoint2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<SpringJoint2D>(resolvedObject, "instanceId", "SpringJoint2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var autoConfigureConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureConnectedAnchor");
        var anchor = ParseOptionalVector2Parameter(paramsObject, "anchor");
        var connectedAnchor = ParseOptionalVector2Parameter(paramsObject, "connectedAnchor");
        var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var connectedBodyInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "connectedBodyInstanceId");
        var connectedAnchorMode = ParseOptionalConnectedAnchorModeParameter(paramsObject, "connectedAnchorMode");
        var autoConfigureDistance = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureDistance");
        var distance = ParseOptionalFloatParameter(paramsObject, "distance");
        var dampingRatio = ParseOptionalFloatParameter(paramsObject, "dampingRatio");
        var frequency = ParseOptionalFloatParameter(paramsObject, "frequency");

        if (!enabled.HasValue &&
            !autoConfigureConnectedAnchor.HasValue &&
            !anchor.HasValue &&
            !connectedAnchor.HasValue &&
            !enableCollision.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !connectedBodyInstanceId.IsSpecified &&
            !connectedAnchorMode.HasValue &&
            !autoConfigureDistance.HasValue &&
            !distance.HasValue &&
            !dampingRatio.HasValue &&
            !frequency.HasValue)
        {
            throw new ArgumentException("At least one SpringJoint2D setting must be provided.");
        }

        ValidateCommonJoint2DSettingValues(breakForce, breakTorque);
        if (distance.HasValue && distance.Value < 0f)
        {
            throw new ArgumentException("Parameter 'distance' must be greater than or equal to 0.");
        }

        if (dampingRatio.HasValue && (dampingRatio.Value < 0f || dampingRatio.Value > 1f))
        {
            throw new ArgumentException("Parameter 'dampingRatio' must be between 0 and 1.");
        }

        if (frequency.HasValue && frequency.Value < 0f)
        {
            throw new ArgumentException("Parameter 'frequency' must be greater than or equal to 0.");
        }

        Undo.RecordObject(joint, "UnityMCP Set SpringJoint2D Settings");
        ApplyCommonJoint2DSettings(joint, enabled, autoConfigureConnectedAnchor, anchor, connectedAnchor, enableCollision, breakForce, breakTorque, connectedBodyInstanceId, connectedAnchorMode);

        if (autoConfigureDistance.HasValue)
        {
            joint.autoConfigureDistance = autoConfigureDistance.Value;
        }

        if (distance.HasValue)
        {
            joint.distance = distance.Value;
        }

        if (dampingRatio.HasValue)
        {
            joint.dampingRatio = dampingRatio.Value;
        }

        if (frequency.HasValue)
        {
            joint.frequency = frequency.Value;
        }

        EditorUtility.SetDirty(joint);
        var connectionState = CreateJoint2DAppliedConnectionState(joint, connectedAnchorMode);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateSpringJoint2DSettingsSnapshot(joint),
            applied = new
            {
                enabled = enabled.HasValue,
                autoConfigureConnectedAnchor = connectionState.AutoConfigureConnectedAnchor,
                anchor = anchor.HasValue,
                connectedAnchor = connectionState.ConnectedAnchor,
                connectedAnchorMode = connectionState.ConnectedAnchorMode,
                enableCollision = enableCollision.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                connectedBodyInstanceId = connectionState.ConnectedBodyInstanceId,
                autoConfigureDistance = autoConfigureDistance.HasValue,
                distance = distance.HasValue,
                dampingRatio = dampingRatio.HasValue,
                frequency = frequency.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetDistanceJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "distanceJoint2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<DistanceJoint2D>(resolvedObject, "instanceId", "DistanceJoint2D");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateDistanceJoint2DSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetDistanceJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "distanceJoint2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<DistanceJoint2D>(resolvedObject, "instanceId", "DistanceJoint2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var autoConfigureConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureConnectedAnchor");
        var anchor = ParseOptionalVector2Parameter(paramsObject, "anchor");
        var connectedAnchor = ParseOptionalVector2Parameter(paramsObject, "connectedAnchor");
        var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var connectedBodyInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "connectedBodyInstanceId");
        var connectedAnchorMode = ParseOptionalConnectedAnchorModeParameter(paramsObject, "connectedAnchorMode");
        var autoConfigureDistance = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureDistance");
        var distance = ParseOptionalFloatParameter(paramsObject, "distance");
        var maxDistanceOnly = ParseOptionalBooleanValueParameter(paramsObject, "maxDistanceOnly");

        if (!enabled.HasValue &&
            !autoConfigureConnectedAnchor.HasValue &&
            !anchor.HasValue &&
            !connectedAnchor.HasValue &&
            !enableCollision.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !connectedBodyInstanceId.IsSpecified &&
            !connectedAnchorMode.HasValue &&
            !autoConfigureDistance.HasValue &&
            !distance.HasValue &&
            !maxDistanceOnly.HasValue)
        {
            throw new ArgumentException("At least one DistanceJoint2D setting must be provided.");
        }

        ValidateCommonJoint2DSettingValues(breakForce, breakTorque);
        if (distance.HasValue && distance.Value < 0f)
        {
            throw new ArgumentException("Parameter 'distance' must be greater than or equal to 0.");
        }

        Undo.RecordObject(joint, "UnityMCP Set DistanceJoint2D Settings");
        ApplyCommonJoint2DSettings(joint, enabled, autoConfigureConnectedAnchor, anchor, connectedAnchor, enableCollision, breakForce, breakTorque, connectedBodyInstanceId, connectedAnchorMode);

        if (autoConfigureDistance.HasValue)
        {
            joint.autoConfigureDistance = autoConfigureDistance.Value;
        }

        if (distance.HasValue)
        {
            joint.distance = distance.Value;
        }

        if (maxDistanceOnly.HasValue)
        {
            joint.maxDistanceOnly = maxDistanceOnly.Value;
        }

        EditorUtility.SetDirty(joint);
        var connectionState = CreateJoint2DAppliedConnectionState(joint, connectedAnchorMode);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateDistanceJoint2DSettingsSnapshot(joint),
            applied = new
            {
                enabled = enabled.HasValue,
                autoConfigureConnectedAnchor = connectionState.AutoConfigureConnectedAnchor,
                anchor = anchor.HasValue,
                connectedAnchor = connectionState.ConnectedAnchor,
                connectedAnchorMode = connectionState.ConnectedAnchorMode,
                enableCollision = enableCollision.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                connectedBodyInstanceId = connectionState.ConnectedBodyInstanceId,
                autoConfigureDistance = autoConfigureDistance.HasValue,
                distance = distance.HasValue,
                maxDistanceOnly = maxDistanceOnly.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetFixedJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "fixedJoint2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<FixedJoint2D>(resolvedObject, "instanceId", "FixedJoint2D");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateFixedJoint2DSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetFixedJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "fixedJoint2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<FixedJoint2D>(resolvedObject, "instanceId", "FixedJoint2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var autoConfigureConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureConnectedAnchor");
        var anchor = ParseOptionalVector2Parameter(paramsObject, "anchor");
        var connectedAnchor = ParseOptionalVector2Parameter(paramsObject, "connectedAnchor");
        var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var connectedBodyInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "connectedBodyInstanceId");
        var connectedAnchorMode = ParseOptionalConnectedAnchorModeParameter(paramsObject, "connectedAnchorMode");
        var dampingRatio = ParseOptionalFloatParameter(paramsObject, "dampingRatio");
        var frequency = ParseOptionalFloatParameter(paramsObject, "frequency");

        if (!enabled.HasValue &&
            !autoConfigureConnectedAnchor.HasValue &&
            !anchor.HasValue &&
            !connectedAnchor.HasValue &&
            !enableCollision.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !connectedBodyInstanceId.IsSpecified &&
            !connectedAnchorMode.HasValue &&
            !dampingRatio.HasValue &&
            !frequency.HasValue)
        {
            throw new ArgumentException("At least one FixedJoint2D setting must be provided.");
        }

        ValidateCommonJoint2DSettingValues(breakForce, breakTorque);
        if (dampingRatio.HasValue && (dampingRatio.Value < 0f || dampingRatio.Value > 1f))
        {
            throw new ArgumentException("Parameter 'dampingRatio' must be between 0 and 1.");
        }

        if (frequency.HasValue && frequency.Value < 0f)
        {
            throw new ArgumentException("Parameter 'frequency' must be greater than or equal to 0.");
        }

        Undo.RecordObject(joint, "UnityMCP Set FixedJoint2D Settings");
        ApplyCommonJoint2DSettings(joint, enabled, autoConfigureConnectedAnchor, anchor, connectedAnchor, enableCollision, breakForce, breakTorque, connectedBodyInstanceId, connectedAnchorMode);

        if (dampingRatio.HasValue)
        {
            joint.dampingRatio = dampingRatio.Value;
        }

        if (frequency.HasValue)
        {
            joint.frequency = frequency.Value;
        }

        EditorUtility.SetDirty(joint);
        var connectionState = CreateJoint2DAppliedConnectionState(joint, connectedAnchorMode);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateFixedJoint2DSettingsSnapshot(joint),
            applied = new
            {
                enabled = enabled.HasValue,
                autoConfigureConnectedAnchor = connectionState.AutoConfigureConnectedAnchor,
                anchor = anchor.HasValue,
                connectedAnchor = connectionState.ConnectedAnchor,
                connectedAnchorMode = connectionState.ConnectedAnchorMode,
                enableCollision = enableCollision.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                connectedBodyInstanceId = connectionState.ConnectedBodyInstanceId,
                dampingRatio = dampingRatio.HasValue,
                frequency = frequency.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetSliderJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "sliderJoint2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<SliderJoint2D>(resolvedObject, "instanceId", "SliderJoint2D");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateSliderJoint2DSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetSliderJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "sliderJoint2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<SliderJoint2D>(resolvedObject, "instanceId", "SliderJoint2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var autoConfigureConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureConnectedAnchor");
        var anchor = ParseOptionalVector2Parameter(paramsObject, "anchor");
        var connectedAnchor = ParseOptionalVector2Parameter(paramsObject, "connectedAnchor");
        var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var connectedBodyInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "connectedBodyInstanceId");
        var connectedAnchorMode = ParseOptionalConnectedAnchorModeParameter(paramsObject, "connectedAnchorMode");
        var autoConfigureAngle = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureAngle");
        var angle = ParseOptionalFloatParameter(paramsObject, "angle");
        var useMotor = ParseOptionalBooleanValueParameter(paramsObject, "useMotor");
        var motorSpeed = ParseOptionalFloatParameter(paramsObject, "motorSpeed");
        var maxMotorTorque = ParseOptionalFloatParameter(paramsObject, "maxMotorTorque");
        var useLimits = ParseOptionalBooleanValueParameter(paramsObject, "useLimits");
        var lowerTranslation = ParseOptionalFloatParameter(paramsObject, "lowerTranslation");
        var upperTranslation = ParseOptionalFloatParameter(paramsObject, "upperTranslation");

        if (!enabled.HasValue &&
            !autoConfigureConnectedAnchor.HasValue &&
            !anchor.HasValue &&
            !connectedAnchor.HasValue &&
            !enableCollision.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !connectedBodyInstanceId.IsSpecified &&
            !connectedAnchorMode.HasValue &&
            !autoConfigureAngle.HasValue &&
            !angle.HasValue &&
            !useMotor.HasValue &&
            !motorSpeed.HasValue &&
            !maxMotorTorque.HasValue &&
            !useLimits.HasValue &&
            !lowerTranslation.HasValue &&
            !upperTranslation.HasValue)
        {
            throw new ArgumentException("At least one SliderJoint2D setting must be provided.");
        }

        ValidateCommonJoint2DSettingValues(breakForce, breakTorque);
        if (maxMotorTorque.HasValue && maxMotorTorque.Value < 0f)
        {
            throw new ArgumentException("Parameter 'maxMotorTorque' must be greater than or equal to 0.");
        }

        Undo.RecordObject(joint, "UnityMCP Set SliderJoint2D Settings");
        ApplyCommonJoint2DSettings(joint, enabled, autoConfigureConnectedAnchor, anchor, connectedAnchor, enableCollision, breakForce, breakTorque, connectedBodyInstanceId, connectedAnchorMode);

        if (autoConfigureAngle.HasValue)
        {
            joint.autoConfigureAngle = autoConfigureAngle.Value;
        }

        if (angle.HasValue)
        {
            joint.angle = angle.Value;
        }

        if (useMotor.HasValue)
        {
            joint.useMotor = useMotor.Value;
        }

        if (motorSpeed.HasValue || maxMotorTorque.HasValue)
        {
            var motor = joint.motor;
            if (motorSpeed.HasValue)
            {
                motor.motorSpeed = motorSpeed.Value;
            }

            if (maxMotorTorque.HasValue)
            {
                motor.maxMotorTorque = maxMotorTorque.Value;
            }

            joint.motor = motor;
        }

        if (useLimits.HasValue)
        {
            joint.useLimits = useLimits.Value;
        }

        if (lowerTranslation.HasValue || upperTranslation.HasValue)
        {
            var limits = joint.limits;
            if (lowerTranslation.HasValue)
            {
                limits.min = lowerTranslation.Value;
            }

            if (upperTranslation.HasValue)
            {
                limits.max = upperTranslation.Value;
            }

            joint.limits = limits;
        }

        EditorUtility.SetDirty(joint);
        var connectionState = CreateJoint2DAppliedConnectionState(joint, connectedAnchorMode);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateSliderJoint2DSettingsSnapshot(joint),
            applied = new
            {
                enabled = enabled.HasValue,
                autoConfigureConnectedAnchor = connectionState.AutoConfigureConnectedAnchor,
                anchor = anchor.HasValue,
                connectedAnchor = connectionState.ConnectedAnchor,
                connectedAnchorMode = connectionState.ConnectedAnchorMode,
                enableCollision = enableCollision.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                connectedBodyInstanceId = connectionState.ConnectedBodyInstanceId,
                autoConfigureAngle = autoConfigureAngle.HasValue,
                angle = angle.HasValue,
                useMotor = useMotor.HasValue,
                motorSpeed = motorSpeed.HasValue,
                maxMotorTorque = maxMotorTorque.HasValue,
                useLimits = useLimits.HasValue,
                lowerTranslation = lowerTranslation.HasValue,
                upperTranslation = upperTranslation.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetWheelJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "wheelJoint2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<WheelJoint2D>(resolvedObject, "instanceId", "WheelJoint2D");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateWheelJoint2DSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetWheelJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "wheelJoint2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<WheelJoint2D>(resolvedObject, "instanceId", "WheelJoint2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var autoConfigureConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureConnectedAnchor");
        var anchor = ParseOptionalVector2Parameter(paramsObject, "anchor");
        var connectedAnchor = ParseOptionalVector2Parameter(paramsObject, "connectedAnchor");
        var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var connectedBodyInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "connectedBodyInstanceId");
        var connectedAnchorMode = ParseOptionalConnectedAnchorModeParameter(paramsObject, "connectedAnchorMode");
        var useMotor = ParseOptionalBooleanValueParameter(paramsObject, "useMotor");
        var motorSpeed = ParseOptionalFloatParameter(paramsObject, "motorSpeed");
        var maxMotorTorque = ParseOptionalFloatParameter(paramsObject, "maxMotorTorque");
        var suspensionDampingRatio = ParseOptionalFloatParameter(paramsObject, "suspensionDampingRatio");
        var suspensionFrequency = ParseOptionalFloatParameter(paramsObject, "suspensionFrequency");
        var suspensionAngle = ParseOptionalFloatParameter(paramsObject, "suspensionAngle");

        if (!enabled.HasValue &&
            !autoConfigureConnectedAnchor.HasValue &&
            !anchor.HasValue &&
            !connectedAnchor.HasValue &&
            !enableCollision.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !connectedBodyInstanceId.IsSpecified &&
            !connectedAnchorMode.HasValue &&
            !useMotor.HasValue &&
            !motorSpeed.HasValue &&
            !maxMotorTorque.HasValue &&
            !suspensionDampingRatio.HasValue &&
            !suspensionFrequency.HasValue &&
            !suspensionAngle.HasValue)
        {
            throw new ArgumentException("At least one WheelJoint2D setting must be provided.");
        }

        ValidateCommonJoint2DSettingValues(breakForce, breakTorque);
        if (maxMotorTorque.HasValue && maxMotorTorque.Value < 0f)
        {
            throw new ArgumentException("Parameter 'maxMotorTorque' must be greater than or equal to 0.");
        }

        if (suspensionDampingRatio.HasValue && (suspensionDampingRatio.Value < 0f || suspensionDampingRatio.Value > 1f))
        {
            throw new ArgumentException("Parameter 'suspensionDampingRatio' must be between 0 and 1.");
        }

        if (suspensionFrequency.HasValue && suspensionFrequency.Value < 0f)
        {
            throw new ArgumentException("Parameter 'suspensionFrequency' must be greater than or equal to 0.");
        }

        Undo.RecordObject(joint, "UnityMCP Set WheelJoint2D Settings");
        ApplyCommonJoint2DSettings(joint, enabled, autoConfigureConnectedAnchor, anchor, connectedAnchor, enableCollision, breakForce, breakTorque, connectedBodyInstanceId, connectedAnchorMode);

        if (useMotor.HasValue)
        {
            joint.useMotor = useMotor.Value;
        }

        if (motorSpeed.HasValue || maxMotorTorque.HasValue)
        {
            var motor = joint.motor;
            if (motorSpeed.HasValue)
            {
                motor.motorSpeed = motorSpeed.Value;
            }

            if (maxMotorTorque.HasValue)
            {
                motor.maxMotorTorque = maxMotorTorque.Value;
            }

            joint.motor = motor;
        }

        if (suspensionDampingRatio.HasValue || suspensionFrequency.HasValue || suspensionAngle.HasValue)
        {
            var suspension = joint.suspension;
            if (suspensionDampingRatio.HasValue)
            {
                suspension.dampingRatio = suspensionDampingRatio.Value;
            }

            if (suspensionFrequency.HasValue)
            {
                suspension.frequency = suspensionFrequency.Value;
            }

            if (suspensionAngle.HasValue)
            {
                suspension.angle = suspensionAngle.Value;
            }

            joint.suspension = suspension;
        }

        EditorUtility.SetDirty(joint);
        var connectionState = CreateJoint2DAppliedConnectionState(joint, connectedAnchorMode);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateWheelJoint2DSettingsSnapshot(joint),
            applied = new
            {
                enabled = enabled.HasValue,
                autoConfigureConnectedAnchor = connectionState.AutoConfigureConnectedAnchor,
                anchor = anchor.HasValue,
                connectedAnchor = connectionState.ConnectedAnchor,
                connectedAnchorMode = connectionState.ConnectedAnchorMode,
                enableCollision = enableCollision.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                connectedBodyInstanceId = connectionState.ConnectedBodyInstanceId,
                useMotor = useMotor.HasValue,
                motorSpeed = motorSpeed.HasValue,
                maxMotorTorque = maxMotorTorque.HasValue,
                suspensionDampingRatio = suspensionDampingRatio.HasValue,
                suspensionFrequency = suspensionFrequency.HasValue,
                suspensionAngle = suspensionAngle.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetTargetJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "targetJoint2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<TargetJoint2D>(resolvedObject, "instanceId", "TargetJoint2D");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateTargetJoint2DSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetTargetJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "targetJoint2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<TargetJoint2D>(resolvedObject, "instanceId", "TargetJoint2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var anchor = ParseOptionalVector2Parameter(paramsObject, "anchor");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var autoConfigureTarget = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureTarget");
        var target = ParseOptionalVector2Parameter(paramsObject, "target");
        var maxForce = ParseOptionalFloatParameter(paramsObject, "maxForce");
        var dampingRatio = ParseOptionalFloatParameter(paramsObject, "dampingRatio");
        var frequency = ParseOptionalFloatParameter(paramsObject, "frequency");

        if (!enabled.HasValue &&
            !anchor.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !autoConfigureTarget.HasValue &&
            !target.HasValue &&
            !maxForce.HasValue &&
            !dampingRatio.HasValue &&
            !frequency.HasValue)
        {
            throw new ArgumentException("At least one TargetJoint2D setting must be provided.");
        }

        ValidateCommonJoint2DSettingValues(breakForce, breakTorque);
        if (maxForce.HasValue && maxForce.Value < 0f)
        {
            throw new ArgumentException("Parameter 'maxForce' must be greater than or equal to 0.");
        }

        if (dampingRatio.HasValue && (dampingRatio.Value < 0f || dampingRatio.Value > 1f))
        {
            throw new ArgumentException("Parameter 'dampingRatio' must be between 0 and 1.");
        }

        if (frequency.HasValue && frequency.Value < 0f)
        {
            throw new ArgumentException("Parameter 'frequency' must be greater than or equal to 0.");
        }

        Undo.RecordObject(joint, "UnityMCP Set TargetJoint2D Settings");

        if (enabled.HasValue)
        {
            joint.enabled = enabled.Value;
        }

        if (anchor.HasValue)
        {
            joint.anchor = anchor.Value;
        }

        if (breakForce.HasValue)
        {
            joint.breakForce = breakForce.Value;
        }

        if (breakTorque.HasValue)
        {
            joint.breakTorque = breakTorque.Value;
        }

        if (autoConfigureTarget.HasValue)
        {
            joint.autoConfigureTarget = autoConfigureTarget.Value;
        }

        if (target.HasValue)
        {
            joint.target = target.Value;
        }

        if (maxForce.HasValue)
        {
            joint.maxForce = maxForce.Value;
        }

        if (dampingRatio.HasValue)
        {
            joint.dampingRatio = dampingRatio.Value;
        }

        if (frequency.HasValue)
        {
            joint.frequency = frequency.Value;
        }

        EditorUtility.SetDirty(joint);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateTargetJoint2DSettingsSnapshot(joint),
            applied = new
            {
                enabled = enabled.HasValue,
                anchor = anchor.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                autoConfigureTarget = autoConfigureTarget.HasValue,
                target = target.HasValue,
                maxForce = maxForce.HasValue,
                dampingRatio = dampingRatio.HasValue,
                frequency = frequency.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetHingeJointSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "hingeJoint.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<HingeJoint>(resolvedObject, "instanceId", "HingeJoint");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateHingeJointSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetHingeJointSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "hingeJoint.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<HingeJoint>(resolvedObject, "instanceId", "HingeJoint");

        var autoConfigureConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureConnectedAnchor");
        var anchor = ParseOptionalVector3Parameter(paramsObject, "anchor");
        var connectedAnchor = ParseOptionalVector3Parameter(paramsObject, "connectedAnchor");
        var axis = ParseOptionalVector3Parameter(paramsObject, "axis");
        var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var connectedBodyInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "connectedBodyInstanceId");
        var connectedAnchorMode = ParseOptionalConnectedAnchorModeParameter(paramsObject, "connectedAnchorMode");
        var useSpring = ParseOptionalBooleanValueParameter(paramsObject, "useSpring");
        var spring = ParseOptionalFloatParameter(paramsObject, "spring");
        var damper = ParseOptionalFloatParameter(paramsObject, "damper");
        var targetPosition = ParseOptionalFloatParameter(paramsObject, "targetPosition");
        var useMotor = ParseOptionalBooleanValueParameter(paramsObject, "useMotor");
        var motorTargetVelocity = ParseOptionalFloatParameter(paramsObject, "motorTargetVelocity");
        var motorForce = ParseOptionalFloatParameter(paramsObject, "motorForce");
        var motorFreeSpin = ParseOptionalBooleanValueParameter(paramsObject, "motorFreeSpin");
        var useLimits = ParseOptionalBooleanValueParameter(paramsObject, "useLimits");
        var minLimit = ParseOptionalFloatParameter(paramsObject, "minLimit");
        var maxLimit = ParseOptionalFloatParameter(paramsObject, "maxLimit");

        if (!autoConfigureConnectedAnchor.HasValue &&
            !anchor.HasValue &&
            !connectedAnchor.HasValue &&
            !axis.HasValue &&
            !enableCollision.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !connectedBodyInstanceId.IsSpecified &&
            !connectedAnchorMode.HasValue &&
            !useSpring.HasValue &&
            !spring.HasValue &&
            !damper.HasValue &&
            !targetPosition.HasValue &&
            !useMotor.HasValue &&
            !motorTargetVelocity.HasValue &&
            !motorForce.HasValue &&
            !motorFreeSpin.HasValue &&
            !useLimits.HasValue &&
            !minLimit.HasValue &&
            !maxLimit.HasValue)
        {
            throw new ArgumentException("At least one HingeJoint setting must be provided.");
        }

        ValidateCommonJointSettingValues(breakForce, breakTorque);
        if (spring.HasValue && spring.Value < 0f)
        {
            throw new ArgumentException("Parameter 'spring' must be greater than or equal to 0.");
        }

        if (damper.HasValue && damper.Value < 0f)
        {
            throw new ArgumentException("Parameter 'damper' must be greater than or equal to 0.");
        }

        if (motorForce.HasValue && motorForce.Value < 0f)
        {
            throw new ArgumentException("Parameter 'motorForce' must be greater than or equal to 0.");
        }

        Undo.RecordObject(joint, "UnityMCP Set HingeJoint Settings");
        ApplyCommonJointSettings(joint, autoConfigureConnectedAnchor, anchor, connectedAnchor, axis, enableCollision, breakForce, breakTorque, connectedBodyInstanceId, connectedAnchorMode);

        if (useSpring.HasValue)
        {
            joint.useSpring = useSpring.Value;
        }

        if (spring.HasValue || damper.HasValue || targetPosition.HasValue)
        {
            var springSettings = joint.spring;
            if (spring.HasValue)
            {
                springSettings.spring = spring.Value;
            }

            if (damper.HasValue)
            {
                springSettings.damper = damper.Value;
            }

            if (targetPosition.HasValue)
            {
                springSettings.targetPosition = targetPosition.Value;
            }

            joint.spring = springSettings;
        }

        if (useMotor.HasValue)
        {
            joint.useMotor = useMotor.Value;
        }

        if (motorTargetVelocity.HasValue || motorForce.HasValue || motorFreeSpin.HasValue)
        {
            var motor = joint.motor;
            if (motorTargetVelocity.HasValue)
            {
                motor.targetVelocity = motorTargetVelocity.Value;
            }

            if (motorForce.HasValue)
            {
                motor.force = motorForce.Value;
            }

            if (motorFreeSpin.HasValue)
            {
                motor.freeSpin = motorFreeSpin.Value;
            }

            joint.motor = motor;
        }

        if (useLimits.HasValue)
        {
            joint.useLimits = useLimits.Value;
        }

        if (minLimit.HasValue || maxLimit.HasValue)
        {
            var limits = joint.limits;
            if (minLimit.HasValue)
            {
                limits.min = minLimit.Value;
            }

            if (maxLimit.HasValue)
            {
                limits.max = maxLimit.Value;
            }

            joint.limits = limits;
        }

        EditorUtility.SetDirty(joint);
        var connectionState = CreateJointAppliedConnectionState(joint, connectedAnchorMode);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateHingeJointSettingsSnapshot(joint),
            applied = new
            {
                autoConfigureConnectedAnchor = connectionState.AutoConfigureConnectedAnchor,
                anchor = anchor.HasValue,
                connectedAnchor = connectionState.ConnectedAnchor,
                connectedAnchorMode = connectionState.ConnectedAnchorMode,
                axis = axis.HasValue,
                enableCollision = enableCollision.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                connectedBodyInstanceId = connectionState.ConnectedBodyInstanceId,
                useSpring = useSpring.HasValue,
                spring = spring.HasValue,
                damper = damper.HasValue,
                targetPosition = targetPosition.HasValue,
                useMotor = useMotor.HasValue,
                motorTargetVelocity = motorTargetVelocity.HasValue,
                motorForce = motorForce.HasValue,
                motorFreeSpin = motorFreeSpin.HasValue,
                useLimits = useLimits.HasValue,
                minLimit = minLimit.HasValue,
                maxLimit = maxLimit.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetSpringJointSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "springJoint.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<SpringJoint>(resolvedObject, "instanceId", "SpringJoint");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateSpringJointSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetSpringJointSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "springJoint.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<SpringJoint>(resolvedObject, "instanceId", "SpringJoint");

        var autoConfigureConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureConnectedAnchor");
        var anchor = ParseOptionalVector3Parameter(paramsObject, "anchor");
        var connectedAnchor = ParseOptionalVector3Parameter(paramsObject, "connectedAnchor");
        var axis = ParseOptionalVector3Parameter(paramsObject, "axis");
        var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var connectedBodyInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "connectedBodyInstanceId");
        var connectedAnchorMode = ParseOptionalConnectedAnchorModeParameter(paramsObject, "connectedAnchorMode");
        var spring = ParseOptionalFloatParameter(paramsObject, "spring");
        var damper = ParseOptionalFloatParameter(paramsObject, "damper");
        var minDistance = ParseOptionalFloatParameter(paramsObject, "minDistance");
        var maxDistance = ParseOptionalFloatParameter(paramsObject, "maxDistance");
        var tolerance = ParseOptionalFloatParameter(paramsObject, "tolerance");

        if (!autoConfigureConnectedAnchor.HasValue &&
            !anchor.HasValue &&
            !connectedAnchor.HasValue &&
            !axis.HasValue &&
            !enableCollision.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !connectedBodyInstanceId.IsSpecified &&
            !connectedAnchorMode.HasValue &&
            !spring.HasValue &&
            !damper.HasValue &&
            !minDistance.HasValue &&
            !maxDistance.HasValue &&
            !tolerance.HasValue)
        {
            throw new ArgumentException("At least one SpringJoint setting must be provided.");
        }

        ValidateCommonJointSettingValues(breakForce, breakTorque);
        if (spring.HasValue && spring.Value < 0f)
        {
            throw new ArgumentException("Parameter 'spring' must be greater than or equal to 0.");
        }

        if (damper.HasValue && damper.Value < 0f)
        {
            throw new ArgumentException("Parameter 'damper' must be greater than or equal to 0.");
        }

        if (minDistance.HasValue && minDistance.Value < 0f)
        {
            throw new ArgumentException("Parameter 'minDistance' must be greater than or equal to 0.");
        }

        if (maxDistance.HasValue && maxDistance.Value < 0f)
        {
            throw new ArgumentException("Parameter 'maxDistance' must be greater than or equal to 0.");
        }

        if (tolerance.HasValue && tolerance.Value < 0f)
        {
            throw new ArgumentException("Parameter 'tolerance' must be greater than or equal to 0.");
        }

        Undo.RecordObject(joint, "UnityMCP Set SpringJoint Settings");
        ApplyCommonJointSettings(joint, autoConfigureConnectedAnchor, anchor, connectedAnchor, axis, enableCollision, breakForce, breakTorque, connectedBodyInstanceId, connectedAnchorMode);

        if (spring.HasValue)
        {
            joint.spring = spring.Value;
        }

        if (damper.HasValue)
        {
            joint.damper = damper.Value;
        }

        if (minDistance.HasValue)
        {
            joint.minDistance = minDistance.Value;
        }

        if (maxDistance.HasValue)
        {
            joint.maxDistance = maxDistance.Value;
        }

        if (tolerance.HasValue)
        {
            joint.tolerance = tolerance.Value;
        }

        EditorUtility.SetDirty(joint);
        var connectionState = CreateJointAppliedConnectionState(joint, connectedAnchorMode);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateSpringJointSettingsSnapshot(joint),
            applied = new
            {
                autoConfigureConnectedAnchor = connectionState.AutoConfigureConnectedAnchor,
                anchor = anchor.HasValue,
                connectedAnchor = connectionState.ConnectedAnchor,
                connectedAnchorMode = connectionState.ConnectedAnchorMode,
                axis = axis.HasValue,
                enableCollision = enableCollision.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                connectedBodyInstanceId = connectionState.ConnectedBodyInstanceId,
                spring = spring.HasValue,
                damper = damper.HasValue,
                minDistance = minDistance.HasValue,
                maxDistance = maxDistance.HasValue,
                tolerance = tolerance.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetFixedJointSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "fixedJoint.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<FixedJoint>(resolvedObject, "instanceId", "FixedJoint");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateFixedJointSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetFixedJointSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "fixedJoint.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<FixedJoint>(resolvedObject, "instanceId", "FixedJoint");

        var autoConfigureConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureConnectedAnchor");
        var anchor = ParseOptionalVector3Parameter(paramsObject, "anchor");
        var connectedAnchor = ParseOptionalVector3Parameter(paramsObject, "connectedAnchor");
        var axis = ParseOptionalVector3Parameter(paramsObject, "axis");
        var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var connectedBodyInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "connectedBodyInstanceId");
        var connectedAnchorMode = ParseOptionalConnectedAnchorModeParameter(paramsObject, "connectedAnchorMode");

        if (!autoConfigureConnectedAnchor.HasValue &&
            !anchor.HasValue &&
            !connectedAnchor.HasValue &&
            !axis.HasValue &&
            !enableCollision.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !connectedBodyInstanceId.IsSpecified &&
            !connectedAnchorMode.HasValue)
        {
            throw new ArgumentException("At least one FixedJoint setting must be provided.");
        }

        ValidateCommonJointSettingValues(breakForce, breakTorque);

        Undo.RecordObject(joint, "UnityMCP Set FixedJoint Settings");
        ApplyCommonJointSettings(joint, autoConfigureConnectedAnchor, anchor, connectedAnchor, axis, enableCollision, breakForce, breakTorque, connectedBodyInstanceId, connectedAnchorMode);
        EditorUtility.SetDirty(joint);
        var connectionState = CreateJointAppliedConnectionState(joint, connectedAnchorMode);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateFixedJointSettingsSnapshot(joint),
            applied = new
            {
                autoConfigureConnectedAnchor = connectionState.AutoConfigureConnectedAnchor,
                anchor = anchor.HasValue,
                connectedAnchor = connectionState.ConnectedAnchor,
                connectedAnchorMode = connectionState.ConnectedAnchorMode,
                axis = axis.HasValue,
                enableCollision = enableCollision.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                connectedBodyInstanceId = connectionState.ConnectedBodyInstanceId
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetCharacterJointSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "characterJoint.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<CharacterJoint>(resolvedObject, "instanceId", "CharacterJoint");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateCharacterJointSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetCharacterJointSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "characterJoint.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<CharacterJoint>(resolvedObject, "instanceId", "CharacterJoint");

        var autoConfigureConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureConnectedAnchor");
        var anchor = ParseOptionalVector3Parameter(paramsObject, "anchor");
        var connectedAnchor = ParseOptionalVector3Parameter(paramsObject, "connectedAnchor");
        var axis = ParseOptionalVector3Parameter(paramsObject, "axis");
        var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var connectedBodyInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "connectedBodyInstanceId");
        var connectedAnchorMode = ParseOptionalConnectedAnchorModeParameter(paramsObject, "connectedAnchorMode");
        var swingAxis = ParseOptionalVector3Parameter(paramsObject, "swingAxis");
        var enableProjection = ParseOptionalBooleanValueParameter(paramsObject, "enableProjection");
        var enablePreprocessing = ParseOptionalBooleanValueParameter(paramsObject, "enablePreprocessing");
        var twistLimitSpring = ParseOptionalSoftJointLimitSpringParameter(paramsObject, "twistLimitSpring");
        var swingLimitSpring = ParseOptionalSoftJointLimitSpringParameter(paramsObject, "swingLimitSpring");
        var lowTwistLimit = ParseOptionalSoftJointLimitParameter(paramsObject, "lowTwistLimit");
        var highTwistLimit = ParseOptionalSoftJointLimitParameter(paramsObject, "highTwistLimit");
        var swing1Limit = ParseOptionalSoftJointLimitParameter(paramsObject, "swing1Limit");
        var swing2Limit = ParseOptionalSoftJointLimitParameter(paramsObject, "swing2Limit");

        if (!autoConfigureConnectedAnchor.HasValue &&
            !anchor.HasValue &&
            !connectedAnchor.HasValue &&
            !axis.HasValue &&
            !enableCollision.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !connectedBodyInstanceId.IsSpecified &&
            !connectedAnchorMode.HasValue &&
            !swingAxis.HasValue &&
            !enableProjection.HasValue &&
            !enablePreprocessing.HasValue &&
            !twistLimitSpring.IsSpecified &&
            !swingLimitSpring.IsSpecified &&
            !lowTwistLimit.IsSpecified &&
            !highTwistLimit.IsSpecified &&
            !swing1Limit.IsSpecified &&
            !swing2Limit.IsSpecified)
        {
            throw new ArgumentException("At least one CharacterJoint setting must be provided.");
        }

        ValidateCommonJointSettingValues(breakForce, breakTorque);
        ValidateSoftJointLimitSpringUpdate(twistLimitSpring, "twistLimitSpring");
        ValidateSoftJointLimitSpringUpdate(swingLimitSpring, "swingLimitSpring");
        ValidateSoftJointLimitUpdate(lowTwistLimit, "lowTwistLimit");
        ValidateSoftJointLimitUpdate(highTwistLimit, "highTwistLimit");
        ValidateSoftJointLimitUpdate(swing1Limit, "swing1Limit");
        ValidateSoftJointLimitUpdate(swing2Limit, "swing2Limit");

        Undo.RecordObject(joint, "UnityMCP Set CharacterJoint Settings");
        ApplyCommonJointSettings(joint, autoConfigureConnectedAnchor, anchor, connectedAnchor, axis, enableCollision, breakForce, breakTorque, connectedBodyInstanceId, connectedAnchorMode);

        if (swingAxis.HasValue)
        {
            joint.swingAxis = swingAxis.Value;
        }

        if (enableProjection.HasValue)
        {
            joint.enableProjection = enableProjection.Value;
        }

        if (enablePreprocessing.HasValue)
        {
            joint.enablePreprocessing = enablePreprocessing.Value;
        }

        if (twistLimitSpring.IsSpecified)
        {
            joint.twistLimitSpring = ApplySoftJointLimitSpringUpdate(joint.twistLimitSpring, twistLimitSpring);
        }

        if (swingLimitSpring.IsSpecified)
        {
            joint.swingLimitSpring = ApplySoftJointLimitSpringUpdate(joint.swingLimitSpring, swingLimitSpring);
        }

        if (lowTwistLimit.IsSpecified)
        {
            joint.lowTwistLimit = ApplySoftJointLimitUpdate(joint.lowTwistLimit, lowTwistLimit);
        }

        if (highTwistLimit.IsSpecified)
        {
            joint.highTwistLimit = ApplySoftJointLimitUpdate(joint.highTwistLimit, highTwistLimit);
        }

        if (swing1Limit.IsSpecified)
        {
            joint.swing1Limit = ApplySoftJointLimitUpdate(joint.swing1Limit, swing1Limit);
        }

        if (swing2Limit.IsSpecified)
        {
            joint.swing2Limit = ApplySoftJointLimitUpdate(joint.swing2Limit, swing2Limit);
        }

        EditorUtility.SetDirty(joint);
        var connectionState = CreateJointAppliedConnectionState(joint, connectedAnchorMode);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateCharacterJointSettingsSnapshot(joint),
            applied = new
            {
                autoConfigureConnectedAnchor = connectionState.AutoConfigureConnectedAnchor,
                anchor = anchor.HasValue,
                connectedAnchor = connectionState.ConnectedAnchor,
                connectedAnchorMode = connectionState.ConnectedAnchorMode,
                axis = axis.HasValue,
                enableCollision = enableCollision.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                connectedBodyInstanceId = connectionState.ConnectedBodyInstanceId,
                swingAxis = swingAxis.HasValue,
                enableProjection = enableProjection.HasValue,
                enablePreprocessing = enablePreprocessing.HasValue,
                twistLimitSpring = twistLimitSpring.IsSpecified,
                swingLimitSpring = swingLimitSpring.IsSpecified,
                lowTwistLimit = lowTwistLimit.IsSpecified,
                highTwistLimit = highTwistLimit.IsSpecified,
                swing1Limit = swing1Limit.IsSpecified,
                swing2Limit = swing2Limit.IsSpecified
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetConfigurableJointSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "configurableJoint.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<ConfigurableJoint>(resolvedObject, "instanceId", "ConfigurableJoint");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateConfigurableJointSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetConfigurableJointSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "configurableJoint.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<ConfigurableJoint>(resolvedObject, "instanceId", "ConfigurableJoint");

        var autoConfigureConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureConnectedAnchor");
        var anchor = ParseOptionalVector3Parameter(paramsObject, "anchor");
        var connectedAnchor = ParseOptionalVector3Parameter(paramsObject, "connectedAnchor");
        var axis = ParseOptionalVector3Parameter(paramsObject, "axis");
        var secondaryAxis = ParseOptionalVector3Parameter(paramsObject, "secondaryAxis");
        var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var connectedBodyInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "connectedBodyInstanceId");
        var connectedAnchorMode = ParseOptionalConnectedAnchorModeParameter(paramsObject, "connectedAnchorMode");
        var configuredInWorldSpace = ParseOptionalBooleanValueParameter(paramsObject, "configuredInWorldSpace");
        var swapBodies = ParseOptionalBooleanValueParameter(paramsObject, "swapBodies");
        var xMotion = ParseOptionalEnumParameter<ConfigurableJointMotion>(paramsObject, "xMotion");
        var yMotion = ParseOptionalEnumParameter<ConfigurableJointMotion>(paramsObject, "yMotion");
        var zMotion = ParseOptionalEnumParameter<ConfigurableJointMotion>(paramsObject, "zMotion");
        var angularXMotion = ParseOptionalEnumParameter<ConfigurableJointMotion>(paramsObject, "angularXMotion");
        var angularYMotion = ParseOptionalEnumParameter<ConfigurableJointMotion>(paramsObject, "angularYMotion");
        var angularZMotion = ParseOptionalEnumParameter<ConfigurableJointMotion>(paramsObject, "angularZMotion");
        var linearLimit = ParseOptionalSoftJointLimitParameter(paramsObject, "linearLimit");
        var lowAngularXLimit = ParseOptionalSoftJointLimitParameter(paramsObject, "lowAngularXLimit");
        var highAngularXLimit = ParseOptionalSoftJointLimitParameter(paramsObject, "highAngularXLimit");
        var angularYLimit = ParseOptionalSoftJointLimitParameter(paramsObject, "angularYLimit");
        var angularZLimit = ParseOptionalSoftJointLimitParameter(paramsObject, "angularZLimit");
        var targetPosition = ParseOptionalVector3Parameter(paramsObject, "targetPosition");
        var targetVelocity = ParseOptionalVector3Parameter(paramsObject, "targetVelocity");
        var targetAngularVelocity = ParseOptionalVector3Parameter(paramsObject, "targetAngularVelocity");
        var rotationDriveMode = ParseOptionalEnumParameter<RotationDriveMode>(paramsObject, "rotationDriveMode");
        var xDrive = ParseOptionalJointDriveParameter(paramsObject, "xDrive");
        var yDrive = ParseOptionalJointDriveParameter(paramsObject, "yDrive");
        var zDrive = ParseOptionalJointDriveParameter(paramsObject, "zDrive");
        var angularXDrive = ParseOptionalJointDriveParameter(paramsObject, "angularXDrive");
        var angularYZDrive = ParseOptionalJointDriveParameter(paramsObject, "angularYZDrive");
        var slerpDrive = ParseOptionalJointDriveParameter(paramsObject, "slerpDrive");
        var projectionMode = ParseOptionalEnumParameter<JointProjectionMode>(paramsObject, "projectionMode");
        var projectionDistance = ParseOptionalFloatParameter(paramsObject, "projectionDistance");
        var projectionAngle = ParseOptionalFloatParameter(paramsObject, "projectionAngle");

        if (!autoConfigureConnectedAnchor.HasValue &&
            !anchor.HasValue &&
            !connectedAnchor.HasValue &&
            !axis.HasValue &&
            !secondaryAxis.HasValue &&
            !enableCollision.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !connectedBodyInstanceId.IsSpecified &&
            !connectedAnchorMode.HasValue &&
            !configuredInWorldSpace.HasValue &&
            !swapBodies.HasValue &&
            !xMotion.HasValue &&
            !yMotion.HasValue &&
            !zMotion.HasValue &&
            !angularXMotion.HasValue &&
            !angularYMotion.HasValue &&
            !angularZMotion.HasValue &&
            !linearLimit.IsSpecified &&
            !lowAngularXLimit.IsSpecified &&
            !highAngularXLimit.IsSpecified &&
            !angularYLimit.IsSpecified &&
            !angularZLimit.IsSpecified &&
            !targetPosition.HasValue &&
            !targetVelocity.HasValue &&
            !targetAngularVelocity.HasValue &&
            !rotationDriveMode.HasValue &&
            !xDrive.IsSpecified &&
            !yDrive.IsSpecified &&
            !zDrive.IsSpecified &&
            !angularXDrive.IsSpecified &&
            !angularYZDrive.IsSpecified &&
            !slerpDrive.IsSpecified &&
            !projectionMode.HasValue &&
            !projectionDistance.HasValue &&
            !projectionAngle.HasValue)
        {
            throw new ArgumentException("At least one ConfigurableJoint setting must be provided.");
        }

        ValidateCommonJointSettingValues(breakForce, breakTorque);
        ValidateSoftJointLimitUpdate(linearLimit, "linearLimit");
        ValidateSoftJointLimitUpdate(lowAngularXLimit, "lowAngularXLimit");
        ValidateSoftJointLimitUpdate(highAngularXLimit, "highAngularXLimit");
        ValidateSoftJointLimitUpdate(angularYLimit, "angularYLimit");
        ValidateSoftJointLimitUpdate(angularZLimit, "angularZLimit");
        ValidateJointDriveUpdate(xDrive, "xDrive");
        ValidateJointDriveUpdate(yDrive, "yDrive");
        ValidateJointDriveUpdate(zDrive, "zDrive");
        ValidateJointDriveUpdate(angularXDrive, "angularXDrive");
        ValidateJointDriveUpdate(angularYZDrive, "angularYZDrive");
        ValidateJointDriveUpdate(slerpDrive, "slerpDrive");
        if (projectionDistance.HasValue && projectionDistance.Value < 0f)
        {
            throw new ArgumentException("Parameter 'projectionDistance' must be greater than or equal to 0.");
        }

        if (projectionAngle.HasValue && projectionAngle.Value < 0f)
        {
            throw new ArgumentException("Parameter 'projectionAngle' must be greater than or equal to 0.");
        }

        Undo.RecordObject(joint, "UnityMCP Set ConfigurableJoint Settings");
        ApplyCommonJointSettings(joint, autoConfigureConnectedAnchor, anchor, connectedAnchor, axis, enableCollision, breakForce, breakTorque, connectedBodyInstanceId, connectedAnchorMode);

        if (secondaryAxis.HasValue)
        {
            joint.secondaryAxis = secondaryAxis.Value;
        }

        if (configuredInWorldSpace.HasValue)
        {
            joint.configuredInWorldSpace = configuredInWorldSpace.Value;
        }

        if (swapBodies.HasValue)
        {
            joint.swapBodies = swapBodies.Value;
        }

        if (xMotion.HasValue)
        {
            joint.xMotion = xMotion.Value;
        }

        if (yMotion.HasValue)
        {
            joint.yMotion = yMotion.Value;
        }

        if (zMotion.HasValue)
        {
            joint.zMotion = zMotion.Value;
        }

        if (angularXMotion.HasValue)
        {
            joint.angularXMotion = angularXMotion.Value;
        }

        if (angularYMotion.HasValue)
        {
            joint.angularYMotion = angularYMotion.Value;
        }

        if (angularZMotion.HasValue)
        {
            joint.angularZMotion = angularZMotion.Value;
        }

        if (linearLimit.IsSpecified)
        {
            joint.linearLimit = ApplySoftJointLimitUpdate(joint.linearLimit, linearLimit);
        }

        if (lowAngularXLimit.IsSpecified)
        {
            joint.lowAngularXLimit = ApplySoftJointLimitUpdate(joint.lowAngularXLimit, lowAngularXLimit);
        }

        if (highAngularXLimit.IsSpecified)
        {
            joint.highAngularXLimit = ApplySoftJointLimitUpdate(joint.highAngularXLimit, highAngularXLimit);
        }

        if (angularYLimit.IsSpecified)
        {
            joint.angularYLimit = ApplySoftJointLimitUpdate(joint.angularYLimit, angularYLimit);
        }

        if (angularZLimit.IsSpecified)
        {
            joint.angularZLimit = ApplySoftJointLimitUpdate(joint.angularZLimit, angularZLimit);
        }

        if (targetPosition.HasValue)
        {
            joint.targetPosition = targetPosition.Value;
        }

        if (targetVelocity.HasValue)
        {
            joint.targetVelocity = targetVelocity.Value;
        }

        if (targetAngularVelocity.HasValue)
        {
            joint.targetAngularVelocity = targetAngularVelocity.Value;
        }

        if (rotationDriveMode.HasValue)
        {
            joint.rotationDriveMode = rotationDriveMode.Value;
        }

        if (xDrive.IsSpecified)
        {
            joint.xDrive = ApplyJointDriveUpdate(joint.xDrive, xDrive);
        }

        if (yDrive.IsSpecified)
        {
            joint.yDrive = ApplyJointDriveUpdate(joint.yDrive, yDrive);
        }

        if (zDrive.IsSpecified)
        {
            joint.zDrive = ApplyJointDriveUpdate(joint.zDrive, zDrive);
        }

        if (angularXDrive.IsSpecified)
        {
            joint.angularXDrive = ApplyJointDriveUpdate(joint.angularXDrive, angularXDrive);
        }

        if (angularYZDrive.IsSpecified)
        {
            joint.angularYZDrive = ApplyJointDriveUpdate(joint.angularYZDrive, angularYZDrive);
        }

        if (slerpDrive.IsSpecified)
        {
            joint.slerpDrive = ApplyJointDriveUpdate(joint.slerpDrive, slerpDrive);
        }

        if (projectionMode.HasValue)
        {
            joint.projectionMode = projectionMode.Value;
        }

        if (projectionDistance.HasValue)
        {
            joint.projectionDistance = projectionDistance.Value;
        }

        if (projectionAngle.HasValue)
        {
            joint.projectionAngle = projectionAngle.Value;
        }

        EditorUtility.SetDirty(joint);
        var connectionState = CreateJointAppliedConnectionState(joint, connectedAnchorMode);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateConfigurableJointSettingsSnapshot(joint),
            applied = new
            {
                autoConfigureConnectedAnchor = connectionState.AutoConfigureConnectedAnchor,
                anchor = anchor.HasValue,
                connectedAnchor = connectionState.ConnectedAnchor,
                connectedAnchorMode = connectionState.ConnectedAnchorMode,
                axis = axis.HasValue,
                secondaryAxis = secondaryAxis.HasValue,
                enableCollision = enableCollision.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                connectedBodyInstanceId = connectionState.ConnectedBodyInstanceId,
                configuredInWorldSpace = configuredInWorldSpace.HasValue,
                swapBodies = swapBodies.HasValue,
                xMotion = xMotion.HasValue,
                yMotion = yMotion.HasValue,
                zMotion = zMotion.HasValue,
                angularXMotion = angularXMotion.HasValue,
                angularYMotion = angularYMotion.HasValue,
                angularZMotion = angularZMotion.HasValue,
                linearLimit = linearLimit.IsSpecified,
                lowAngularXLimit = lowAngularXLimit.IsSpecified,
                highAngularXLimit = highAngularXLimit.IsSpecified,
                angularYLimit = angularYLimit.IsSpecified,
                angularZLimit = angularZLimit.IsSpecified,
                targetPosition = targetPosition.HasValue,
                targetVelocity = targetVelocity.HasValue,
                targetAngularVelocity = targetAngularVelocity.HasValue,
                rotationDriveMode = rotationDriveMode.HasValue,
                xDrive = xDrive.IsSpecified,
                yDrive = yDrive.IsSpecified,
                zDrive = zDrive.IsSpecified,
                angularXDrive = angularXDrive.IsSpecified,
                angularYZDrive = angularYZDrive.IsSpecified,
                slerpDrive = slerpDrive.IsSpecified,
                projectionMode = projectionMode.HasValue,
                projectionDistance = projectionDistance.HasValue,
                projectionAngle = projectionAngle.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }
}
}
