namespace NEXUS.Modules.Identity.Application;

public record RegisterCommand(string Email, string Password, string FirstName, string LastName);
