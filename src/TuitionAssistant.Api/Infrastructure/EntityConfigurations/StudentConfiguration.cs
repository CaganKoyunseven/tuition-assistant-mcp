using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TuitionAssistant.Api.Domain;

namespace TuitionAssistant.Api.Infrastructure.EntityConfigurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("students");
        builder.HasKey(s => s.StudentNo);
        builder.Property(s => s.StudentNo).HasMaxLength(8);
        builder.Property(s => s.FullName).HasMaxLength(120);

        builder.HasMany(s => s.TuitionRecords)
            .WithOne(r => r.Student)
            .HasForeignKey(r => r.StudentNo)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
