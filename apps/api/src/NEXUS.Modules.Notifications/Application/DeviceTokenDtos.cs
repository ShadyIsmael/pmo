namespace NEXUS.Modules.Notifications.Application;

public sealed record RegisterDeviceTokenRequest(
    string Token,
    string Platform,
    string? DeviceName = null,
    Guid? PreviousDeviceId = null);

public sealed record DeviceTokenResponse(
    Guid Id,
    string Platform,
    string? DeviceName,
    bool IsActive,
    DateTime LastSeenAt);

public sealed class DeviceTokenOwnershipConflictException : Exception
{
    public DeviceTokenOwnershipConflictException()
        : base("This device token is already registered to another account.")
    {
    }
}
