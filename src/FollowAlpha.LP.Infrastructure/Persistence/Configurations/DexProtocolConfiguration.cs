using FollowAlpha.LP.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FollowAlpha.LP.Infrastructure.Persistence.Configurations;

internal sealed class DexProtocolConfiguration : IEntityTypeConfiguration<DexProtocol>
{
    public void Configure(EntityTypeBuilder<DexProtocol> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasOne<Chain>().WithMany().HasForeignKey(e => e.ChainId).OnDelete(DeleteBehavior.Restrict);
    }
}
