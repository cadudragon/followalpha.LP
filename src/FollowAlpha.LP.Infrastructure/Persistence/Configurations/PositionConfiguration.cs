using FollowAlpha.LP.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FollowAlpha.LP.Infrastructure.Persistence.Configurations;

internal sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasOne<Wallet>().WithMany().HasForeignKey(e => e.WalletId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Pool>().WithMany().HasForeignKey(e => e.PoolId).OnDelete(DeleteBehavior.Restrict);
    }
}
