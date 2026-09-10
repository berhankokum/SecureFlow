using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureFlow.Modules.Outbox.Domain;
namespace SecureFlow.Modules.Outbox.Infrastructure;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("OutboxMessages", t => { t.HasCheckConstraint("CK_OutboxMessages_AttemptCount", "\"AttemptCount\" >= 0"); t.HasCheckConstraint("CK_OutboxMessages_SchemaVersion", "\"SchemaVersion\" > 0"); });
        b.HasKey(x => x.Id); b.Property(x => x.Type).HasMaxLength(100).IsRequired(); b.Property(x => x.Payload).HasColumnType("jsonb").IsRequired(); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.LastErrorCode).HasMaxLength(100); b.HasIndex(x => new { x.Status, x.NextAttemptAt, x.OccurredAt });
    }
}
