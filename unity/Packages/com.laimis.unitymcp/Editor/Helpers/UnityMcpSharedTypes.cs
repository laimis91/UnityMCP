#nullable enable

using UnityEngine;

namespace UnityMcp.Editor
{

internal enum ConnectedAnchorMode
{
    Preserve,
    Auto,
    Zero,
    MatchAnchor
}

internal enum PrefabOverrideScope
{
    InstanceRoot,
    Object,
    Component
}

internal readonly struct OptionalInstanceIdParameter
{
    public OptionalInstanceIdParameter(bool isSpecified, int? value)
    {
        IsSpecified = isSpecified;
        Value = value;
    }

    public bool IsSpecified { get; }

    public int? Value { get; }

    public bool HasValue => Value.HasValue;
}

internal readonly struct PrefabInstanceDetails
{
    public PrefabInstanceDetails(
        GameObject targetGameObject,
        GameObject nearestPrefabInstanceRoot,
        GameObject outermostPrefabInstanceRoot,
        GameObject sourceAsset,
        string assetPath,
        string guid,
        string prefabInstanceStatus,
        string prefabAssetType)
    {
        TargetGameObject = targetGameObject;
        NearestPrefabInstanceRoot = nearestPrefabInstanceRoot;
        OutermostPrefabInstanceRoot = outermostPrefabInstanceRoot;
        SourceAsset = sourceAsset;
        AssetPath = assetPath;
        Guid = guid;
        PrefabInstanceStatus = prefabInstanceStatus;
        PrefabAssetType = prefabAssetType;
    }

    public GameObject TargetGameObject { get; }

    public GameObject NearestPrefabInstanceRoot { get; }

    public GameObject OutermostPrefabInstanceRoot { get; }

    public GameObject SourceAsset { get; }

    public string AssetPath { get; }

    public string Guid { get; }

    public string PrefabInstanceStatus { get; }

    public string PrefabAssetType { get; }

    public bool IsOutermostPrefabInstanceRoot => TargetGameObject == OutermostPrefabInstanceRoot;
}

internal readonly struct SoftJointLimitUpdate
{
    public SoftJointLimitUpdate(bool isSpecified, float? limit, float? bounciness, float? contactDistance)
    {
        IsSpecified = isSpecified;
        Limit = limit;
        Bounciness = bounciness;
        ContactDistance = contactDistance;
    }

    public bool IsSpecified { get; }

    public float? Limit { get; }

    public float? Bounciness { get; }

    public float? ContactDistance { get; }
}

internal readonly struct SoftJointLimitSpringUpdate
{
    public SoftJointLimitSpringUpdate(bool isSpecified, float? spring, float? damper)
    {
        IsSpecified = isSpecified;
        Spring = spring;
        Damper = damper;
    }

    public bool IsSpecified { get; }

    public float? Spring { get; }

    public float? Damper { get; }
}

internal readonly struct JointDriveUpdate
{
    public JointDriveUpdate(bool isSpecified, float? positionSpring, float? positionDamper, float? maximumForce)
    {
        IsSpecified = isSpecified;
        PositionSpring = positionSpring;
        PositionDamper = positionDamper;
        MaximumForce = maximumForce;
    }

    public bool IsSpecified { get; }

    public float? PositionSpring { get; }

    public float? PositionDamper { get; }

    public float? MaximumForce { get; }
}

}
