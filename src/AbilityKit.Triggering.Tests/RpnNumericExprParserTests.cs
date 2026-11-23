using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Runtime.Plan;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class RpnNumericExprParserTests
{
    [Fact]
    public void Explicit_domain_resolution_preserves_stable_ids_for_registered_domain()
    {
        const string expression = "payload:amount bb:declared:hp +";
        var expected = RpnNumericExprParser.Parse(expression);
        var actual = RpnNumericExprParser.Parse(expression, blackboardDomainIdResolver: ResolveDeclaredDomain);

        Assert.Equal(expected, actual);
        Assert.Equal(StableStringId.Get("bb:declared:hp"), actual[1].Value.KeyId);
    }

    [Fact]
    public void Explicit_domain_resolution_rejects_domain_not_declared_by_host()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => RpnNumericExprParser.Parse("bb:unknown:hp", blackboardDomainIdResolver: ResolveDeclaredDomain));

        Assert.Contains("unknown", error.Message);
    }

    private static int ResolveDeclaredDomain(string domain)
        => string.Equals(domain, "declared", StringComparison.Ordinal)
            ? StableStringId.Get("bb:declared")
            : throw new InvalidOperationException($"未注册黑板域：{domain}");
}
