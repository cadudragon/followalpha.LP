using FollowAlpha.LP.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FollowAlpha.LP.Infrastructure.Persistence.Configurations;

internal sealed class WalletSyncCursorConfiguration : IEntityTypeConfiguration<WalletSyncCursor>
{
    public void Configure(EntityTypeBuilder<WalletSyncCursor> builder)
    {
        builder.HasKey(e => new { e.ChainId, e.WalletId });
        builder.HasOne<Chain>().WithMany().HasForeignKey(e => e.ChainId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Wallet>().WithMany().HasForeignKey(e => e.WalletId).OnDelete(DeleteBehavior.Restrict);
    }
}
