using FollowAlpha.LP.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FollowAlpha.LP.Infrastructure.Persistence.Configurations;

internal sealed class AlertRuleConfiguration : IEntityTypeConfiguration<AlertRule>
{
    public void Configure(EntityTypeBuilder<AlertRule> builder)
    {
        // TargetRef is polymorphic (Type + ref) — no foreign key.
        builder.HasKey(e => e.Id);
    }
}
