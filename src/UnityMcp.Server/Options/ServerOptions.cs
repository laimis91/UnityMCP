namespace UnityMcp.Server.Options;

public sealed class ServerOptions
{
    public const string SectionName = "UnityMcp";

    public int Port { get; set; } = 5001;

    public int UnityRequestTimeoutSeconds { get; set; } = 30;
}

