using Microsoft.Extensions.Hosting;
using AbilityKit.Orleans.Grains.Hello;
using Orleans.Configuration;
using Orleans.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.UseOrleans(silo =>
{
    silo.UseLocalhostClustering();
    silo.Configure<ClusterOptions>(options =>
    {
        options.ClusterId = "abilitykit-dev";
        options.ServiceId = "abilitykit-orleans";
    });

    silo.ConfigureApplicationParts(parts =>
    {
        parts.AddApplicationPart(typeof(HelloGrain).Assembly).WithReferences();
    });
});

var host = builder.Build();
await host.RunAsync();
