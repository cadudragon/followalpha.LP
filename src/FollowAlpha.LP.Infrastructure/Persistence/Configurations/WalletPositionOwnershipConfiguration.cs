using FollowAlpha.LP.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FollowAlpha.LP.Infrastructure.Persistence.Configurations;

internal sealed class WalletPositionOwnershipConfiguration : IEntityTypeConfiguration<WalletPositionOwnership>
{
    public void Configure(EntityTypeBuilder<WalletPositionOwnership> builder)
    {
        // Working state, keyed by its natural identity (TenantId column, not part of the key — v1 realization).
        builder.HasKey(e => new { e.ChainId, e.WalletId, e.TokenId, e.Seq });
        builder.HasOne<Chain>().WithMany().HasForeignKey(e => e.ChainId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Wallet>().WithMany().HasForeignKey(e => e.WalletId).OnDelete(DeleteBehavior.Restrict);
    }
}
