namespace TuitionAssistant.Api.Domain;

/// <summary>
/// A login identity. Students use their student number as username; the banking app is a
/// machine client whose client secret hash is stored in <see cref="PasswordHash"/>.
/// </summary>
public class User
{
    public Guid Id { get; set; }

    public required string Username { get; set; }

    /// <summary>BCrypt hash of the password or client secret.</summary>
    public required string PasswordHash { get; set; }

    /// <summary>One of <see cref="Roles"/>.</summary>
    public required string Role { get; set; }

    /// <summary>Set only for users with role <c>student</c>.</summary>
    public string? StudentNo { get; set; }

    public bool IsActive { get; set; } = true;

    public Student? Student { get; set; }
}
