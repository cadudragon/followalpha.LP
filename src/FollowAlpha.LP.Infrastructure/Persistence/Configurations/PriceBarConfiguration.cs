using FollowAlpha.LP.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FollowAlpha.LP.Infrastructure.Persistence.Configurations;

internal sealed class PriceBarConfiguration : IEntityTypeConfiguration<PriceBar>
{
    public void Configure(EntityTypeBuilder<PriceBar> builder)
    {
        // Composite natural key = primary key (scoped by TenantId).
        builder.HasKey(e => new { e.TenantId, e.AssetId, e.Resolution, e.OpenTimeUtc });
        builder.HasOne<Asset>().WithMany().HasForeignKey(e => e.AssetId).OnDelete(DeleteBehavior.Restrict);
    }
}
