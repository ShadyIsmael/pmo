$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:NEXUS_EF_DEVELOPMENT_CONNECTION_STRING = 'Server=tcp:localhost,1433;Database=NexusPmo;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=true'
dotnet ef migrations list --project apps/api/src/NEXUS.Persistence --startup-project apps/api/src/NEXUS.Api --context AppDbContext