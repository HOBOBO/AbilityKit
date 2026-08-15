using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Pooling;

namespace AbilityKit.Samples.Foundation;

/// <summary>
/// Foundation 示例的业务模块：演示如何通过 <see cref="IWorldModule"/>
/// 把服务注册进世界容器，并由 <c>WorldModulePlanner</c> 按依赖排序装配。
/// </summary>
public sealed class GameplayModule : IWorldModule, IWorldModuleInfo
{
    public string Id => "gameplay";

    public int Order => 10;

    public Type[] DependsOn => Array.Empty<Type>();

    public Type[] ConflictsWith => Array.Empty<Type>();

    public void Configure(WorldContainerBuilder builder)
    {
        // 事件分发器：实例注册为单例，世界内服务与外部订阅方共用同一实例。
        builder.RegisterInstance(new EventDispatcher());

        // 生成系统：容器工厂创建，构造注入事件分发器；OnInit/OnDeinit 由容器自动调用。
        builder.RegisterType<ISpawnSystem, PooledSpawnSystem>(WorldLifetime.Singleton);

        // 根驱动服务：FoundationWorld.Tick 每帧调用，由它驱动其余系统。
        builder.Register<IFoundationTickLoop>(
            WorldLifetime.Singleton,
            r => new FoundationTickLoop(r.Resolve<ISpawnSystem>()));
    }
}

/// <summary>示例事件 id。字符串 id 由 dispatcher 内部的 StableStringIdRegistry 稳定编号。</summary>
public static class SampleEventIds
{
    public const string EntitySpawned = "sample.entity.spawned";
}

/// <summary>实体生成事件。readonly struct 发布零分配，不需要入池。</summary>
public readonly struct EntitySpawnedEvent
{
    public readonly int EntityId;
    public readonly float PositionX;

    public EntitySpawnedEvent(int entityId, float positionX)
    {
        EntityId = entityId;
        PositionX = positionX;
    }
}

/// <summary>池化实体：由 core.Pooling 的对象池租借/归还。</summary>
public sealed class PooledEntity
{
    public int Id;
    public float X;
    public int RemainingLifetime;
}

public interface ISpawnSystem : IService
{
    int ActiveCount { get; }

    int SpawnedTotal { get; }

    void Tick(float deltaTime);
}

/// <summary>
/// 演示 core.Pooling + core.Eventing 与 world.di 生命周期钩子的组合：
/// 实体从对象池租借，生成时发布事件，存活期结束归还池。
/// </summary>
public sealed class PooledSpawnSystem : ISpawnSystem, IWorldInitializable, IWorldDeinitializable
{
    private const int SpawnIntervalFrames = 3;
    private const int EntityLifetimeFrames = 5;

    private readonly EventDispatcher _events;
    private readonly List<PooledEntity> _active = new(16);
    private ObjectPool<PooledEntity> _pool = null!;
    private int _nextEntityId;
    private int _spawnCounter;

    public PooledSpawnSystem(EventDispatcher events)
    {
        _events = events;
    }

    public int ActiveCount => _active.Count;

    public int SpawnedTotal { get; private set; }

    public void OnInit(IWorldResolver services)
    {
        _pool = Pools.GetPool(() => new PooledEntity(), defaultCapacity: 8);
        Log.Info("[SpawnSystem] OnInit —— 对象池就绪（容量 8）");
    }

    public void Tick(float deltaTime)
    {
        _spawnCounter++;
        if (_spawnCounter >= SpawnIntervalFrames)
        {
            _spawnCounter = 0;

            var entity = _pool.Get();
            entity.Id = ++_nextEntityId;
            entity.X = _nextEntityId * 1.5f;
            entity.RemainingLifetime = EntityLifetimeFrames;
            _active.Add(entity);
            SpawnedTotal++;

            _events.Publish(SampleEventIds.EntitySpawned, new EntitySpawnedEvent(entity.Id, entity.X));
        }

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var entity = _active[i];
            if (--entity.RemainingLifetime > 0)
            {
                continue;
            }

            int id = entity.Id;
            _active.RemoveAt(i);
            _pool.Release(entity);
            Log.Info($"[SpawnSystem] entity {id} 存活期结束，归还对象池");
        }
    }

    public void OnDeinit(IWorldResolver services)
    {
        Log.Info($"[SpawnSystem] OnDeinit —— 累计生成 {SpawnedTotal} 个实体，剩余存活 {_active.Count} 个已清理");
        _active.Clear();
    }

    public void Dispose()
    {
        // 释放逻辑已由 OnDeinit 覆盖；IService 继承 IDisposable 以支持非托管资源场景。
    }
}

/// <summary>根驱动服务：接入方世界 Tick 的入口，按需扩展为多系统编排。</summary>
public interface IFoundationTickLoop : IService
{
    void Tick(float deltaTime);
}

public sealed class FoundationTickLoop : IFoundationTickLoop
{
    private readonly ISpawnSystem _spawnSystem;

    public FoundationTickLoop(ISpawnSystem spawnSystem)
    {
        _spawnSystem = spawnSystem;
    }

    public void Tick(float deltaTime)
    {
        _spawnSystem.Tick(deltaTime);
    }

    public void Dispose()
    {
    }
}
