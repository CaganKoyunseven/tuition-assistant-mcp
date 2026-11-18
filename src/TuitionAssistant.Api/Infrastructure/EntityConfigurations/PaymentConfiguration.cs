using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TuitionAssistant.Api.Domain;

namespace TuitionAssistant.Api.Infrastructure.EntityConfigurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments", table =>
            table.HasCheckConstraint("ck_payments_amount_positive", "amount_cents > 0"));
        builder.HasKey(p => p.Id);

        builder.Property(p => p.TransactionId).HasMaxLength(64);
        builder.Property(p => p.StudentNo).HasMaxLength(8);
        builder.Property(p => p.Term).HasMaxLength(16);
        builder.Property(p => p.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(p => p.Channel).HasMaxLength(16);
        builder.Property(p => p.PerformedBy).HasMaxLength(64);

        // Duplicate transaction IDs are rejected by the database even under concurrent requests.
        builder.HasIndex(p => p.TransactionId).IsUnique();
        builder.HasIndex(p => p.StudentNo);

        builder.HasOne(p => p.TuitionRecord)
            .WithMany(r => r.Payments)
            .HasForeignKey(p => p.TuitionRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
