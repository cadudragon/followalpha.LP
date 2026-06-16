using FollowAlpha.LP.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FollowAlpha.LP.Infrastructure.Persistence.Configurations;

internal sealed class PoolConfiguration : IEntityTypeConfiguration<Pool>
{
    public void Configure(EntityTypeBuilder<Pool> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasOne<Chain>().WithMany().HasForeignKey(e => e.ChainId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DexProtocol>().WithMany().HasForeignKey(e => e.DexProtocolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Asset>().WithMany().HasForeignKey(e => e.Token0AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Asset>().WithMany().HasForeignKey(e => e.Token1AssetId).OnDelete(DeleteBehavior.Restrict);
    }
}
