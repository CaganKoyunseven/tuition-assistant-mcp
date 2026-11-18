using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TuitionAssistant.Api.Domain;

namespace TuitionAssistant.Api.Infrastructure.EntityConfigurations;

public class TuitionRecordConfiguration : IEntityTypeConfiguration<TuitionRecord>
{
    public void Configure(EntityTypeBuilder<TuitionRecord> builder)
    {
        builder.ToTable("tuition_records", table =>
        {
            table.HasCheckConstraint("ck_tuition_records_amount_non_negative", "amount_cents >= 0");
            table.HasCheckConstraint("ck_tuition_records_paid_range", "paid_cents >= 0 AND paid_cents <= amount_cents");
        });
        builder.HasKey(r => r.Id);

        builder.Property(r => r.StudentNo).HasMaxLength(8);
        builder.Property(r => r.Term).HasMaxLength(16);
        builder.Property(r => r.PaidCents).HasDefaultValue(0L);
        builder.Property(r => r.Currency).HasMaxLength(3).IsFixedLength().HasDefaultValue("USD");
        builder.Property(r => r.ConcurrencyStamp).IsConcurrencyToken();

        builder.Ignore(r => r.BalanceCents);
        builder.Ignore(r => r.Status);

        builder.HasIndex(r => new { r.StudentNo, r.Term }).IsUnique();
        builder.HasIndex(r => r.Term);
    }
}
