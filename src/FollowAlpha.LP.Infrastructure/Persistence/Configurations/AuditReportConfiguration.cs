using FollowAlpha.LP.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FollowAlpha.LP.Infrastructure.Persistence.Configurations;

internal sealed class AuditReportConfiguration : IEntityTypeConfiguration<AuditReport>
{
    public void Configure(EntityTypeBuilder<AuditReport> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasOne<Wallet>().WithMany().HasForeignKey(e => e.WalletId).OnDelete(DeleteBehavior.Restrict);
    }
}
