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
    internal static class JointsHandler
    {
        // ── Base Joint (3D) Methods ──────────────────────────────────────

        internal static string BuildGetJointSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "joint.getSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var joint = ResolveComponentOfTypeTarget<Joint>(resolvedObject, "instanceId", "Joint");

            var connectedBody = joint.connectedBody;
            var result = new
            {
                target = CreateObjectSummary(joint.gameObject),
                component = CreateComponentSummary(joint),
                settings = new
                {
                    connectedBodyInstanceId = connectedBody != null ? (int?)connectedBody.gameObject.GetInstanceID() : null,
                    breakForce = joint.breakForce,
                    breakTorque = joint.breakTorque,
                    enableCollision = joint.enableCollision,
                    enablePreprocessing = joint.enablePreprocessing
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildSetJointSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "joint.setSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var joint = ResolveComponentOfTypeTarget<Joint>(resolvedObject, "instanceId", "Joint");

            var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
            var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
            var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");

            if (!breakForce.HasValue && !breakTorque.HasValue && !enableCollision.HasValue)
            {
                throw new ArgumentException("At least one joint setting must be provided: breakForce, breakTorque, or enableCollision.");
            }

            Undo.RecordObject(joint, "UnityMCP Set Joint Settings");

            if (breakForce.HasValue)
            {
                joint.breakForce = breakForce.Value;
            }

            if (breakTorque.HasValue)
            {
                joint.breakTorque = breakTorque.Value;
            }

            if (enableCollision.HasValue)
            {
                joint.enableCollision = enableCollision.Value;
            }

            EditorUtility.SetDirty(joint);

            var connectedBody = joint.connectedBody;
            var result = new
            {
                target = CreateObjectSummary(joint.gameObject),
                component = CreateComponentSummary(joint),
                settings = new
                {
                    connectedBodyInstanceId = connectedBody != null ? (int?)connectedBody.gameObject.GetInstanceID() : null,
                    breakForce = joint.breakForce,
                    breakTorque = joint.breakTorque,
                    enableCollision = joint.enableCollision,
                    enablePreprocessing = joint.enablePreprocessing
                },
                applied = new
                {
                    breakForce = breakForce.HasValue,
                    breakTorque = breakTorque.HasValue,
                    enableCollision = enableCollision.HasValue
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        // ── HingeJoint Methods ──────────────────────────────────────────

        internal static string BuildGetHingeJointSettingsResponse(JToken idToken, JObject root)
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

        internal static string BuildSetHingeJointSettingsResponse(JToken idToken, JObject root)
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

        // ── SpringJoint Methods ──────────────────────────────────────────

        internal static string BuildGetSpringJointSettingsResponse(JToken idToken, JObject root)
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

        internal static string BuildSetSpringJointSettingsResponse(JToken idToken, JObject root)
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

        // ── FixedJoint Methods ──────────────────────────────────────────

        internal static string BuildGetFixedJointSettingsResponse(JToken idToken, JObject root)
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

        internal static string BuildSetFixedJointSettingsResponse(JToken idToken, JObject root)
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
    }
}