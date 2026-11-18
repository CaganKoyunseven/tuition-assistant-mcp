using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TuitionAssistant.Api.Domain;

namespace TuitionAssistant.Api.Infrastructure.EntityConfigurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username).HasMaxLength(64);
        builder.Property(u => u.PasswordHash).HasMaxLength(100);
        builder.Property(u => u.Role).HasMaxLength(16);
        builder.Property(u => u.StudentNo).HasMaxLength(8);

        builder.HasIndex(u => u.Username).IsUnique();

        // A student has at most one login (unique index on the nullable FK).
        builder.HasOne(u => u.Student)
            .WithOne()
            .HasForeignKey<User>(u => u.StudentNo)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
