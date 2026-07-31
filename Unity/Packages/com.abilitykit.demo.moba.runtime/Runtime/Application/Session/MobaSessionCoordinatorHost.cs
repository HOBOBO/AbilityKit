using System;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Combat.Collision;
using AbilityKit.Coordinator;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.BattleDemo;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Ability.World;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Demo.Moba.Worlds.Blueprints;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.CreateWorld;

namespace AbilityKit.Demo.Moba.Session
{
    public interface ILogicWorldSessionHost
    {
        HostRuntime HostRuntime { get; }
        IWorldHost CreateLogicWorldHost(string worldType = null);
        void ConfigureLogicWorldOptions(WorldCreateOptions options, string worldType, string worldId);
        void RegisterLogicWorldServices(IWorld world);
        void LoadLogicWorldConfig(IWorld world);
        LogicWorldSpawnData[] CreateLogicWorldSpawnData(int localPlayerId);
        void SetPendingPlayerLoadouts(MobaPlayerLoadout[] loadouts);
    }

    /// <summary>
    /// MOBA 会话接入适配器（跨平台复用）。
    /// 负责把 coordinator 的通用会话宿主接口转换为 moba.runtime 的建世、配置注入和初始化数据注册流程。
    /// 房间、匹配、网络协议编排仍应放在 host.extension 或具体运行环境中。
    /// </summary>
    public sealed class MobaSessionCoordinatorHost : ILogicWorldSessionHost, ISessionCoordinatorHost, ISessionCoordinatorConfigPolicy
    {
        private readonly ITextAssetLoader _textAssetLoader;
        private readonly MobaSessionDefaults _defaults;
        private HostRuntime _hostRuntime;
        private LogicWorldSpawnData[] _pendingSpawns;
        private MobaPlayerLoadout[] _pendingLoadouts;

        public MobaSessionCoordinatorHost(ITextAssetLoader textAssetLoader)
            : this(textAssetLoader, null)
        {
        }

        public MobaSessionCoordinatorHost(ITextAssetLoader textAssetLoader, MobaSessionDefaults defaults)
        {
            _textAssetLoader = textAssetLoader;
            _defaults = MobaSessionDefaults.OrDefault(defaults);
        }

        public HostRuntime HostRuntime => _hostRuntime;

        public void ConfigureSession(ref SessionConfig config)
        {
            config.RequireLogicWorldDriveGate = true;
            config.UseCoordinatorSpawnService = false;
        }

        public void SetPendingSpawns(PlayerSpawnData[] spawns)
        {
            _pendingSpawns = ToLogicWorldSpawns(spawns);
        }

        public void SetPendingLogicWorldSpawns(LogicWorldSpawnData[] spawns)
        {
            _pendingSpawns = spawns;
        }

        public void SetPendingPlayerLoadouts(MobaPlayerLoadout[] loadouts)
        {
            _pendingLoadouts = loadouts;
        }

        public IWorldHost CreateWorldHost(SessionConfig config)
        {
            return CreateLogicWorldHost(config.WorldType);
        }

        public IWorldHost CreateLogicWorldHost(string worldType = null)
        {
            var worldTypeRegistry = new WorldTypeRegistry();
            MobaWorldBlueprintsRegistration.RegisterAll(worldTypeRegistry, options => new EntitasWorld(options));

            var worldManager = new WorldManager(new RegistryWorldFactory(worldTypeRegistry));
            _hostRuntime = new HostRuntime(worldManager, new HostRuntimeOptions());

            Log.Info("[MobaSessionCoordinatorHost] HostRuntime created with moba world blueprints");
            return _hostRuntime;
        }

        public void ConfigureWorldCreateOptions(in SessionConfig config, WorldCreateOptions options)
        {
            ConfigureLogicWorldOptions(
                options,
                config.WorldType,
                config.WorldId > 0 ? config.WorldId.ToString() : _defaults.WorldId);
            RegisterCreateWorldInitData(in config, options);
        }

        public void ConfigureLogicWorldOptions(WorldCreateOptions options, string worldType, string worldId)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            options.Id = new WorldId(_defaults.ResolveWorldId(worldId));
            options.WorldType = _defaults.ResolveWorldType(worldType);

            options.ServiceBuilder ??= WorldServiceContainerFactory.CreateDefaultOnly();

            if (_textAssetLoader != null)
            {
                options.ServiceBuilder.TryRegister<ITextAssetLoader>(
                    WorldLifetime.Singleton,
                    _ => _textAssetLoader);
            }

            options.ServiceBuilder.TryRegister<IMobaConfigTableRegistry>(
                WorldLifetime.Singleton,
                _ => MobaConfigRegistry.Instance);

            options.ServiceBuilder.TryRegister<ICollisionService>(
                WorldLifetime.Singleton,
                _ => new CollisionService(new CollisionWorldOptions { BroadphaseType = BroadphaseType.Grid, GridCellSize = 4f }));
        }

        public void RegisterServices(IWorld world, SessionConfig config)
        {
            RegisterLogicWorldServices(world);
            Log.Info($"[MobaSessionCoordinatorHost] World services ready, SyncMode={config.SyncMode}");
        }

