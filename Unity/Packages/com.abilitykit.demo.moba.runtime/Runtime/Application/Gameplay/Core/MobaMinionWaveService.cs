using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.Config;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Minions;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.Map;

namespace AbilityKit.Demo.Moba.Gameplay
{
    public interface IMobaMinionWaveService : IService
    {
        int ActiveCount { get; }
        int CommittedWaveCount { get; }
        void Tick();
        void Reset();
        MobaMinionWaveRollbackState CaptureRollbackState();
        void RestoreRollbackState(in MobaMinionWaveRollbackState state);
        int DrainBaseDamageEvents(List<MobaMinionBaseDamageEvent> destination);
        bool TryGetRuntime(int actorId, out MobaMinionRuntimeView runtime);
        int CopyActiveRuntimeViews(List<MobaMinionRuntimeView> destination);
        void SetAiState(int actorId, MobaMinionAiState state);
    }

    [WorldService(typeof(IMobaMinionWaveService), WorldLifetime.Scoped)]
    [WorldService(typeof(MobaMinionWaveService), WorldLifetime.Scoped)]
    public sealed class MobaMinionWaveService : IMobaMinionWaveService
    {
        private const int DefaultBaseDamage = 1;
        private const float DefaultSafeSpacing = 1.5f;
        private const long DefaultStallTimeoutMs = 1_000L;
        private const float ProgressTieEpsilon = 0.0001f;

        private readonly MobaGameplayService _gameplay;
        private readonly IMobaActorSpawnService _spawn;
        private readonly ActorIdAllocator _actorIds;
        private readonly ActorEntityInitPipeline _initializer;
        private readonly MobaActorRegistry _actors;
        private readonly Dictionary<int, MinionState> _active = new Dictionary<int, MinionState>();
        private readonly List<PendingSpawn> _pendingSpawns = new List<PendingSpawn>(32);
        private readonly List<MobaMinionBaseDamageEvent> _baseDamageEvents = new List<MobaMinionBaseDamageEvent>(8);
        private readonly List<int> _activeIdScratch = new List<int>(64);
        private readonly List<int> _staleIdScratch = new List<int>(16);
        private MobaMinionWaveScheduler _scheduler;
        private MobaMinionRouteResource _routeResource;
        private MobaMinionRouteDefinition _route;
        [WorldInject(required: false)] private IEventBus _eventBus;
        [WorldInject(required: false)] private MobaConfigDatabase _config;
        [WorldInject(required: false)] private IMobaBattleDiagnosticEventSink _eventCollector;
        [WorldInject(required: false)] private IMobaMapRuntimeService _maps;
        [WorldInject(required: false)] private IMobaMinionRouteRuntimeConfig _routeConfig;
        [WorldInject(required: false)] private MobaActorSpawnSnapshotService _spawnSnapshots;
        private long _gameplayId;
        private int _committedWaveCount;
        private int _lastFrame;
        private long _elapsedMs;

        public MobaMinionWaveService(
            MobaGameplayService gameplay,
            IMobaActorSpawnService spawn,
            ActorIdAllocator actorIds,
            ActorEntityInitPipeline initializer,
            MobaActorRegistry actors)
        {
            _gameplay = gameplay ?? throw new ArgumentNullException(nameof(gameplay));
            _spawn = spawn ?? throw new ArgumentNullException(nameof(spawn));
            _actorIds = actorIds ?? throw new ArgumentNullException(nameof(actorIds));
            _initializer = initializer ?? throw new ArgumentNullException(nameof(initializer));
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
        }

        public int ActiveCount => _active.Count;
        public int CommittedWaveCount => _committedWaveCount;

        public bool TryGetRuntime(int actorId, out MobaMinionRuntimeView runtime)
        {
            if (actorId > 0 && _active.TryGetValue(actorId, out var state))
            {
                runtime = state.ToView();
                return true;
            }

            runtime = default;
            return false;
        }

        public int CopyActiveRuntimeViews(List<MobaMinionRuntimeView> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            _activeIdScratch.Clear();
            _activeIdScratch.AddRange(_active.Keys);
            _activeIdScratch.Sort();
            for (var i = 0; i < _activeIdScratch.Count; i++)
            {
                if (_active.TryGetValue(_activeIdScratch[i], out var state))
                    destination.Add(state.ToView());
            }

            return destination.Count;
        }

