using NEXUS.Modules.WorkItems.Application;
using Xunit;

namespace NEXUS.Modules.WorkItems.Tests;

public class WorkItemTerminalStateTests
{
    [Fact]
    public void LegacyFallbackTerminalStatusCodes_IncludesDoneAndCancelled()
    {
        Assert.Contains("Done", WorkItemTerminalStateLookup.LegacyFallbackTerminalStatusCodes);
        Assert.Contains("Cancelled", WorkItemTerminalStateLookup.LegacyFallbackTerminalStatusCodes);
    }

    [Fact]
    public void LegacyFallbackTerminalStatusCodes_ContainsExpectedCompatibilityStatuses()
    {
        Assert.Equal(2, WorkItemTerminalStateLookup.LegacyFallbackTerminalStatusCodes.Length);
        Assert.Equal(new[] { "Done", "Cancelled" }, WorkItemTerminalStateLookup.LegacyFallbackTerminalStatusCodes);
    }
}
