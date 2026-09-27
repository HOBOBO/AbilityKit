using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;
using AbilityKit.Coordinator;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Session;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using ET.AbilityKit.Demo.ET.Share;
using ActorKind = ET.AbilityKit.Demo.ET.Share.ActorKind;
using MobaFrameSnapshotData = AbilityKit.Demo.Moba.Share.FrameSnapshotData;

namespace ET.Logic
{
    /// <summary>
    /// ET battle host component and facade for the MOBA Runtime world.
    /// </summary>
    [ComponentOf(typeof(Scene))]
    public class ETMobaBattleDriver : Entity, IAwake, IUpdate, IDestroy
    {
        private readonly List<WorldStateSnapshot> _runtimeSnapshots = new List<WorldStateSnapshot>(32);
        private readonly List<PlayerInputCommand> _playerCommands = new List<PlayerInputCommand>(16);
        private readonly List<object> _bufferedInputCommands = new List<object>(16);

        public int CurrentFrame { get; set; }
        public double LogicTimeSeconds { get; set; }
        public int TickRate { get; set; } = 30;
        public bool IsRunning { get; set; }
        public IBattleViewEventSink ViewEventSink { get; set; }
        public BattleStartPlan Plan { get; set; }
        public bool RuntimeGameStarted { get; set; }

        public IWorldManager WorldManager { get; set; }
        public HostRuntime HostRuntime { get; set; }
        public IWorld World { get; set; }

        private MobaSessionCoordinatorHost _sessionHost;
        private MobaBattleDriverHost _driverHost;

        private IBattleViewEventSink _viewSink;
        public IBattleViewEventSink ViewSink
        {
            get => _viewSink;
            set
            {
                _viewSink = value;
                ViewEventSink = value;
            }
        }

        public ITextAssetLoader TextAssetLoader { get; set; }

        public List<ETPlayerSpawnData> PlayerSpawnData { get; set; } = new List<ETPlayerSpawnData>();

        public FrameSnapshotDispatcher SnapshotDispatcher { get; set; }
        public ActorSpawnData[] LastActorSpawnSnapshot { get; private set; } = Array.Empty<ActorSpawnData>();
        public ActorTransformData[] LastActorTransformSnapshot { get; private set; } = Array.Empty<ActorTransformData>();
        public StateHashData LastStateHashSnapshot { get; private set; }

        public Dictionary<int, ETUnit> Units { get; } = new Dictionary<int, ETUnit>();

        public double LastTickTime { get; set; }

        public void Awake()
        {
        }

        public void Update(ETMobaBattleDriver self)
        {
        }

        public void OnDestroy(ETMobaBattleDriver self)
        {
        }

