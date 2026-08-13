namespace NEXUS.Modules.Identity.Application;

public class UserLookupDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
}
