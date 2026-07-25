extern alias Gateway;

using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Grains.Battle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using GatewayAbstractions = Gateway::AbilityKit.Orleans.Gateway.Abstractions;
using GatewayCore = Gateway::AbilityKit.Orleans.Gateway.Core;
using GatewayGenerated = Gateway::AbilityKit.Orleans.Gateway.Generated;
using GatewayHandlers = Gateway::AbilityKit.Orleans.Gateway.Handlers;
using GatewayNetworking = Gateway::AbilityKit.Orleans.Gateway.Networking;

internal static class ShooterSmokeGatewayServiceCollectionExtensions
{
    public static IServiceCollection AddShooterSmokeGateway(this IServiceCollection services, int tcpGatewayPort)
    {
        services.Configure<GatewayCore.GatewayOptions>(options =>
        {
            options.RequestTimeoutMs = 30000;
            options.MaxFrameLength = 1024 * 1024;
        });

        services.Configure<GatewayNetworking.TcpTransportOptions>(options =>
        {
            options.Enabled = true;
            options.Host = "127.0.0.1";
            options.Port = tcpGatewayPort;
            options.RequestTimeoutMs = 5000;
            options.MaxFrameLength = 1024 * 1024;
        });

        services.AddSingleton<GatewayCore.GatewaySessionRegistry>();
        services.AddSingleton<GatewayAbstractions.IGatewaySessionRegistry>(sp => sp.GetRequiredService<GatewayCore.GatewaySessionRegistry>());
        services.AddSingleton<GatewayCore.GatewaySessionBinder>();
        services.AddSingleton<GatewayCore.GatewayFrameSyncSubscriptionManager>();
        services.AddSingleton(sp =>
            new GatewayHandlers.GatewayBattleInputGuard(
                sp.GetRequiredService<IOptions<BattleInputSecurityOptions>>().Value));

        services.AddSingleton<GatewayCore.GatewayHandlerRegistry>(sp =>
        {
            var registry = new GatewayCore.GatewayHandlerRegistry(sp);
            GatewayGenerated.GeneratedGatewayHandlerRegistration.RegisterGeneratedGatewayHandlers(registry, sp);
            return registry;
        });
        services.AddSingleton<GatewayAbstractions.IGatewayHandlerRegistry>(sp => sp.GetRequiredService<GatewayCore.GatewayHandlerRegistry>());

        GatewayGenerated.GeneratedGatewayHandlerRegistration.AddGeneratedGatewayHandlers(services);

        services.AddSingleton<GatewayCore.GatewayRequestRouter>();
        services.AddSingleton<GatewayAbstractions.IGatewayRequestRouter>(sp => sp.GetRequiredService<GatewayCore.GatewayRequestRouter>());

        services.AddSingleton<GatewayCore.GatewayBackgroundTaskQueue>();
        services.AddHostedService(sp => sp.GetRequiredService<GatewayCore.GatewayBackgroundTaskQueue>());
        services.AddSingleton<GatewayAbstractions.IGatewayTransportEvents, GatewayCore.GatewayTransportHandler>();
        services.AddSingleton<GatewayCore.GatewayTransportHandler>();
        services.AddSingleton<GatewayNetworking.TcpTransportServer>();

        services.AddSingleton<GatewayCore.GatewayPushTargetGrain>();
        services.AddSingleton<AbilityKit.Orleans.Contracts.Battle.IGatewayPushTargetGrain>(sp => sp.GetRequiredService<GatewayCore.GatewayPushTargetGrain>());
        return services;
    }
}
