namespace NEXUS.SharedKernel.Helpers;

public static class GuidHelper
{
    public static Guid? TryParse(string? value) =>
        Guid.TryParse(value, out var parsed) ? parsed : null;
}