        public void SetAiState(int actorId, MobaMinionAiState state)
        {
            if (actorId <= 0 || !_active.TryGetValue(actorId, out var runtime)) return;
            runtime.AiState = state;
        }

        public void Tick()
        {
            if (!_gameplay.IsRunning)
            {
                Reset();
                return;
            }

            var clock = _gameplay.GetClockState();
            _elapsedMs = clock.ElapsedMilliseconds;
            _lastFrame = _gameplay.CurrentFrame;
            if (_gameplayId != _gameplay.CurrentGameplayId)
            {
                Reset();
                _gameplayId = _gameplay.CurrentGameplayId;
                LoadConfiguration();
                _scheduler.Reset(_gameplayId);
            }

            SweepMissingActors();
            if (_scheduler.TryPeekDueWave(_elapsedMs, out var wave) && CanCommitWave(in wave) &&
                _scheduler.CommitWave(in wave))
            {
                _committedWaveCount = checked(_committedWaveCount + 1);
                QueueWave(wave);
            }

            SpawnDue(_elapsedMs);
            AdvanceRoutes();
        }

        public int DrainBaseDamageEvents(List<MobaMinionBaseDamageEvent> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            destination.AddRange(_baseDamageEvents);
            _baseDamageEvents.Clear();
            return destination.Count;
        }

        public void Reset()
        {
            foreach (var item in _active)
            {
                if (_actors.TryGet(item.Key, out var actor))
                    ActorLifecycleRequests.RequestDespawn(actor, _lastFrame, ActorDespawnReason.SceneCleanup, item.Key);
            }
            _active.Clear();
            _pendingSpawns.Clear();
            _baseDamageEvents.Clear();
            _gameplayId = 0;
            _committedWaveCount = 0;
            _lastFrame = 0;
            _elapsedMs = 0L;
            _scheduler = null;
            _routeResource = null;
            _route = null;
        }

        public MobaMinionWaveRollbackState CaptureRollbackState()
        {
            var pending = new List<MobaMinionPendingRollbackState>(_pendingSpawns.Count);
            for (var i = 0; i < _pendingSpawns.Count; i++)
            {
                var item = _pendingSpawns[i];
                pending.Add(new MobaMinionPendingRollbackState(
                    item.WaveId, item.Team, item.Archetype, item.DueElapsedMs, item.Slot));
            }
            pending.Sort((left, right) => left.DueElapsedMs != right.DueElapsedMs
                ? left.DueElapsedMs.CompareTo(right.DueElapsedMs)
                : left.Slot != right.Slot ? left.Slot.CompareTo(right.Slot)
                : left.Team != right.Team ? ((int)left.Team).CompareTo((int)right.Team)
                : left.WaveId != right.WaveId ? left.WaveId.CompareTo(right.WaveId)
                : ((int)left.Archetype).CompareTo((int)right.Archetype));

            var active = new List<MobaMinionActiveRollbackState>(_active.Count);
            foreach (var item in _active)
            {
                var state = item.Value;
                active.Add(new MobaMinionActiveRollbackState(
                    state.ActorId, state.WaveId, state.Team, state.Archetype, state.Slot,
                    state.AiState, state.Cursor.WaypointIndex, state.Cursor.IsTerminal,
                    state.StallTracker.BlockedSinceMs, state.StallTracker.HasReported,
                    state.BlockingActorId));
            }
            active.Sort((left, right) => left.ActorId.CompareTo(right.ActorId));

            var nextWaveOrdinal = _scheduler == null ? 0 : _scheduler.NextWaveOrdinal;
            return new MobaMinionWaveRollbackState(
                _gameplayId, _committedWaveCount, nextWaveOrdinal, _lastFrame, _elapsedMs,
                pending, active);
        }

