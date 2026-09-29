using AbilityKit.Demo.Tiny.Server;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Gameplay;
using AbilityKit.Orleans.Grains.Persistence;
using AbilityKit.Orleans.Hosting;
using Microsoft.Extensions.Configuration;
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
builder.Logging.AddAbilityKitServerLogging(builder.Configuration, "TinyCustomHost");

var storage = builder.Configuration.GetAbilityKitStorageOptions();
builder.Services.AddAbilityKitGrainStateStorage(storage.SessionStateProvider,
    storage.RoomStateProvider, storage.AllowInMemoryFallbackForUnsupportedProviders);
builder.Services.AddSingleton(_ =>
{
    var catalog = ServerGameplayModuleCatalog.Default.WithModule(TinyServerGameplayModule.Create());
    if (builder.Configuration.GetValue("AbilityKit:Tiny:EnableTurn", true))
        catalog = catalog.WithModule(TinyTurnGameplayModule.Create());
    return ConfigureGameplay(catalog);
});
builder.Services.AddSingleton<ServerBattleWorldManager>(sp =>
    new ServerBattleWorldManager(sp.GetRequiredService<ILogger<ServerBattleWorldManager>>(),
        sp.GetRequiredService<ServerGameplayModuleCatalog>()));
builder.UseAbilityKitLocalOrleansSilo();

await builder.Build().RunAsync();

static ServerGameplayModuleCatalog ConfigureGameplay(ServerGameplayModuleCatalog catalog)
{
    // Register project modules here with catalog.WithModule(MyGameplayModule.Create()).
    return catalog;
}
