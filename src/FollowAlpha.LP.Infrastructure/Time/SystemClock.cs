using FollowAlpha.LP.Application.Abstractions;

namespace FollowAlpha.LP.Infrastructure.Time;

/// <summary>The real clock (<see cref="IClock"/>). Lives outside the Domain, which never reads the clock.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
