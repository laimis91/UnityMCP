#nullable enable

using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMcp.Editor
{
    internal sealed partial class UnityMcpClient
    {
        private static string BuildGetJointSettingsResponse(JToken idToken, JObject root)
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

        private static string BuildSetJointSettingsResponse(JToken idToken, JObject root)
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
    }
}
