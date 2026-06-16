using FollowAlpha.LP.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FollowAlpha.LP.Infrastructure.Persistence.Configurations;

internal sealed class TickLiquiditySnapshotConfiguration : IEntityTypeConfiguration<TickLiquiditySnapshot>
{
    public void Configure(EntityTypeBuilder<TickLiquiditySnapshot> builder)
    {
        builder.HasKey(e => new { e.TenantId, e.PoolId, e.AsOfUtc, e.Tick });
        builder.HasOne<Pool>().WithMany().HasForeignKey(e => e.PoolId).OnDelete(DeleteBehavior.Restrict);
    }
}
