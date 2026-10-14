namespace NEXUS.Modules.Integrations.Application;

public sealed class DirectoryIntegrationException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}