        public void RestoreRollbackState(in MobaMinionWaveRollbackState state)
        {
            if (state.GameplayId < 0 || state.CommittedWaveCount < 0 || state.NextWaveOrdinal < 0 ||
                state.LastFrame < 0 || state.ElapsedMs < 0L)
                throw new InvalidOperationException("Invalid minion wave rollback state.");

            _active.Clear();
            _pendingSpawns.Clear();
            _baseDamageEvents.Clear();
            _gameplayId = state.GameplayId;
            _committedWaveCount = state.CommittedWaveCount;
            _lastFrame = state.LastFrame;
            _elapsedMs = state.ElapsedMs;
            if (_gameplayId <= 0)
            {
                _scheduler = null;
                _routeResource = null;
                _route = null;
                return;
            }

            LoadConfiguration();
            _scheduler.Reset(_gameplayId);
            _scheduler.RestoreNextWaveOrdinal(state.NextWaveOrdinal);
            for (var i = 0; i < state.Pending.Count; i++)
            {
                var item = state.Pending[i];
                _pendingSpawns.Add(new PendingSpawn(
                    item.WaveId, item.Team, item.Archetype, item.DueElapsedMs, item.Slot));
            }

            for (var i = 0; i < state.Active.Count; i++)
            {
                var item = state.Active[i];
                if (item.ActorId <= 0 || !_actors.Contains(item.ActorId)) continue;
                var cursor = item.Team == Team.Team1
                    ? _route.CreateCursor(item.WaypointIndex)
                    : new MobaLaneRouteCursor(Reverse(_route.Waypoints), _route.AcceptanceRadius, item.WaypointIndex);
                cursor.RestoreState(item.WaypointIndex, item.IsTerminal);
                var runtime = new MinionState(item.ActorId, item.WaveId, item.Team,
                    item.Archetype, cursor, item.Slot)
                {
                    AiState = item.AiState,
                    BlockingActorId = item.BlockingActorId,
                };
                runtime.StallTracker.Restore(item.BlockedSinceMs, item.StallReported);
                _active[item.ActorId] = runtime;
            }
        }

        public void Dispose() => Reset();

        private void QueueWave(in MobaMinionWavePlan wave)
        {
            for (var teamIndex = 0; teamIndex < 2; teamIndex++)
            {
                var team = teamIndex == 0 ? Team.Team1 : Team.Team2;
                var slot = 0;
                for (var entryIndex = 0; entryIndex < wave.Composition.Count; entryIndex++)
                {
                    var entry = wave.Composition[entryIndex];
                    for (var count = 0; count < entry.Count; count++)
                    {
                        _pendingSpawns.Add(new PendingSpawn(
                            wave.WaveId,
                            team,
                            entry.Archetype,
                            wave.DueElapsedMs + (long)slot * entry.SpawnIntervalMs,
                            slot));
                        slot++;
                    }
                }
            }
        }

        private bool CanCommitWave(in MobaMinionWavePlan wave)
        {
            var capacity = _routeResource.Schedule.MaxActiveMinionsPerTeam;
            for (var teamIndex = 0; teamIndex < 2; teamIndex++)
            {
                var team = teamIndex == 0 ? Team.Team1 : Team.Team2;
                var activeCount = 0;
                foreach (var item in _active)
                    if (item.Value.Team == team) activeCount++;

                var pendingCount = 0;
                for (var i = 0; i < _pendingSpawns.Count; i++)
                    if (_pendingSpawns[i].Team == team) pendingCount++;

                if (!MobaMinionWaveCapacityPolicy.CanCommitWave(
                        capacity, activeCount, pendingCount, wave.Composition))
                    return false;
            }

            return true;
        }

        private void SpawnDue(long elapsedMs)
        {
            _pendingSpawns.Sort(PendingSpawnComparer.Instance);
            var dueCount = 0;
            while (dueCount < _pendingSpawns.Count && _pendingSpawns[dueCount].DueElapsedMs <= elapsedMs)
                dueCount++;

            for (var i = 0; i < dueCount; i++)
            {
                var pending = _pendingSpawns[i];
                SpawnMinion(in pending);
            }
            if (dueCount > 0) _pendingSpawns.RemoveRange(0, dueCount);
        }

