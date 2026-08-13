using Xunit;

// Integration tests share the process-wide AppDbContext tenant resolution
// (AppDbContext.ConfigureTenantResolution), mirroring the single wiring point in
// production Program.cs. Running collections in parallel would let one test swap
// the tenant resolution while another is querying, so the test classes must run
// sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
