using FollowAlpha.LP.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FollowAlpha.LP.Infrastructure.Persistence.Configurations;

internal sealed class DecisionAnnotationConfiguration : IEntityTypeConfiguration<DecisionAnnotation>
{
    public void Configure(EntityTypeBuilder<DecisionAnnotation> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.DecisionLogEntryId);
        builder.HasOne<DecisionLogEntry>().WithMany().HasForeignKey(e => e.DecisionLogEntryId).OnDelete(DeleteBehavior.Restrict);
    }
}