        private void SpawnMinion(in PendingSpawn pending)
        {
            var team1 = pending.Team == Team.Team1;
            var route = _route ?? throw new InvalidOperationException("Minion route configuration was not loaded before spawning.");
            var points = route.Waypoints;
            var position = team1 ? points[0] : points[points.Count - 1];
            var rotation = team1 ? Quat.Identity : Quat.FromAxisAngle(Vec3.Up, (float)Math.PI);
            var teamId = team1 ? (int)Team.Team1 : (int)Team.Team2;
            if (_maps != null && _maps.TryGetTeamSpawnPoint(teamId, 0, out var spawnPoint) && spawnPoint != null)
            {
                position = spawnPoint.Position;
                rotation = Quat.FromAxisAngle(
                    Vec3.Up,
                    spawnPoint.YawDegrees * ((float)Math.PI / 180f));
            }
            var characterId = MobaMinionArchetypeCatalog.GetCharacterId(pending.Archetype);
            if (_config == null || characterId <= 0 ||
                !_config.TryGetCharacter(characterId, out var character) || character == null)
                throw new InvalidOperationException(
                    $"Minion character config is required. archetype={pending.Archetype}, characterId={characterId}.");
            var attributeTemplateId = character.AttributeTemplateId;
            var modelId = character.ModelId;
            if (attributeTemplateId <= 0 ||
                !_config.TryGetAttributeTemplate(attributeTemplateId, out var attributeTemplate) || attributeTemplate == null)
                throw new InvalidOperationException(
                    $"Minion attribute template config is required. archetype={pending.Archetype}, characterId={characterId}, attributeTemplateId={attributeTemplateId}.");
            if (modelId <= 0 || !_config.TryGetModel(modelId, out var model) || model == null)
                throw new InvalidOperationException(
                    $"Minion model config is required. archetype={pending.Archetype}, characterId={characterId}, modelId={modelId}.");
            var archetypeId = characterId;
            var spec = new MobaActorBuildSpec(
                new MobaEntityInfo(
                    actorId: _actorIds.Next(),
                    kind: MobaEntityKind.Minion,
                    transform: new Transform3(position, rotation, Vec3.One),
                    team: pending.Team,
                    mainType: EntityMainType.Unit,
                    unitSubType: UnitSubType.Minion,
                    ownerPlayer: default,
                    templateId: attributeTemplateId),
                MobaActorBuildSourceKind.Minion,
                archetypeId,
                0);
            var request = MobaActorSpawnRequest.FromSpec(in spec);
            request.PostSetup = new MobaActorSpawnPostSetup
            {
                SetModelId = modelId > 0,
                ModelId = modelId,
            };
            request.Initializer = (entity, _) => _initializer.InitializeFromAttributeTemplate(entity, attributeTemplateId);
            if (!_spawn.TrySpawn(in request, out var result) || !result.Success || result.Entity == null)
                return;

            var cursor = team1
                ? route.CreateCursor()
                : new MobaLaneRouteCursor(Reverse(route.Waypoints), route.AcceptanceRadius);
            _active[result.ActorId] = new MinionState(
                result.ActorId,
                pending.WaveId,
                pending.Team,
                pending.Archetype,
                cursor,
                pending.Slot);

            if (!_spawnSnapshots.TryEnqueueActorSpawn(
                    _actors,
                    result.ActorId,
                    AbilityKit.Protocol.Moba.StateSync.SpawnEntityKind.Minion,
                    modelId))
            {
                throw new InvalidOperationException(
                    $"Could not publish minion spawn snapshot. actorId={result.ActorId}, modelId={modelId}.");
            }
        }

        private void AdvanceRoutes()
        {
            _activeIdScratch.Clear();
            _activeIdScratch.AddRange(_active.Keys);
            var ids = _activeIdScratch;
            ids.Sort();
            for (var i = 0; i < ids.Count; i++)
            {
                var actorId = ids[i];
                if (!_active.TryGetValue(actorId, out var state) || !_actors.TryGet(actorId, out var actor))
                    continue;
                if (!MobaMinionAiStatePolicy.ShouldFollowRoute(state.AiState) ||
                    (actor.hasAttributeGroup && actor.GetMobaAttrs().Hp <= 0f))
                {
                    ResetBlocking(state);
                    continue;
                }
                if (!actor.hasTransform)
                {
                    ResetBlocking(state);
                    continue;
                }

                var position = actor.transform.Value.Position;
                while (state.Cursor.AdvanceIfReached(in position)) { }
                if (state.Cursor.IsTerminal)
                {
                    ResetBlocking(state);
                    ResolveTerminal(state, actorId, actor);
                    continue;
                }

                if (TryFindBlockingActor(state, in position, out var blockingActorId))
                {
                    state.BlockingActorId = blockingActorId;
                    var shouldReport = state.StallTracker.Update(
                        true, _elapsedMs, DefaultStallTimeoutMs, out var blockedForMs);
                    if (shouldReport)
                    {
                        PublishBlockingDiagnostic(state, blockingActorId, blockedForMs);
                    }

                    if (actor.hasMoveInput) actor.ReplaceMoveInput(0f, 0f);
                    else actor.AddMoveInput(0f, 0f);
                    continue;
                }

                ResetBlocking(state);

                var target = state.Cursor.CurrentWaypoint;
                var dx = target.X - position.X;
                var dz = target.Z - position.Z;
                var length = (float)Math.Sqrt(dx * dx + dz * dz);
                if (length <= 0.0001f) continue;
                var input = new Vec2(dx / length, dz / length);
                if (actor.hasMoveInput) actor.ReplaceMoveInput(input.X, input.Y);
                else actor.AddMoveInput(input.X, input.Y);
            }
        }

