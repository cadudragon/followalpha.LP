using FollowAlpha.LP.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FollowAlpha.LP.Infrastructure.Persistence.Configurations;

internal sealed class BacktestRunConfiguration : IEntityTypeConfiguration<BacktestRun>
{
    public void Configure(EntityTypeBuilder<BacktestRun> builder)
    {
        builder.HasKey(e => e.Id);
    }
}
