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
    internal static class Joints2DHandler
    {
        #region HingeJoint2D Methods

        internal static string BuildGetHingeJoint2DSettingsResponse(JToken idToken, JObject root)
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

        internal static string BuildSetHingeJoint2DSettingsResponse(JToken idToken, JObject root)
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

        #endregion

        #region SpringJoint2D Methods

        internal static string BuildGetSpringJoint2DSettingsResponse(JToken idToken, JObject root)
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

        internal static string BuildSetSpringJoint2DSettingsResponse(JToken idToken, JObject root)
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

        #endregion

        #region DistanceJoint2D Methods

        internal static string BuildGetDistanceJoint2DSettingsResponse(JToken idToken, JObject root)
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

        internal static string BuildSetDistanceJoint2DSettingsResponse(JToken idToken, JObject root)
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

        #endregion

        #region FixedJoint2D Methods

        internal static string BuildGetFixedJoint2DSettingsResponse(JToken idToken, JObject root)
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

        internal static string BuildSetFixedJoint2DSettingsResponse(JToken idToken, JObject root)
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

        #endregion

        #region SliderJoint2D Methods

        internal static string BuildGetSliderJoint2DSettingsResponse(JToken idToken, JObject root)
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

        internal static string BuildSetSliderJoint2DSettingsResponse(JToken idToken, JObject root)
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

        #endregion

        #region WheelJoint2D Methods

        internal static string BuildGetWheelJoint2DSettingsResponse(JToken idToken, JObject root)
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

        internal static string BuildSetWheelJoint2DSettingsResponse(JToken idToken, JObject root)
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

        #endregion

        #region TargetJoint2D Methods

        internal static string BuildGetTargetJoint2DSettingsResponse(JToken idToken, JObject root)
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

        internal static string BuildSetTargetJoint2DSettingsResponse(JToken idToken, JObject root)
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

        #endregion
    }
}