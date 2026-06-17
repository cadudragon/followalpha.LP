using System.Text.Json;
using FollowAlpha.LP.Application.Abstractions;
using FollowAlpha.LP.Application.ChainEvents;
using FollowAlpha.LP.Application.Collection;
using FollowAlpha.LP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FollowAlpha.LP.Collector.Jobs;

/// <summary>
/// Scheduled wallet event-sync job: for each seeded audit wallet and each chain it is tracked on, discovers
/// the wallet's position tokenIds and ingests their enriched mint/burn/collect events
/// (<see cref="SyncWalletPositionEvents"/>) over <c>[WalletSyncFromBlock, chainHead]</c>. Idempotent —
/// re-syncing an overlapping window inserts nothing new.
/// </summary>
public sealed class WalletSyncJob : CronBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<CollectorOptions> _options;
    private readonly CollectorHealth _health;

    public WalletSyncJob(
        IServiceScopeFactory scopeFactory,
        IOptions<CollectorOptions> options,
        IClock clock,
        CollectorHealth health,
        ILogger<WalletSyncJob> logger)
        : base(options.Value.WalletSyncCron, options.Value.RunJobsOnStartup, clock, logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _health = health;
    }

    protected override string JobName => "wallet-sync";

    protected override async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var eventReader = scope.ServiceProvider.GetRequiredService<IChainEventReader>();
        var useCase = scope.ServiceProvider.GetRequiredService<SyncWalletPositionEvents>();

        var wallets = await db.Wallets.AsNoTracking().ToListAsync(cancellationToken);
        if (wallets.Count == 0)
        {
            Logger.LogWarning("Wallet sync job has no seeded wallets; nothing to do.");
            return;
        }

        var headByChain = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        foreach (var wallet in wallets)
        {
            foreach (var chainId in ParseChains(wallet.Chains))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!headByChain.TryGetValue(chainId, out var head))
                    {
                        head = await eventReader.GetChainHeadBlockAsync(chainId, cancellationToken);
                        headByChain[chainId] = head;
                    }

                    var outcome = await useCase.RunAsync(
                        new WalletSyncRequest(
                            wallet.Id, wallet.Address, chainId,
                            _options.Value.WalletSyncFromBlock, head, _options.Value.WalletSyncReorgBuffer),
                        cancellationToken);

                    Logger.LogInformation(
                        "Wallet {Wallet} on {Chain} [{From}-{To}]: {Transfers} transfers, {Owned} owned tokenIds, {Read} events read, {Inserted} inserted, {Skipped} skipped (not owned).",
                        wallet.Id, chainId, outcome.FromBlock, outcome.ToBlock,
                        outcome.TransfersSeen, outcome.TokenIdsOwned, outcome.EventsRead, outcome.EventsInserted, outcome.EventsSkippedNotOwned);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Logger.LogError(ex, "Wallet {Wallet} sync on {Chain} failed.", wallet.Id, chainId);
                }
            }
        }

        _health.RecordRun(JobName, Now);
    }

    private static List<string> ParseChains(string chainsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(chainsJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
