using System.Security.Claims;
using TuitionAssistant.Api.Contracts;
using TuitionAssistant.Api.Domain;
using TuitionAssistant.Api.Errors;
using TuitionAssistant.Api.Security;

namespace TuitionAssistant.Api.Tests.Auth;

public class StudentAccessTests
{
    private static ClaimsPrincipal User(string role, string? studentNo = null)
    {
        var claims = new List<Claim> { new(TuitionClaims.Subject, studentNo ?? role), new(TuitionClaims.Role, role) };
        if (studentNo is not null)
        {
            claims.Add(new Claim(TuitionClaims.StudentNo, studentNo));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test", TuitionClaims.Subject, TuitionClaims.Role));
    }

    [Fact]
    public void Bank_CanAccessAnyStudent()
    {
        StudentAccess.EnsureCanAccessStudent(User(Roles.Bank), "20230002");
    }

    [Fact]
    public void Student_CanAccessOwnRecords()
    {
        StudentAccess.EnsureCanAccessStudent(User(Roles.Student, "20230001"), "20230001");
    }

    [Fact]
    public void Student_AccessingAnotherStudent_Throws403NotOwner()
    {
        var ex = Assert.Throws<ApiException>(() =>
            StudentAccess.EnsureCanAccessStudent(User(Roles.Student, "20230001"), "20230002"));

        Assert.Equal(403, ex.Status);
        Assert.Equal(ErrorCodes.ForbiddenNotOwner, ex.Code);
    }

    [Fact]
    public void StudentTokenWithoutStudentNoClaim_IsDenied()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(TuitionClaims.Role, Roles.Student)], "test", TuitionClaims.Subject, TuitionClaims.Role));

        Assert.Throws<ApiException>(() => StudentAccess.EnsureCanAccessStudent(principal, "20230001"));
    }

    [Fact]
    public void Admin_AccessingStudentRecords_Throws403Forbidden()
    {
        var ex = Assert.Throws<ApiException>(() => StudentAccess.EnsureCanAccessStudent(User(Roles.Admin), "20230001"));

        Assert.Equal(ErrorCodes.Forbidden, ex.Code);
    }
}
