namespace AbilityKit.Orleans.Gateway.HttpApi;

using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Orleans.Gateway.Core;
using AbilityKit.Orleans.Gateway.Generated;
using AbilityKit.Orleans.Gateway.Handlers;
using AbilityKit.Orleans.Gateway.Networking;
using AbilityKit.Orleans.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public static class GatewayModuleExtensions
{
    public static IServiceCollection AddAbilityKitGatewayModule(this IServiceCollection services, IConfiguration configuration)
    {
        var gatewaySection = configuration.GetSection(AbilityKitServerConfigurationSections.Gateway);
        var tcpSection = gatewaySection.GetSection("Tcp");

        services.AddOptions<AbilityKitGatewayOptions>()
            .Bind(gatewaySection);
        services.AddOptions<GatewayOptions>()
            .Bind(tcpSection);
        services.AddOptions<TcpTransportOptions>()
            .Bind(tcpSection);
        services.AddOptions<BattleInputSecurityOptions>()
            .Bind(configuration.GetSection(BattleInputSecurityOptions.ConfigurationSection))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<BattleInputSecurityOptions>, BattleInputSecurityOptionsValidator>();
        services.AddSingleton(serviceProvider =>
            new GatewayBattleInputGuard(
                serviceProvider.GetRequiredService<IOptions<BattleInputSecurityOptions>>().Value));
        services.AddSingleton<GatewaySessionRegistry>();
        services.AddSingleton<IGatewaySessionRegistry>(serviceProvider =>
            serviceProvider.GetRequiredService<GatewaySessionRegistry>());
        services.AddSingleton<GatewaySessionBinder>();
        services.AddSingleton<GatewayRoomMembershipService>();
        services.AddSingleton<GatewayFrameSyncSubscriptionManager>();
        services.AddSingleton<GatewayStateSyncPushSubscriptionManager>();

        services.AddSingleton<GatewayHandlerRegistry>(serviceProvider =>
        {
            var registry = new GatewayHandlerRegistry(serviceProvider);
            GeneratedGatewayHandlerRegistration.RegisterGeneratedGatewayHandlers(registry, serviceProvider);
            return registry;
        });
        services.AddSingleton<IGatewayHandlerRegistry>(serviceProvider =>
            serviceProvider.GetRequiredService<GatewayHandlerRegistry>());
        GeneratedGatewayHandlerRegistration.AddGeneratedGatewayHandlers(services);

        services.AddSingleton<GatewayRequestRouter>();
        services.AddSingleton<IGatewayRequestRouter>(serviceProvider =>
            serviceProvider.GetRequiredService<GatewayRequestRouter>());

        services.AddSingleton<GatewayBackgroundTaskQueue>();
        services.AddHostedService(serviceProvider =>
            serviceProvider.GetRequiredService<GatewayBackgroundTaskQueue>());
        services.AddSingleton<GatewayTransportHandler>();
        services.AddSingleton<IGatewayTransportEvents>(serviceProvider =>
            serviceProvider.GetRequiredService<GatewayTransportHandler>());
        services.AddSingleton<TcpTransportServer>();
        services.AddHostedService<TcpTransportHostedService>();

        services.AddSingleton<GatewayPushTargetGrain>();
        services.AddSingleton<IGatewayPushTargetGrain>(serviceProvider =>
            serviceProvider.GetRequiredService<GatewayPushTargetGrain>());

        services.AddGatewayHttpApi();
        return services;
    }

    public static WebApplication MapAbilityKitGatewayPipeline(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AbilityKitGatewayOptions>>().Value;
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapAbilityKitGatewayHealthEndpoints(options);
        app.MapGatewayHttpApi();
        app.MapGet("/admin", () => Results.Redirect("/admin/index.html"))
            .WithName("Gateway.AdminConsole")
            .Produces(StatusCodes.Status302Found);
        app.MapGet("/debug", () => Results.Redirect("/debug/index.html"))
            .WithName("Gateway.DebugConsole")
            .Produces(StatusCodes.Status302Found);
        return app;
    }

    private sealed class BattleInputSecurityOptionsValidator : IValidateOptions<BattleInputSecurityOptions>
    {
        public ValidateOptionsResult Validate(string? name, BattleInputSecurityOptions options)
        {
            var failures = BattleInputSecurityOptions.GetValidationFailures(options);
            return failures.Count == 0
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(failures);
        }
    }
}
