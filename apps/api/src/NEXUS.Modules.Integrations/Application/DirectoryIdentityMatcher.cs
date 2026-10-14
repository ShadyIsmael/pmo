using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Integrations.Domain;

namespace NEXUS.Modules.Integrations.Application;

public sealed record DirectoryUserMatch(Guid? UserId, bool IsConflict, string Reason);

public static class DirectoryIdentityMatcher
{
    public static DirectoryUserMatch Match(
        Guid connectionId,
        DirectoryUserRecord source,
        IReadOnlyCollection<DirectoryUserLink> links,
        IReadOnlyCollection<User> users)
    {
        var byExternalId = links.FirstOrDefault(link => link.DirectoryConnectionId == connectionId
            && string.Equals(link.ExternalDirectoryId, source.ExternalDirectoryId, StringComparison.OrdinalIgnoreCase));

        var candidates = new HashSet<Guid>();
        if (byExternalId?.UserId is { } linkedUserId) candidates.Add(linkedUserId);

        if (!string.IsNullOrWhiteSpace(source.Email))
        {
            foreach (var user in users.Where(user => string.Equals(user.Email, source.Email, StringComparison.OrdinalIgnoreCase)))
                candidates.Add(user.Id);
        }

        if (!string.IsNullOrWhiteSpace(source.UserPrincipalName))
        {
            foreach (var link in links.Where(link => link.DirectoryConnectionId == connectionId
                && string.Equals(link.UserPrincipalName, source.UserPrincipalName, StringComparison.OrdinalIgnoreCase)))
            {
                if (link.UserId is { } linkedId) candidates.Add(linkedId);
            }
            foreach (var user in users.Where(user => string.Equals(user.Email, source.UserPrincipalName, StringComparison.OrdinalIgnoreCase)))
                candidates.Add(user.Id);
        }

        if (candidates.Count > 1)
            return new DirectoryUserMatch(null, true, "External ID, email, or UPN point to different NEXUS users.");
        if (candidates.Count == 1)
            return new DirectoryUserMatch(candidates.Single(), false, byExternalId is not null ? "External directory ID matched." : "Email or UPN matched.");

        // Display name is intentionally excluded: it is not a safe identity key.
        return new DirectoryUserMatch(null, false, "No existing NEXUS identity matched by external ID, email, or UPN.");
    }
}