        private bool TryFindBlockingActor(MinionState state, in Vec3 position, out int blockingActorId)
        {
            blockingActorId = 0;
            var currentProgress = state.Cursor.GetProgress(in position);
            var closestDelta = float.MaxValue;
            foreach (var item in _active)
            {
                var candidate = item.Value;
                if (candidate.ActorId == state.ActorId || candidate.Team != state.Team ||
                    candidate.AiState == MobaMinionAiState.Dead ||
                    !_actors.TryGet(candidate.ActorId, out var candidateActor) ||
                    candidateActor == null || !candidateActor.hasTransform ||
                    (candidateActor.hasAttributeGroup && candidateActor.GetMobaAttrs().Hp <= 0f))
                    continue;

                var candidatePosition = candidateActor.transform.Value.Position;
                var candidateProgress = candidate.Cursor.GetProgress(in candidatePosition);
                var progressDelta = candidateProgress - currentProgress;
                if (!MobaMinionLaneSpacingPolicy.IsAheadWithinSafeSpacing(
                        state.ActorId,
                        currentProgress,
                        candidate.ActorId,
                        candidateProgress,
                        DefaultSafeSpacing,
                        ProgressTieEpsilon))
                    continue;
                if (Math.Abs(progressDelta) <= ProgressTieEpsilon) progressDelta = 0f;

                if (progressDelta < closestDelta ||
                    (Math.Abs(progressDelta - closestDelta) <= ProgressTieEpsilon &&
                     candidate.ActorId < blockingActorId))
                {
                    closestDelta = progressDelta;
                    blockingActorId = candidate.ActorId;
                }
            }

            return blockingActorId > 0;
        }

        private void PublishBlockingDiagnostic(MinionState state, int blockingActorId, long blockedForMs)
        {
            if (_eventCollector == null || !_eventCollector.IsEnabled(BattleDiagnosticEventChannel.WarningAndException))
                return;

            var draft = new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.Warning,
                BattleDiagnosticEventChannel.WarningAndException,
                BattleDiagnosticEventOutcome.None,
                sourceActorId: state.ActorId,
                targetActorId: blockingActorId,
                configId: _route.RouteId,
                summary: $"Minion {state.ActorId} stalled behind {blockingActorId} for {blockedForMs}ms on route {_route.RouteId}.");
            _eventCollector.TryCollect(in draft);
        }

        private static void ResetBlocking(MinionState state)
        {
            state.StallTracker.Reset();
            state.BlockingActorId = 0;
        }

        private void ResolveTerminal(MinionState state, int actorId, global::ActorEntity actor)
        {
            if (state.AiState == MobaMinionAiState.Terminal)
                return;

            switch (_route.TerminalPolicy)
            {
                case MobaMinionTerminalPolicy.BaseDamageEvent:
                    var baseDamageEvent = new MobaMinionBaseDamageEvent(
                        state.WaveId,
                        actorId,
                        state.Team == Team.Team1 ? (int)Team.Team1 : (int)Team.Team2,
                        state.Team == Team.Team1 ? (int)Team.Team2 : (int)Team.Team1,
                        DefaultBaseDamage,
                        _lastFrame);
                    _baseDamageEvents.Add(baseDamageEvent);
                    PublishBaseDamage(in baseDamageEvent);
                    ActorLifecycleRequests.RequestDespawn(actor, _lastFrame,
                        ActorDespawnReason.MinionTerminal, actorId);
                    _active.Remove(actorId);
                    break;

                case MobaMinionTerminalPolicy.Despawn:
                    ActorLifecycleRequests.RequestDespawn(actor, _lastFrame,
                        ActorDespawnReason.MinionTerminal, actorId);
                    _active.Remove(actorId);
                    break;

                case MobaMinionTerminalPolicy.Hold:
                    state.AiState = MobaMinionAiState.Terminal;
                    if (actor.hasMoveInput) actor.ReplaceMoveInput(0f, 0f);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported minion terminal policy: {_route.TerminalPolicy}");
            }
        }

