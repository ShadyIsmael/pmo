using NEXUS.SharedKernel.Management;

namespace NEXUS.Api.Management;

public sealed class ManagementContextAccessor : IManagementContextAccessor
{
    public ManagementContext? Current { get; private set; }
    public void Set(ManagementContext context) => Current = context;
}
