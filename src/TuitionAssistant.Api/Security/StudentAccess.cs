using System.Security.Claims;
using TuitionAssistant.Api.Contracts;
using TuitionAssistant.Api.Domain;
using TuitionAssistant.Api.Errors;

namespace TuitionAssistant.Api.Security;

/// <summary>
/// Resource-level ownership check. The student number in the URL or body is never trusted on its own:
/// a student token may only access the student number in its own <c>student_no</c> claim.
/// This is what stops the AI agent (which uses the student's token) from reading other students' data.
/// </summary>
public static class StudentAccess
{
    public static void EnsureCanAccessStudent(ClaimsPrincipal user, string studentNo)
    {
        if (user.IsInRole(Roles.Bank))
        {
            return;
        }

        if (user.IsInRole(Roles.Student))
        {
            if (string.Equals(user.FindFirstValue(TuitionClaims.StudentNo), studentNo, StringComparison.Ordinal))
            {
                return;
            }

            throw ApiException.Forbidden("Students can only access their own tuition records.", ErrorCodes.ForbiddenNotOwner);
        }

        throw ApiException.Forbidden("This role is not allowed to access student tuition records.");
    }

    /// <summary>The caller's identity for auditing (<c>payments.performed_by</c>).</summary>
    public static string Subject(ClaimsPrincipal user) => user.FindFirstValue(TuitionClaims.Subject) ?? "unknown";
}
