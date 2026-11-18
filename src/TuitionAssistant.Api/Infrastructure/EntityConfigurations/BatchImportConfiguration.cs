using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TuitionAssistant.Api.Domain;

namespace TuitionAssistant.Api.Infrastructure.EntityConfigurations;

public class BatchImportConfiguration : IEntityTypeConfiguration<BatchImport>
{
    public void Configure(EntityTypeBuilder<BatchImport> builder)
    {
        builder.ToTable("batch_imports");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.UploadedBy).HasMaxLength(64);
        builder.Property(b => b.FileName).HasMaxLength(200);
    }
}