        public void RegisterLogicWorldServices(IWorld world)
        {
            if (world?.Services == null)
            {
                return;
            }
        }

        public void LoadConfig(IWorld world, SessionConfig config)
        {
            LoadLogicWorldConfig(world);
        }

        public void LoadLogicWorldConfig(IWorld world)
        {
            if (world?.Services == null)
            {
                return;
            }

            if (world.Services.TryResolve<MobaConfigDatabase>(out var database))
            {
                Log.Info($"[MobaSessionCoordinatorHost] MobaConfigDatabase loaded, tables={database != null}");
            }
            else
            {
                Log.Warning("[MobaSessionCoordinatorHost] MobaConfigDatabase not resolved");
            }
        }

        public PlayerSpawnData[] CreatePlayerSpawnData(SessionConfig config)
        {
            if (!config.UseCoordinatorSpawnService)
            {
                return Array.Empty<PlayerSpawnData>();
            }

            var spawns = CreateLogicWorldSpawnData(config);
            return ToPlayerSpawnData(spawns);
        }

        public LogicWorldSpawnData[] CreateLogicWorldSpawnData(SessionConfig config)
        {
            return CreateLogicWorldSpawnData(config.LocalPlayerId);
        }

        public LogicWorldSpawnData[] CreateLogicWorldSpawnData(int localPlayerId)
        {
            return _pendingSpawns != null && _pendingSpawns.Length > 0
                ? _pendingSpawns
                : Array.Empty<LogicWorldSpawnData>();
        }

        private void RegisterCreateWorldInitData(in SessionConfig config, WorldCreateOptions options)
        {
            var loadouts = CreatePlayerLoadouts();
            if (loadouts == null || loadouts.Length == 0)
            {
                throw new InvalidOperationException("MobaSessionCoordinatorHost requires explicit MobaPlayerLoadout[] before world bootstrap.");
            }

            var localPlayerId = new PlayerId(config.LocalPlayerId.ToString());
            var spec = new MobaCreateWorldSpec(
                matchId: CreateMatchId(config),
                mapId: _defaults.ResolveMapId(config.MapId),
                randomSeed: CreateSessionSeed(config),
                tickRate: _defaults.ResolveTickRate(config.TickRate),
                inputDelayFrames: _defaults.InputDelayFrames,
                players: loadouts,
                gameplayId: 0);

            var validation = MobaProtocolValidation.ValidateCreateWorldSpec(localPlayerId, in spec);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException("MobaSessionCoordinatorHost create-world spec is invalid. " + validation);
            }

            var payload = new MobaCreateWorldInitPayload(localPlayerId, in spec, opCode: 0, payload: null);
            var initData = new WorldInitData(MobaWorldBootstrapModule.InitOpCode, MobaCreateWorldInitCodec.Serialize(in payload));

            options.ServiceBuilder.RegisterInstance(initData);
            Log.Info("[MobaSessionCoordinatorHost] Create-world init payload registered for bootstrap start flow");
        }

        private string CreateMatchId(SessionConfig config)
        {
            return config.SessionId.Value > 0 ? config.SessionId.ToString() : _defaults.MatchId;
        }

        private int CreateSessionSeed(SessionConfig config)
        {
            var value = config.SessionId.Value;
            if (value == 0)
            {
                value = config.RoomId != 0 ? config.RoomId : config.WorldId;
            }

            unchecked
            {
                var seed = (int)(value ^ (value >> 32));
                return _defaults.ResolveSeed(seed);
            }
        }

        private MobaPlayerLoadout[] CreatePlayerLoadouts()
        {
            return _pendingLoadouts != null && _pendingLoadouts.Length > 0
                ? _pendingLoadouts
                : Array.Empty<MobaPlayerLoadout>();
        }

        private static LogicWorldSpawnData[] ToLogicWorldSpawns(PlayerSpawnData[] spawns)
        {
            if (spawns == null || spawns.Length == 0)
            {
                return Array.Empty<LogicWorldSpawnData>();
            }

            var result = new LogicWorldSpawnData[spawns.Length];
            for (int i = 0; i < spawns.Length; i++)
            {
                var spawn = spawns[i];
                result[i] = new LogicWorldSpawnData(
                    spawn.PlayerId,
                    spawn.CharacterId,
                    spawn.TeamId,
                    spawn.X,
                    spawn.Y,
                    spawn.Z,
                    spawn.Name);
            }

            return result;
        }

        private static PlayerSpawnData[] ToPlayerSpawnData(LogicWorldSpawnData[] spawns)
        {
            if (spawns == null || spawns.Length == 0)
            {
                return Array.Empty<PlayerSpawnData>();
            }

            var result = new PlayerSpawnData[spawns.Length];
            for (int i = 0; i < spawns.Length; i++)
            {
                var spawn = spawns[i];
                result[i] = new PlayerSpawnData
                {
                    PlayerId = spawn.PlayerId,
                    CharacterId = spawn.CharacterId,
                    TeamId = spawn.TeamId,
                    X = spawn.X,
                    Y = spawn.Y,
                    Z = spawn.Z,
                    Name = spawn.Name
                };
            }

            return result;
        }
    }
}
