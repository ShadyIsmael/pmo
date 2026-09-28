namespace NEXUS.SharedKernel;

public sealed record UserReference(Guid Id, string FullName, string Email);

public interface IUserReferenceLookup
{
    Task<UserReference?> FindAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, UserReference>> GetManyAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, UserReference>> FindByEmailsAsync(IEnumerable<string> emails, CancellationToken cancellationToken = default);
}
