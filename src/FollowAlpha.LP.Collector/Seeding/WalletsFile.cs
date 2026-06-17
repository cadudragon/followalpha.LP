using System.Text.Json;
using System.Text.Json.Serialization;

namespace FollowAlpha.LP.Collector.Seeding;

/// <summary>The audit wallets file (<c>config/wallets.json</c>): the LP-Audit targets (CLAUDE.md).</summary>
public sealed class WalletsFile
{
    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = "default";

    [JsonPropertyName("wallets")]
    public List<WalletEntry> Wallets { get; set; } = [];

    /// <summary>Loads the wallets file, resolving the path relative to the content root then walking up to
    /// the repo root (so a <c>dotnet run</c> from the host folder still finds <c>config/wallets.json</c>).
    /// Returns an empty file (not an error) when none is found — the wallet-sync job then logs and idles.</summary>
    public static WalletsFile LoadOrEmpty(string path, string contentRoot)
    {
        var resolved = Resolve(path, contentRoot);
        if (resolved is null)
        {
            return new WalletsFile();
        }

        var json = File.ReadAllText(resolved);
        return JsonSerializer.Deserialize<WalletsFile>(json) ?? new WalletsFile();
    }

    private static string? Resolve(string path, string contentRoot)
    {
        if (Path.IsPathRooted(path) && File.Exists(path))
        {
            return path;
        }

        var dir = new DirectoryInfo(contentRoot);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, path);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }
}

/// <summary>One wallet entry in the file.</summary>
public sealed class WalletEntry
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("chains")]
    public List<string> Chains { get; set; } = [];
}
