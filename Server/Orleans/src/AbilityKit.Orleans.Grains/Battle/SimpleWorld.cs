using System;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using WorldId = AbilityKit.Ability.World.Abstractions.WorldId;

namespace AbilityKit.Orleans.Grains.Battle;

/// <summary>
/// 服务器端简单的 World 实现
/// </summary>
public sealed class SimpleWorld : IWorld
{
    private readonly WorldId _id;
    private readonly string _worldType;
    private readonly IWorldResolver _services;
    private readonly IDisposable? _serviceScope;
    private readonly IDisposable? _serviceRoot;
    private bool _disposed;

    public SimpleWorld(WorldId id, string worldType, IWorldResolver services)
    {
        _id = id;
        _worldType = worldType;

        if (services is WorldContainer container)
        {
            var scope = container.CreateScope();
            _services = scope;
            _serviceScope = scope;
            _serviceRoot = container;
            return;
        }

        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public WorldId Id => _id;
    public string WorldType => _worldType;
    public IWorldResolver Services => _services;

    public void Initialize()
    {
    }

    public void Tick(float deltaTime)
    {
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _serviceScope?.Dispose();
        _serviceRoot?.Dispose();

        if (_serviceScope == null && _services is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

/// <summary>
/// 简单的 World 工厂
/// </summary>
public sealed class SimpleWorldFactory
{
    public IWorld Create(WorldCreateOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        if (options.TryGetEntitasContextsFactory(out _))
        {
            var world = new EntitasWorld(options);
            world.Initialize();
            return world;
        }

        options.ServiceBuilder ??= WorldServiceContainerFactory.CreateDefaultOnly();
        for (int i = 0; i < options.Modules.Count; i++)
        {
            var module = options.Modules[i];
            if (module == null) continue;
            options.ServiceBuilder.AddModule(module);
        }

        return new SimpleWorld(options.Id, options.WorldType, options.ServiceBuilder.Build());
    }
}
