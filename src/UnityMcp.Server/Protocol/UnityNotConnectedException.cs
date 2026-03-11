namespace UnityMcp.Server.Protocol;

public sealed class UnityNotConnectedException : Exception
{
    public UnityNotConnectedException()
        : base("Unity is not connected.")
    {
    }
}
