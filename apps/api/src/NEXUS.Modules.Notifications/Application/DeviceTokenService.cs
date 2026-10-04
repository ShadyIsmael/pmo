using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Notifications.Domain;

namespace NEXUS.Modules.Notifications.Application;

public sealed class DeviceTokenService : IDeviceTokenService
{
    private readonly DbContext _context;

    public DeviceTokenService(DbContext context)
    {
        _context = context;
    }

    public async Task<DeviceTokenResponse> RegisterAsync(Guid tenantId, Guid userId, RegisterDeviceTokenRequest request, CancellationToken cancellationToken = default)
    {
        var device = await _context.Set<UserDeviceToken>()
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Token == request.Token, cancellationToken);

        if (device is null)
        {
            device = new UserDeviceToken
            {
                TenantId = tenantId,
                UserId = userId,
                Token = request.Token,
                Platform = request.Platform,
                DeviceName = request.DeviceName,
                CreatedBy = userId.ToString(),
            };
            _context.Set<UserDeviceToken>().Add(device);
        }
        else
        {
            device.UserId = userId;
            device.Platform = request.Platform;
            device.DeviceName = request.DeviceName;
            device.IsActive = true;
            device.DeactivatedAt = null;
            device.LastSeenAt = DateTime.UtcNow;
            device.UpdatedBy = userId.ToString();
        }

        await _context.SaveChangesAsync(cancellationToken);
        return new DeviceTokenResponse(device.Id, device.Platform, device.DeviceName, device.IsActive, device.LastSeenAt);
    }

    public async Task<bool> DeactivateAsync(Guid tenantId, Guid userId, Guid deviceId, CancellationToken cancellationToken = default)
    {
        var device = await _context.Set<UserDeviceToken>()
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == deviceId && item.TenantId == tenantId && item.UserId == userId, cancellationToken);
        if (device is null) return false;

        device.IsActive = false;
        device.DeactivatedAt = DateTime.UtcNow;
        device.UpdatedBy = userId.ToString();
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
