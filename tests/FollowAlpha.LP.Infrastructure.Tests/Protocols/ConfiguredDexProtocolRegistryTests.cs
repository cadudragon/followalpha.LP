using FollowAlpha.LP.Infrastructure.Protocols;
using FluentAssertions;
using Xunit;

namespace FollowAlpha.LP.Infrastructure.Tests.Protocols;

public class ConfiguredDexProtocolRegistryTests
{
    private static ConfiguredDexProtocolRegistry Registry() => new(DefaultDexProtocols.UniswapV3);

    [Fact]
    public void Defaults_cover_arbitrum_and_base_with_recorded_subgraph_ids()
    {
        var registry = Registry();

        registry.All.Should().HaveCount(2);
        registry.GetByChain("arbitrum").SubgraphId.Should().Be("FQ6JYszEKApsBpAmiHesRsd9Ygc6mzmpNRANeVQFYoVX");
        registry.GetByChain("base").SubgraphId.Should().Be("FUbEPQw1oMghy39fwWBFY5fE6MXPXZQtjncQy2cXdrNS");
    }

    [Fact]
    public void Descriptors_record_provenance_for_revalidation()
    {
        var arbitrum = Registry().GetByChain("arbitrum");
        arbitrum.Source.Should().NotBeNullOrWhiteSpace();
        arbitrum.RecordedOnUtc.Should().Be(new DateOnly(2026, 6, 16));
        arbitrum.FeeTiers.Should().Contain([100, 500, 3000, 10000]);
    }

    [Fact]
    public void Lookup_is_case_insensitive()
    {
        Registry().GetByChain("ARBITRUM").ChainId.Should().Be("arbitrum");
    }

    [Fact]
    public void Unknown_chain_throws_on_get_and_returns_null_on_find()
    {
        var registry = Registry();
        var act = () => registry.GetByChain("ethereum");
        act.Should().Throw<KeyNotFoundException>();
        registry.FindByChain("ethereum").Should().BeNull();
    }
}
