#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Host.WorldBlueprints;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.World
{
    /// <summary>
    /// WorldBlueprintUsage - 演示如何使用 WorldBlueprint 配置 World
    /// 通过 WorldBlueprintRegistry 实现 World 类型的标准化配置
    /// </summary>
    [Sample]
    public sealed class WorldBlueprintUsage : SampleBase
    {
        public override string Title => "World Blueprint";
        public override string Description => "使用 WorldBlueprint 和 BlueprintRegistry 标准化 World 配置";
        public override SampleCategory Category => SampleCategory.World;

        private HostRuntime _host;
        private WorldBlueprintRegistry _blueprintRegistry;
        private WorldTypeRegistry _registry;

        protected override void OnRun()
        {
            Log("=== World Blueprint 示例 ===");
            Output.Divider();

            // 1. WorldBlueprint 概念
            Log("【1】WorldBlueprint 概念");
            Output.Bullet("IWorldBlueprint - 定义特定类型 World 的标准配置");
            Output.Bullet("WorldBlueprintRegistry - 管理所有 Blueprint 的注册表");
            Output.Bullet("通过 WorldType 字符串关联 Blueprint 和 World");
            Output.Line();

            // 2. 创建 BlueprintRegistry
            Log("【2】创建 BlueprintRegistry 并注册 Blueprint");
            _blueprintRegistry = new WorldBlueprintRegistry();

            _blueprintRegistry.Register(new DelegateWorldBlueprint("LobbyWorld", options =>
            {
                options.Modules.Add(new LobbyWorldModule());
            }));

            _blueprintRegistry.Register(new DelegateWorldBlueprint("BattleWorld", options =>
            {
                options.Modules.Add(new BattleWorldModule());
            }));

            _blueprintRegistry.Register(new DelegateWorldBlueprint("TestWorld", options =>
            {
                options.Modules.Add(new TestWorldModule());
            }));

            Log("已注册 3 种 World Blueprint:");
            Log("  - LobbyWorld: 大厅世界模块");
            Log("  - BattleWorld: 战斗世界模块");
            Log("  - TestWorld: 测试世界模块");
            Output.Line();

            // 3. 创建 WorldTypeRegistry
            Log("【3】创建 WorldTypeRegistry");
            _registry = new WorldTypeRegistry();
            _registry.Register("LobbyWorld", options => CreateWorldFromOptions(options));
            _registry.Register("BattleWorld", options => CreateWorldFromOptions(options));
            _registry.Register("TestWorld", options => CreateWorldFromOptions(options));
            _registry.Register("CustomWorld", options => CreateWorldFromOptions(options));
            Log("WorldTypeRegistry 创建完成");
            Output.Line();

            // 4. 创建 WorldManager 和 HostRuntime
            Log("【4】创建 Host");
            var factory = new RegistryWorldFactory(_registry);
            var worldManager = new WorldManager(factory);
            _host = new HostRuntime(worldManager);
            Log("HostRuntime 创建完成");
            Output.Line();

            // 5. 通过 Blueprint 创建不同类型的 World
            Log("【5】通过 Blueprint 创建 World");
            Log("LobbyWorld:");
            var lobbyWorld = _host.CreateWorld(new WorldCreateOptions
            {
                Id = new WorldId("lobby-1"),
                WorldType = "LobbyWorld"
            });
            Log($"  创建: {lobbyWorld.Id.Value}, 类型: {lobbyWorld.WorldType}");

            Log("BattleWorld:");
            var battleWorld = _host.CreateWorld(new WorldCreateOptions
            {
                Id = new WorldId("battle-1"),
                WorldType = "BattleWorld"
            });
            Log($"  创建: {battleWorld.Id.Value}, 类型: {battleWorld.WorldType}");

            Log("TestWorld:");
            var testWorld = _host.CreateWorld(new WorldCreateOptions
            {
                Id = new WorldId("test-1"),
                WorldType = "TestWorld"
            });
            Log($"  创建: {testWorld.Id.Value}, 类型: {testWorld.WorldType}");

            Log($"\n当前 World 数量: {_host.Worlds.Worlds.Count}");
            Output.Line();

            // 6. 验证 Blueprint 配置的服务
            Log("【6】验证 Blueprint 配置的服务");
            Log("不同 WorldType 的 World 有不同的服务配置:");

            var lobbyServices = lobbyWorld.Services;
            var battleServices = battleWorld.Services;

            lobbyServices.TryResolve<ILobbyService>(out var lobbyService);
            battleServices.TryResolve<IBattleService>(out var battleService);

            Log($"LobbyWorld - ILobbyService: {lobbyService != null}");
            Log($"BattleWorld - IBattleService: {battleService != null}");

            Log("\n  Blueprint 成功为不同 World 类型注入了不同的服务");
            Output.Line();

            // 7. 使用匿名 Blueprint
            Log("【7】使用匿名 Blueprint");
            Log("通过 DelegateWorldBlueprint 可以快速创建一次性 Blueprint:");

            _blueprintRegistry.Register(new DelegateWorldBlueprint("CustomWorld", options =>
            {
                options.Modules.Add(new CustomWorldModule("CustomConfig"));
            }));

            var customWorld = _host.CreateWorld(new WorldCreateOptions
            {
                Id = new WorldId("custom-1"),
                WorldType = "CustomWorld"
            });

            Log($"CustomWorld 创建: {customWorld.Id.Value}");
            customWorld.Services.TryResolve<ICustomService>(out var customService);
            Log($"ICustomService: {customService != null}");
            if (customService != null)
            {
                Log($"CustomService.Config: {customService.Config}");
            }

            Output.Line();

            // 8. 清理
            Log("【8】清理资源");
            _host.Worlds.DisposeAll();
            Log("所有 World 已销毁");
            _blueprintRegistry = null;

            Output.Divider();
        }

        private IWorld CreateWorldFromOptions(WorldCreateOptions options)
        {
            var builder = new WorldContainerBuilder();

            // 基础服务
            builder.RegisterServiceType<IWorldClock, WorldClock>(WorldLifetime.Singleton);
            builder.RegisterServiceType<IWorldLogger, NullWorldLogger>(WorldLifetime.Singleton);

            // 应用 Blueprint 配置
            if (_blueprintRegistry != null)
            {
                _blueprintRegistry.Configure(options);
            }

            // 应用 Modules 配置
            foreach (var module in options.Modules)
            {
                module.Configure(builder);
            }

            var container = builder.Build();
            return new BlueprintWorldInstance(options.Id, options.WorldType, container);
        }
    }

    #region Demo Services

    public interface ILobbyService { }
    public interface IBattleService { }
    public interface ICustomService
    {
        string Config { get; }
    }

    public sealed class LobbyService : ILobbyService { }
    public sealed class BattleService : IBattleService { }

    public sealed class CustomService : ICustomService
    {
        public CustomService(string config)
        {
            Config = config;
        }

        public string Config { get; }
    }

    #endregion

    #region World Modules

    public sealed class LobbyWorldModule : IWorldModule
    {
        public void Configure(WorldContainerBuilder builder)
        {
            builder.RegisterServiceType<ILobbyService, LobbyService>(WorldLifetime.Singleton);
        }
    }

    public sealed class BattleWorldModule : IWorldModule
    {
        public void Configure(WorldContainerBuilder builder)
        {
            builder.RegisterServiceType<IBattleService, BattleService>(WorldLifetime.Singleton);
        }
    }

    public sealed class TestWorldModule : IWorldModule
    {
        public void Configure(WorldContainerBuilder builder)
        {
            // TestWorld 默认不需要特殊服务
        }
    }

    public sealed class CustomWorldModule : IWorldModule
    {
        private readonly string _config;

        public CustomWorldModule(string config)
        {
            _config = config;
        }

        public void Configure(WorldContainerBuilder builder)
        {
            builder.Register<ICustomService>(WorldLifetime.Singleton, r => new CustomService(_config));
        }
    }

    #endregion

    #region BlueprintWorldInstance

    public sealed class BlueprintWorldInstance : IWorld
    {
        private readonly WorldId _id;
        private readonly string _worldType;
        private readonly IWorldResolver _services;
        private bool _disposed;

        public BlueprintWorldInstance(WorldId id, string worldType, IWorldResolver services)
        {
            _id = id;
            _worldType = worldType;
            _services = services;
        }

        public WorldId Id => _id;
        public string WorldType => _worldType;
        public IWorldResolver Services => _services;

        public void Initialize() { }

        public void Tick(float deltaTime)
        {
            if (_services.TryResolve<IWorldClock>(out var clock))
            {
                clock.Tick(deltaTime);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_services is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    #endregion
}
