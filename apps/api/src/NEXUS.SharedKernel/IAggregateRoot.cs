namespace NEXUS.SharedKernel;

public interface IAggregateRoot
{
    IReadOnlyCollection<BaseDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
