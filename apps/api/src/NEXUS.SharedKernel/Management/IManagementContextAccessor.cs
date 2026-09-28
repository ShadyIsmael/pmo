namespace NEXUS.SharedKernel.Management;

public interface IManagementContextAccessor
{
    ManagementContext? Current { get; }
    void Set(ManagementContext context);
}
