namespace TuitionAssistant.Api.Security;

/// <summary>JWT claim names (kept short and unmapped, exactly as they appear in the token).</summary>
public static class TuitionClaims
{
    public const string Subject = "sub";
    public const string Role = "role";
    public const string StudentNo = "student_no";
    public const string TokenId = "jti";
}
