using System;
using System.IO;
using System.Text.Json;
using AbilityKit.Ability.Share.Common.Config;
using AbilityKit.Ability.Share.Common.Reflection;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;
using Orleans.Hosting;

TryInstallWireSerializer();

void TryInstallWireSerializer()
{
    const string configFileName = "abilitykit.features.json";
    const string protocolWireSerializerModuleKey = "protocol.wire_serializer";

    var baseDir = AppContext.BaseDirectory;
    var path = string.IsNullOrEmpty(baseDir) ? configFileName : Path.Combine(baseDir, configFileName);

    var cfg = PersistentJsonConfigLoader.LoadOrDefault<ModuleInstallerConfigSet>(
        path,
        static json => JsonSerializer.Deserialize<ModuleInstallerConfigSet>(json));

    var module = cfg != null ? cfg.FindModule(protocolWireSerializerModuleKey) : null;
    if (module == null || !module.IsValid) return;

    ReflectionInvokeUtils.TryInvokeStaticMethod(module.InstallerType, module.GetEffectiveMethod());
}

var builder = Host.CreateApplicationBuilder(args);

builder.UseOrleans(silo =>
{
    silo.UseLocalhostClustering();
    silo.Configure<ClusterOptions>(options =>
    { 
        options.ClusterId = "abilitykit-dev";
        options.ServiceId = "abilitykit-orleans";
    });
});

var host = builder.Build();
await host.RunAsync();