        private void PublishBaseDamage(in MobaMinionBaseDamageEvent payload)
        {
            if (_eventBus == null) return;
            var eventId = TriggeringIdUtil.GetEventEid(MobaMinionEvents.BaseDamage);
            _eventBus.Publish(new EventKey<MobaMinionBaseDamageEvent>(eventId), in payload);
            var objectKey = new EventKey<object>(eventId);
            if (_eventBus.HasSubscribers(objectKey))
            {
                object boxed = payload;
                _eventBus.Publish(objectKey, in boxed);
            }
        }

        private void SweepMissingActors()
        {
            _staleIdScratch.Clear();
            foreach (var item in _active)
                if (!_actors.Contains(item.Key)) _staleIdScratch.Add(item.Key);
            for (var i = 0; i < _staleIdScratch.Count; i++) _active.Remove(_staleIdScratch[i]);
        }

        private void LoadConfiguration()
        {
            if (_maps == null || !_maps.IsLoaded || _maps.CurrentMap == null)
                throw new InvalidOperationException("Minion wave runtime requires a loaded battle map.");

            if (_routeConfig == null || !_routeConfig.IsPrepared || _routeConfig.MapId != _maps.CurrentMap.Id)
            {
                throw new InvalidOperationException(
                    $"Minion wave runtime requires a prepared route configuration. mapId={_maps.CurrentMap.Id}, path={MobaMinionRouteRuntimeConfig.ResourcePath}");
            }

            if (_spawnSnapshots == null)
            {
                throw new InvalidOperationException(
                    "Minion wave runtime requires MobaActorSpawnSnapshotService for dynamic actor synchronization.");
            }

            _routeResource = _routeConfig.Resource;
            _route = _routeConfig.DefaultRoute;

            var schedule = _routeResource.Schedule;
            _scheduler = new MobaMinionWaveScheduler(in schedule);
        }

        private static IReadOnlyList<Vec3> Reverse(IReadOnlyList<Vec3> points)
        {
            var reversed = new Vec3[points.Count];
            for (var i = 0; i < points.Count; i++) reversed[i] = points[points.Count - 1 - i];
            return reversed;
        }

        private readonly struct PendingSpawn
        {
            public PendingSpawn(long waveId, Team team, MobaMinionArchetype archetype, long dueElapsedMs, int slot)
            {
                WaveId = waveId;
                Team = team;
                Archetype = archetype;
                DueElapsedMs = dueElapsedMs;
                Slot = slot;
            }

            public long WaveId { get; }
            public Team Team { get; }
            public MobaMinionArchetype Archetype { get; }
            public long DueElapsedMs { get; }
            public int Slot { get; }
        }

        private sealed class PendingSpawnComparer : IComparer<PendingSpawn>
        {
            public static readonly PendingSpawnComparer Instance = new PendingSpawnComparer();
            public int Compare(PendingSpawn left, PendingSpawn right)
            {
                var result = left.DueElapsedMs.CompareTo(right.DueElapsedMs);
                if (result != 0) return result;
                result = left.Slot.CompareTo(right.Slot);
                return result != 0 ? result : ((int)left.Team).CompareTo((int)right.Team);
            }
        }

        private sealed class MinionState
        {
            public MinionState(int actorId, long waveId, Team team, MobaMinionArchetype archetype,
                MobaLaneRouteCursor cursor, int slot)
            {
                ActorId = actorId;
                WaveId = waveId;
                Team = team;
                Archetype = archetype;
                Cursor = cursor;
                Slot = slot;
            }

            public int ActorId { get; }
            public long WaveId { get; }
            public Team Team { get; }
            public MobaMinionArchetype Archetype { get; }
            public MobaLaneRouteCursor Cursor { get; }
            public int Slot { get; }
            public MobaMinionAiState AiState { get; set; } = MobaMinionAiState.FollowingRoute;
            public MobaMinionStallTracker StallTracker { get; } = new MobaMinionStallTracker();
            public int BlockingActorId { get; set; }

            public MobaMinionRuntimeView ToView()
            {
                return new MobaMinionRuntimeView(ActorId, WaveId, Team, Archetype, Slot, AiState);
            }
        }
    }
}
