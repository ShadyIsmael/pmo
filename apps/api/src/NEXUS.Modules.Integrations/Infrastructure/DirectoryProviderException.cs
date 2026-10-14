using System.Net;

namespace NEXUS.Modules.Integrations.Infrastructure;

public sealed class DirectoryProviderException(string code, string message, HttpStatusCode? httpStatus = null) : Exception(message)
{
    public string Code { get; } = code;
    public HttpStatusCode? HttpStatus { get; } = httpStatus;
}
