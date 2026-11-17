using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Cryptography;
using System.Text;
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
        var tokenHash = HashToken(request.Token);
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var matches = await _context.Set<UserDeviceToken>().IgnoreQueryFilters()
            .Where(item => item.TokenHash == tokenHash)
            .OrderByDescending(item => item.IsActive)
            .ThenByDescending(item => item.LastSeenAt)
            .ToListAsync(cancellationToken);
        if (matches.Any(item => item.IsActive && (item.TenantId != tenantId || item.UserId != userId)))
            throw new DeviceTokenOwnershipConflictException();

        if (request.PreviousDeviceId.HasValue)
        {
            var previous = await _context.Set<UserDeviceToken>().IgnoreQueryFilters().SingleOrDefaultAsync(item =>
                item.Id == request.PreviousDeviceId && item.TenantId == tenantId && item.UserId == userId, cancellationToken);
            if (previous is not null && previous.TokenHash != tokenHash) Deactivate(previous, userId);
        }

        var device = matches.FirstOrDefault(item => item.IsActive && !item.IsDeleted
            && item.TenantId == tenantId && item.UserId == userId);
        foreach (var duplicateOwnedDevice in matches.Where(item => item.IsActive
            && item != device
            && item.TenantId == tenantId
            && item.UserId == userId))
            Deactivate(duplicateOwnedDevice, userId);

        if (device is null)
        {
            // Reuse this user's inactive row when possible. If this same owner has a
            // legacy active/deleted row, persist its deactivation before inserting so
            // the filtered unique index remains safe within the transaction.
            device = matches.FirstOrDefault(item => !item.IsActive && !item.IsDeleted
                && item.TenantId == tenantId && item.UserId == userId);
            if (device is null)
            {
                if (matches.Any(item => item.IsActive && item.TenantId == tenantId && item.UserId == userId))
                    await _context.SaveChangesAsync(cancellationToken);
                device = new UserDeviceToken { CreatedBy = userId.ToString() };
                _context.Set<UserDeviceToken>().Add(device);
            }
        }

        device.TenantId = tenantId;
        device.UserId = userId;
        device.Token = request.Token;
        device.TokenHash = tokenHash;
        device.Platform = request.Platform;
        device.DeviceName = request.DeviceName;
        device.IsActive = true;
        device.DeactivatedAt = null;
        device.LastSeenAt = DateTime.UtcNow;
        device.UpdatedBy = userId.ToString();
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new DeviceTokenResponse(device.Id, device.Platform, device.DeviceName, device.IsActive, device.LastSeenAt);
    }

    public async Task<bool> DeactivateAsync(Guid tenantId, Guid userId, Guid deviceId, CancellationToken cancellationToken = default)
    {
        var device = await _context.Set<UserDeviceToken>()
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == deviceId && item.TenantId == tenantId && item.UserId == userId, cancellationToken);
        if (device is null) return false;

        Deactivate(device, userId);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    internal static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.Unicode.GetBytes(token)));

    private static void Deactivate(UserDeviceToken device, Guid actorId)
    {
        device.IsActive = false;
        device.DeactivatedAt = DateTime.UtcNow;
        device.UpdatedBy = actorId.ToString();
    }
}
