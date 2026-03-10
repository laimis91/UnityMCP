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