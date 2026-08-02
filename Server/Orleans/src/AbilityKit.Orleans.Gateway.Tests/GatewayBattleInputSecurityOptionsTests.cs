using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Orleans.Gateway.Core;
using AbilityKit.Orleans.Gateway.Handlers;
using AbilityKit.Orleans.Gateway.HttpApi;
using AbilityKit.Orleans.Gateway.Networking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace AbilityKit.Orleans.Gateway.Tests;

public sealed class GatewayBattleInputSecurityOptionsTests
{
    [Fact]
    public void GatewayModule_BindsOptionsAndCreatesSingletonGuard()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [$"{BattleInputSecurityOptions.ConfigurationSection}:ReplayWindowSize"] = "4",
            [$"{BattleInputSecurityOptions.ConfigurationSection}:MaxGatewayTrackedKeys"] = "8",
            [$"{BattleInputSecurityOptions.ConfigurationSection}:GatewayIdleStateTtlSeconds"] = "30"
        });

        var options = provider.GetRequiredService<IOptions<BattleInputSecurityOptions>>().Value;
        var first = provider.GetRequiredService<GatewayBattleInputGuard>();
        var second = provider.GetRequiredService<GatewayBattleInputGuard>();

        Assert.Equal(4, options.ReplayWindowSize);
        Assert.Equal(8, options.MaxGatewayTrackedKeys);
        Assert.Equal(30, options.GatewayIdleStateTtlSeconds);
        Assert.Same(first, second);
        Assert.Equal(4, first.Options.ReplayWindowSize);
    }

    [Fact]
    public void GatewayModule_RegistersSharedSessionRegistryAndGatewayRuntime()
    {
        var services = BuildServices(new Dictionary<string, string?>());
        using var provider = services.BuildServiceProvider();

        var concreteRegistry = provider.GetRequiredService<GatewaySessionRegistry>();
        var registry = provider.GetRequiredService<IGatewaySessionRegistry>();
        var secondRegistry = provider.GetRequiredService<IGatewaySessionRegistry>();
        var binder = provider.GetRequiredService<GatewaySessionBinder>();
        var backgroundTasks = provider.GetRequiredService<GatewayBackgroundTaskQueue>();
        var backgroundTaskDescriptor = services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
            .Single(descriptor => descriptor.ImplementationFactory is not null);
        var hostedBackgroundTasks = backgroundTaskDescriptor.ImplementationFactory!(provider);

        Assert.Same(concreteRegistry, registry);
        Assert.Same(registry, secondRegistry);
        Assert.NotNull(binder);
        Assert.Same(backgroundTasks, hostedBackgroundTasks);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType == typeof(TcpTransportHostedService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(TcpTransportServer));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IGatewayTransportEvents));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IGatewayRequestRouter));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IGatewayHandlerRegistry));
    }

    [Fact]
    public void GatewayModule_BindsCanonicalTcpTransportOptions()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["AbilityKit:Gateway:Tcp:Enabled"] = "true",
            ["AbilityKit:Gateway:Tcp:Host"] = "127.0.0.1",
            ["AbilityKit:Gateway:Tcp:Port"] = "14000",
            ["AbilityKit:Gateway:Tcp:RequestTimeoutMs"] = "4321"
        });

        var options = provider.GetRequiredService<IOptions<TcpTransportOptions>>().Value;

        Assert.True(options.Enabled);
        Assert.Equal("127.0.0.1", options.Host);
        Assert.Equal(14000, options.Port);
        Assert.Equal(4321, options.RequestTimeoutMs);
    }

    [Fact]
    public void GatewayModule_InvalidOptionsThrowBeforeGuardIsCreated()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [$"{BattleInputSecurityOptions.ConfigurationSection}:MaxGatewayTrackedKeys"] = "0"
        });

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<GatewayBattleInputGuard>());

        Assert.Contains(nameof(BattleInputSecurityOptions.MaxGatewayTrackedKeys), exception.Message);
    }

    private static ServiceProvider BuildProvider(IReadOnlyDictionary<string, string?> values)
    {
        return BuildServices(values).BuildServiceProvider();
    }

    private static ServiceCollection BuildServices(IReadOnlyDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAbilityKitGatewayModule(configuration);
        return services;
    }
}