        public void Initialize(in BattleStartPlan plan, IBattleViewEventSink viewSink)
        {
            if (viewSink == null)
            {
                throw new ArgumentNullException(nameof(viewSink));
            }

            Plan = plan;
            RuntimeGameStarted = false;
            ViewSink = viewSink;
            TickRate = plan.TickRate > 0 ? plan.TickRate : 30;

            try
            {
                SnapshotDispatcher = new FrameSnapshotDispatcher();
                CreateFrameworkWorld(in plan);

                CurrentFrame = 0;
                LogicTimeSeconds = 0;
                LastTickTime = 0;
                IsRunning = false;

                Log.Info($"[ETMobaBattleDriver] Initialized via MobaSessionCoordinatorHost: TickRate={TickRate}, WorldId={Plan.WorldId}, World={World?.Id}");
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                Log.Error($"[ETMobaBattleDriver] Initialize failed: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Log.Error($"[ETMobaBattleDriver] Initialize failed: {ex.GetType().Name} - {ex.Message}");
                throw new InvalidOperationException("Failed to initialize battle driver due to unexpected error", ex);
            }
        }

        public void Start()
        {
            if (!TryResolve(out IMobaBattleRuntimePort runtime) || runtime == null || !runtime.Status.IsReadyForBattleLoop)
            {
                var status = runtime != null ? runtime.Status.ToString() : "runtime port missing";
                Log.Error($"[ETMobaBattleDriver] IMobaBattleRuntimePort is not ready: {status}");
                throw new InvalidOperationException("IMobaBattleRuntimePort must be ready for battle loop");
            }

            _driverHost?.Start();
            IsRunning = true;
            LastTickTime = 0;
            CurrentFrame = 0;
            LogicTimeSeconds = 0;
        }

        public void Stop()
        {
            _driverHost?.Stop();
            IsRunning = false;
            RuntimeGameStarted = false;
            Log.Info("[ETMobaBattleDriver] Stopped");
        }

        public void Destroy()
        {
            _driverHost?.Stop();
            SnapshotDispatcher = null;
            Units.Clear();
            World = null;
            HostRuntime = null;
            WorldManager = null;
            ViewSink = null;
            _sessionHost = null;
            _driverHost = null;
            _runtimeSnapshots.Clear();
            _playerCommands.Clear();
            _bufferedInputCommands.Clear();

            RuntimeGameStarted = false;
            IsRunning = false;
            CurrentFrame = 0;
            LogicTimeSeconds = 0;
            LastTickTime = 0;

            Log.Info("[ETMobaBattleDriver] Destroyed");
        }

        public void Tick(float deltaTime)
        {
            if (!IsRunning || _driverHost == null)
            {
                return;
            }

            var targetFrame = new FrameIndex(CurrentFrame + 1);
            SubmitBufferedInputs(targetFrame);

            _driverHost.Step(deltaTime);

            CurrentFrame = _driverHost.CurrentFrame;
            LogicTimeSeconds = _driverHost.LogicTimeSeconds;
            LastTickTime = LogicTimeSeconds;

            CollectAndDispatchSnapshots(new FrameIndex(CurrentFrame));
        }

        public void HandleSnapshot(in MobaFrameSnapshotData snapshot)
        {
            DispatchFrameSnapshot(in snapshot);
            DispatchViewSnapshot(in snapshot);
        }

        public bool TryResolve<T>(out T service) where T : class
        {
            service = null;
            if (World?.Services != null)
            {
                return World.Services.TryResolve(out service);
            }

            return false;
        }

        public bool OnAllPlayersReady(List<ETPlayerSpawnData> players)
        {
            if (RuntimeGameStarted)
            {
                return true;
            }

            PlayerSpawnData.Clear();
            if (players != null)
            {
                PlayerSpawnData.AddRange(players);
            }
 
            var started = ETBattleEnterGameCoordinator.Trigger(this);
            return started;
        }

        private void CreateFrameworkWorld(in BattleStartPlan plan)
        {
            var context = new ETBattleWorldCreateContext(in plan, PlayerSpawnData, TextAssetLoader);
            var result = new ETBattleWorldFactory().Create(in context);

            _sessionHost = result.SessionHost;
            _driverHost = result.DriverHost;
            World = result.World;
            HostRuntime = result.HostRuntime;
            WorldManager = result.WorldManager;
        }

        private void SubmitBufferedInputs(FrameIndex targetFrame)
        {
            var inputComponent = this.Scene()?.GetComponent<ETInputComponent>();
            if (inputComponent == null)
            {
                return;
            }

            if (inputComponent.CopyInputsUpToFrame(targetFrame.Value, _bufferedInputCommands) == 0)
            {
                return;
            }

            _playerCommands.Clear();
            for (int i = 0; i < _bufferedInputCommands.Count; i++)
            {
                if (TryConvertInputCommand(_bufferedInputCommands[i], targetFrame, out var playerCommand))
                {
                    _playerCommands.Add(playerCommand);
                }
            }

            if (_playerCommands.Count == 0)
            {
                inputComponent.ClearProcessedInputs(targetFrame.Value);
                return;
            }

            var result = _driverHost.SubmitCommands(targetFrame, _playerCommands);
            if (result.Succeeded)
            {
                inputComponent.ClearProcessedInputs(targetFrame.Value);
                return;
            }

            Log.Warning($"[ETMobaBattleDriver] Submit input rejected. Frame={targetFrame.Value}, Count={_playerCommands.Count}, Result={result}");
        }

        private static bool TryConvertInputCommand(object command, FrameIndex frameIndex, out PlayerInputCommand playerCommand)
        {
            switch (command)
            {
                case MoveCommand move:
                    playerCommand = new PlayerInputCommand(
                        frameIndex,
                        new PlayerId(move.PlayerId),
                        MobaOpCodes.Input.Move,
                        MobaMoveCodec.Serialize(move.Dx, move.Dz));
                    return true;

                case SkillCommand skill:
                    var runtimeSlot = skill.SkillSlot <= 0 ? 1 : skill.SkillSlot;
                    var skillEvent = new SkillInputEvent(
                        slot: runtimeSlot,
                        phase: SkillInputPhase.Press,
                        targetActorId: skill.TargetActorId,
                        aimPos: new Vec3(skill.TargetX, 0, skill.TargetY));
                    playerCommand = new PlayerInputCommand(
                        frameIndex,
                        new PlayerId(skill.PlayerId),
                        MobaOpCodes.Input.SkillInput,
                        SkillInputCodec.Serialize(in skillEvent));
                    return true;

                case StopCommand stop:
                    playerCommand = new PlayerInputCommand(
                        frameIndex,
                        new PlayerId(stop.PlayerId),
                        MobaOpCodes.Input.Stop,
                        null);
                    return true;

                default:
                    playerCommand = default;
                    Log.Debug($"[ETMobaBattleDriver] Unsupported input command: {command?.GetType().Name ?? "null"}");
                    return false;
            }
        }

        private void CollectAndDispatchSnapshots(FrameIndex frame)
        {
            _runtimeSnapshots.Clear();
            var count = _driverHost.CollectSnapshots(frame, _runtimeSnapshots);
            if (count <= 0)
            {
                return;
            }

            for (int i = 0; i < _runtimeSnapshots.Count; i++)
            {
                var runtimeSnapshot = _runtimeSnapshots[i];
                if (ETBattleWorldSnapshotAdapter.TryConvert(
                    in runtimeSnapshot,
                    frame.Value,
                    LogicTimeSeconds,
                    out var frameSnapshot))
                {
                    HandleSnapshot(in frameSnapshot);
                }
            }
        }

        private void DispatchFrameSnapshot(in MobaFrameSnapshotData snapshot)
        {
            var dispatcher = SnapshotDispatcher;
            if (dispatcher == null)
            {
                return;
            }

            if (snapshot.EnterGame.HasValue)
            {
                dispatcher.DispatchEnterGame(snapshot.FrameIndex, snapshot.EnterGame);
            }

            if (snapshot.ActorTransforms != null && snapshot.ActorTransforms.Count > 0)
            {
                var actorTransforms = ToArray(snapshot.ActorTransforms);
                LastActorTransformSnapshot = actorTransforms;
                dispatcher.DispatchActorTransform(snapshot.FrameIndex, actorTransforms);
            }

            if (snapshot.ProjectileEvents != null && snapshot.ProjectileEvents.Count > 0)
            {
                dispatcher.DispatchProjectileEvent(snapshot.FrameIndex, ToArray(snapshot.ProjectileEvents));
            }

            if (snapshot.AreaEvents != null && snapshot.AreaEvents.Count > 0)
            {
                dispatcher.DispatchAreaEvent(snapshot.FrameIndex, ToArray(snapshot.AreaEvents));
            }

            if (snapshot.DamageEvents != null && snapshot.DamageEvents.Count > 0)
            {
                dispatcher.DispatchDamageEvent(snapshot.FrameIndex, ToArray(snapshot.DamageEvents));
            }

            if (snapshot.StateHash.HasValue)
            {
                LastStateHashSnapshot = snapshot.StateHash;
                dispatcher.DispatchStateHash(snapshot.FrameIndex, snapshot.StateHash);
            }

            if (snapshot.ActorSpawns != null && snapshot.ActorSpawns.Count > 0)
            {
                var actorSpawns = ToArray(snapshot.ActorSpawns);
                LastActorSpawnSnapshot = actorSpawns;
                dispatcher.DispatchActorSpawn(snapshot.FrameIndex, actorSpawns);
            }

            if (snapshot.ActorDespawns != null && snapshot.ActorDespawns.Count > 0)
            {
                dispatcher.DispatchActorDespawn(
                    snapshot.FrameIndex,
                    ToArray(snapshot.ActorDespawns));
            }

            if (snapshot.SkillStates != null && snapshot.SkillStates.Count > 0)
            {
                dispatcher.DispatchSkillState(
                    snapshot.FrameIndex,
                    ToArray(snapshot.SkillStates));
            }
        }

        private void DispatchViewSnapshot(in MobaFrameSnapshotData snapshot)
        {
            if (snapshot.EnterGame.HasValue || (snapshot.ActorSpawns != null && snapshot.ActorSpawns.Count > 0))
            {
                ViewSink?.OnEnterGameSnapshot(in snapshot);
            }

            if (snapshot.ActorTransforms != null && snapshot.ActorTransforms.Count > 0)
            {
                ViewSink?.OnActorTransformSnapshot(in snapshot);
            }

            if (snapshot.ActorDespawns != null && snapshot.ActorDespawns.Count > 0)
            {
                ViewSink?.OnActorDespawnSnapshot(in snapshot);
            }

            if (snapshot.SkillStates != null && snapshot.SkillStates.Count > 0)
            {
                ViewSink?.OnSkillStateSnapshot(in snapshot);
            }

            if (snapshot.DamageEvents != null && snapshot.DamageEvents.Count > 0)
            {
                ViewSink?.OnDamageEventSnapshot(in snapshot);
            }

            if (snapshot.PresentationCues != null && snapshot.PresentationCues.Count > 0)
            {
                ViewSink?.OnPresentationCueSnapshot(in snapshot);
            }

            if (snapshot.ProjectileEvents != null && snapshot.ProjectileEvents.Count > 0)
            {
                ViewSink?.OnProjectileEventSnapshot(in snapshot);
            }

            if (snapshot.AreaEvents != null && snapshot.AreaEvents.Count > 0)
            {
                ViewSink?.OnAreaEventSnapshot(in snapshot);
            }

            if (snapshot.StateHash.HasValue)
            {
                ViewSink?.OnStateHashSnapshot(in snapshot);
            }
        }

        private static T[] ToArray<T>(IReadOnlyList<T> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<T>();
            }

            if (source is T[] array)
            {
                return array;
            }

            var copy = new T[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }
    }
}
