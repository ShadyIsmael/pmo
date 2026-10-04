namespace NEXUS.Modules.Notifications.Application;

public interface IDeviceTokenService
{
    Task<DeviceTokenResponse> RegisterAsync(Guid tenantId, Guid userId, RegisterDeviceTokenRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(Guid tenantId, Guid userId, Guid deviceId, CancellationToken cancellationToken = default);
}
