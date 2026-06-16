using FollowAlpha.LP.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FollowAlpha.LP.Infrastructure.Persistence.Configurations;

internal sealed class DecisionLogEntryConfiguration : IEntityTypeConfiguration<DecisionLogEntry>
{
    public void Configure(EntityTypeBuilder<DecisionLogEntry> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.TenantId, e.PoolId });
        builder.HasOne<Pool>().WithMany().HasForeignKey(e => e.PoolId).OnDelete(DeleteBehavior.Restrict);
    }
}
