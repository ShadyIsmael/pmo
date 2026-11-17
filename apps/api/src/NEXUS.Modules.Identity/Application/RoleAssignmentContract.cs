namespace NEXUS.Modules.Identity.Application;

/// <summary>Stable request/error identifiers shared by role assignment endpoints.</summary>
public static class RoleAssignmentContract
{
    public const string RoleIdsRequired = "ROLE_ASSIGNMENT_ID_REQUIRED";
    public const string InvalidRoleIds = "ROLE_ASSIGNMENT_ID_INVALID";
    public const string OutOfScope = "ROLE_ASSIGNMENT_OUT_OF_SCOPE";
    public const string ProtectedRole = "ROLE_PROTECTED";
    public const string MembershipDependencyConflict = "ROLE_ASSIGNMENT_DEPENDENCY_CONFLICT";
}
