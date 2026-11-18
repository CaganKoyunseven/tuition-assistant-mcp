namespace TuitionAssistant.Api.Domain;

/// <summary>Role names stored in <c>users.role</c> and issued in the JWT <c>role</c> claim.</summary>
public static class Roles
{
    public const string Student = "student";
    public const string Bank = "bank";
    public const string Admin = "admin";
}
