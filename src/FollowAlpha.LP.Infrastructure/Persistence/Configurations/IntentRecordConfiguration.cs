using FollowAlpha.LP.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FollowAlpha.LP.Infrastructure.Persistence.Configurations;

internal sealed class IntentRecordConfiguration : IEntityTypeConfiguration<IntentRecord>
{
    public void Configure(EntityTypeBuilder<IntentRecord> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.TenantId, e.PositionId });
        builder.HasOne<Position>().WithMany().HasForeignKey(e => e.PositionId).OnDelete(DeleteBehavior.Restrict);
        // Self-FK: a reclassification supersedes a prior record in the same chain.
        builder.HasOne<IntentRecord>().WithMany().HasForeignKey(e => e.SupersedesIntentRecordId).OnDelete(DeleteBehavior.Restrict);
    }
}
