using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Logging;

namespace AbilityKit.Samples.Foundation;

/// <summary>
/// 最小可运行的世界实现。
/// world.di 只提供容器与抽象（IWorld / WorldManager / WorldContainerBuilder），
/// 具体的 IWorld 由接入方实现：持有 Build 出的 <see cref="WorldContainer"/>，
/// Initialize 时创建 scope，Tick 时驱动根服务，Dispose 时逆序释放。
/// </summary>
public sealed class FoundationWorld : IWorld
{
    private readonly WorldContainer _container;
    private WorldScope _scope = null!;
    private IFoundationTickLoop _tickLoop = null!;

    public FoundationWorld(WorldId id, string worldType, WorldContainer container)
    {
        Id = id;
        WorldType = worldType;
        _container = container;
    }

    public WorldId Id { get; }

    public string WorldType { get; }

    public IWorldResolver Services => _scope ?? (IWorldResolver)_container;

    public void Initialize()
    {
        _scope = _container.CreateScope();
        _tickLoop = _scope.Resolve<IFoundationTickLoop>();
        Log.Info($"[FoundationWorld] Initialize —— worldId={Id}, type={WorldType}, 服务数={_container.RegisteredServiceTypes.Count}");
    }

    public void Tick(float deltaTime)
    {
        _tickLoop?.Tick(deltaTime);
    }

    public void Dispose()
    {
        _scope?.Dispose();
        _container.Dispose();
    }
}
