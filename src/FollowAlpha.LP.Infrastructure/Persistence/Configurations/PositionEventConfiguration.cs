using FollowAlpha.LP.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FollowAlpha.LP.Infrastructure.Persistence.Configurations;

internal sealed class PositionEventConfiguration : IEntityTypeConfiguration<PositionEvent>
{
    public void Configure(EntityTypeBuilder<PositionEvent> builder)
    {
        builder.HasKey(e => new { e.TenantId, e.ChainId, e.TxHash, e.LogIndex });
        builder.HasOne<Wallet>().WithMany().HasForeignKey(e => e.WalletId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Pool>().WithMany().HasForeignKey(e => e.PoolId).OnDelete(DeleteBehavior.Restrict);
    }
}
