using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TuitionAssistant.Api.Domain;

namespace TuitionAssistant.Api.Infrastructure.EntityConfigurations;

public class DailyQueryCounterConfiguration : IEntityTypeConfiguration<DailyQueryCounter>
{
    public void Configure(EntityTypeBuilder<DailyQueryCounter> builder)
    {
        builder.ToTable("daily_query_counters");
        builder.HasKey(c => new { c.StudentNo, c.DayUtc });
        builder.Property(c => c.StudentNo).HasMaxLength(8);
    }
}
