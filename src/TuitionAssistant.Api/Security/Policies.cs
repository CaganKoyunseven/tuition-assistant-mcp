using Microsoft.AspNetCore.Authorization;
using TuitionAssistant.Api.Domain;

namespace TuitionAssistant.Api.Security;

/// <summary>Authorization policies used by the controllers (see the README authorization matrix).</summary>
public static class Policies
{
    /// <summary>Banking routes: the bank app (any student) or a student (own records only, checked by <see cref="StudentAccess"/>).</summary>
    public const string RequireStudentOrBank = "RequireStudentOrBank";

    /// <summary>Admin routes: Add Tuition, batch import, unpaid report.</summary>
    public const string RequireAdmin = "RequireAdmin";

    /// <summary>Agent routes: only a signed-in student can chat and confirm payments.</summary>
    public const string RequireStudent = "RequireStudent";

    public static void AddTuitionPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(RequireStudentOrBank, policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Student, Roles.Bank));
        options.AddPolicy(RequireAdmin, policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Admin));
        options.AddPolicy(RequireStudent, policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Student));
    }
}
