using FollowAlpha.LP.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FollowAlpha.LP.Infrastructure.Persistence.Configurations;

internal sealed class PoolSnapshotConfiguration : IEntityTypeConfiguration<PoolSnapshot>
{
    public void Configure(EntityTypeBuilder<PoolSnapshot> builder)
    {
        builder.HasKey(e => new { e.TenantId, e.PoolId, e.AsOfUtc });
        builder.HasOne<Pool>().WithMany().HasForeignKey(e => e.PoolId).OnDelete(DeleteBehavior.Restrict);
    }
}
