using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Persistence;
using AbilityKit.Orleans.Grains.Gameplay;
using AbilityKit.Demo.Tiny.Server;
using AbilityKit.Orleans.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddAbilityKitServerOptions(builder.Configuration);
builder.Services.AddStateSyncObserverOptions(builder.Configuration);
builder.Services.AddBattleInputSecurityOptions(builder.Configuration);
builder.Services.AddAbilityKitDeploymentOptions(builder.Configuration);
builder.Services.AddAbilityKitSiloRoleOptions(builder.Configuration);
builder.Services.AddAbilityKitSiloRuntimeProfileOptions(builder.Configuration);
builder.Services.AddAbilityKitDeploymentModeOptions(builder.Configuration);
builder.Logging.AddAbilityKitServerLogging(builder.Configuration, "AbilityKit.Orleans.Host");

var storageOptions = builder.Configuration.GetAbilityKitStorageOptions();
builder.Services.AddAbilityKitGrainStateStorage(
    storageOptions.SessionStateProvider,
    storageOptions.RoomStateProvider,
    storageOptions.AllowInMemoryFallbackForUnsupportedProviders);

builder.Services.AddSingleton(_ =>
    ServerGameplayModuleCatalog.Default
        .WithModule(TinyServerGameplayModule.Create())
        .WithModule(TinyTurnGameplayModule.Create()));
builder.Services.AddSingleton<ServerBattleWorldManager>(sp =>
    new ServerBattleWorldManager(sp.GetRequiredService<ILogger<ServerBattleWorldManager>>(),
        sp.GetRequiredService<ServerGameplayModuleCatalog>()));

builder.UseAbilityKitLocalOrleansSilo();

var host = builder.Build();
await host.RunAsync();
