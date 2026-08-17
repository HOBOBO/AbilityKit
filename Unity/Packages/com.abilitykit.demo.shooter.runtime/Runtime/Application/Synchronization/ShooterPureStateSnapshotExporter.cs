using System;
using System.Collections.Generic;
using AbilityKit.Ability.StateSync.Aoi;
using AbilityKit.Protocol.Shooter;
using AbilityKit.World.Svelto;
using Svelto.DataStructures;
using Svelto.ECS;

namespace AbilityKit.Demo.Shooter.Runtime
{
    public readonly struct ShooterPureStateWorldCacheDiagnostics
    {
        public ShooterPureStateWorldCacheDiagnostics(long rebuildCount, long hitCount, int cachedFrame, int cachedEntityCount)
        {
            RebuildCount = rebuildCount;
            HitCount = hitCount;
            CachedFrame = cachedFrame;
            CachedEntityCount = cachedEntityCount;
        }

        public long RebuildCount { get; }

        public long HitCount { get; }

        public int CachedFrame { get; }

        public int CachedEntityCount { get; }
    }

    public sealed class ShooterPureStateSnapshotExporter
    {
        private const int PositionScale = 1000;
        private const int VelocityScale = 1000;
        private const long SnapshotWorldRevision = long.MinValue;

        private readonly ShooterBattleState _state;
        private readonly IShooterSnapshotReadPort _snapshotReadPort;
        private readonly IShooterStateHashProvider _stateHashProvider;
        private readonly IShooterEntityManager? _entities;
        private readonly ISveltoWorldContext? _context;
        private readonly ShooterSnapshotOrderBuffer _orderBuffer = new();
        private readonly ShooterPureStateInterestPolicy _interestPolicy = new();
        private readonly Dictionary<AoiEntityKey, int> _candidateIndexByKey = new Dictionary<AoiEntityKey, int>();
        private readonly Dictionary<AoiInterestSet, ShooterObserverReplicationState> _observerReplicationStates = new Dictionary<AoiInterestSet, ShooterObserverReplicationState>();
        private readonly ShooterObserverReplicationState _unscopedReplicationState = new ShooterObserverReplicationState();
        private readonly List<AoiEntityKey> _unscopedDespawnKeys = new List<AoiEntityKey>();
        private readonly List<ShooterPureStateEntityDelta> _aoiSelectedEntities = new List<ShooterPureStateEntityDelta>();
        private readonly List<ShooterPureStateVisibilityHint> _aoiSelectedHints = new List<ShooterPureStateVisibilityHint>();
        private readonly AoiSampleBufferView _aoiSampleView = new AoiSampleBufferView();
        private ShooterPureStateCandidate[] _candidateBuffer = Array.Empty<ShooterPureStateCandidate>();
        private ShooterPureStateWorldSample[] _worldSampleBuffer = Array.Empty<ShooterPureStateWorldSample>();
        private AoiEntitySample[] _aoiSampleBuffer = Array.Empty<AoiEntitySample>();
        private ShooterPureStateEntityDelta[] _transientEntities = Array.Empty<ShooterPureStateEntityDelta>();
        private ShooterPureStateVisibilityHint[] _transientVisibilityHints = Array.Empty<ShooterPureStateVisibilityHint>();
        private ulong _unscopedReplicationWorldId;
        private bool _hasUnscopedReplicationWorld;
        private int _cachedWorldFrame = -1;
        private long _cachedWorldMutationRevision = -1;
        private int _cachedWorldSampleCount;
        private long _worldCacheRebuildCount;
        private long _worldCacheHitCount;

        public ShooterPureStateSnapshotExporter(
            ShooterBattleState state,
            IShooterSnapshotReadPort snapshotReadPort,
            IShooterStateHashProvider stateHashProvider)
            : this(state, snapshotReadPort, stateHashProvider, entities: null)
        {
        }

        public ShooterPureStateSnapshotExporter(
            ShooterBattleState state,
            IShooterSnapshotReadPort snapshotReadPort,
            IShooterStateHashProvider stateHashProvider,
            IShooterEntityManager? entities)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _snapshotReadPort = snapshotReadPort ?? throw new ArgumentNullException(nameof(snapshotReadPort));
            _stateHashProvider = stateHashProvider ?? throw new ArgumentNullException(nameof(stateHashProvider));
            _entities = entities;
            _context = entities?.SveltoContext;
        }

        public ShooterPureStateWorldCacheDiagnostics WorldCacheDiagnostics => new ShooterPureStateWorldCacheDiagnostics(
            _worldCacheRebuildCount,
            _worldCacheHitCount,
            _cachedWorldFrame,
            _cachedWorldSampleCount);

        public ShooterPureStateSnapshotPayload Export(
            ulong worldId,
            bool isFullBaseline = true,
            ShooterPureStateSyncSettings? settings = null,
            int baselineFrame = 0,
            uint baselineHash = 0,
            ShooterPureStateInterestScope? interestScope = null,
            AoiInterestSet? aoiInterestSet = null,
            bool computeStateHash = true)
        {
            return ExportCore(
                worldId,
                isFullBaseline,
                settings,
                baselineFrame,
                baselineHash,
                interestScope,
                aoiInterestSet,
                computeStateHash,
                useTransientBuffers: false);
        }

        /// <summary>
        /// Exports a payload backed by reusable capacity arrays. Only the effective counts are
        /// valid, and the payload must be consumed before the next transient export.
        /// </summary>
        public ShooterPureStateSnapshotPayload ExportTransient(
            ulong worldId,
            bool isFullBaseline = true,
            ShooterPureStateSyncSettings? settings = null,
            int baselineFrame = 0,
            uint baselineHash = 0,
            ShooterPureStateInterestScope? interestScope = null,
            AoiInterestSet? aoiInterestSet = null,
            bool computeStateHash = true)
        {
            return ExportCore(
                worldId,
                isFullBaseline,
                settings,
                baselineFrame,
                baselineHash,
                interestScope,
                aoiInterestSet,
                computeStateHash,
                useTransientBuffers: true);
        }

        private ShooterPureStateSnapshotPayload ExportCore(
            ulong worldId,
            bool isFullBaseline,
            ShooterPureStateSyncSettings? settings,
            int baselineFrame,
            uint baselineHash,
            ShooterPureStateInterestScope? interestScope,
            AoiInterestSet? aoiInterestSet,
            bool computeStateHash,
            bool useTransientBuffers)
        {
            var activeSettings = NormalizeSettings(settings ?? ShooterPureStateSyncSettings.Default);
            var frame = _state.CurrentFrame;
            var isLowFrequencyFrame = !isFullBaseline && activeSettings.LowFrequencyIntervalFrames > 0 && frame % activeSettings.LowFrequencyIntervalFrames == 0;
            var entityBudget = isFullBaseline ? activeSettings.MaxEntityCount : activeSettings.ActiveSyncBudget;
            var maxEntities = ResolveMaxEntities(activeSettings, entityBudget, interestScope);
            var cullToAoiBoundary = aoiInterestSet != null && interestScope.HasValue && interestScope.Value.HasRadius;
            if ((aoiInterestSet == null || !interestScope.HasValue) &&
                (!_hasUnscopedReplicationWorld || _unscopedReplicationWorldId != worldId))
            {
                _unscopedReplicationState.Clear();
                _unscopedReplicationWorldId = worldId;
                _hasUnscopedReplicationWorld = true;
            }

            var candidateCount = _context != null
                ? BuildCandidates(_context, isFullBaseline, isLowFrequencyFrame, interestScope, cullToAoiBoundary)
                : BuildCandidatesFromSnapshot(isFullBaseline, isLowFrequencyFrame, interestScope, cullToAoiBoundary, out frame);
            var requiresPriorityOrder = !isFullBaseline || maxEntities < candidateCount || interestScope.HasValue || aoiInterestSet != null;
            if (requiresPriorityOrder && candidateCount > 1)
            {
                Array.Sort(_candidateBuffer, 0, candidateCount);
            }

            var selection = SelectCandidates(
                candidateCount,
                maxEntities,
                isFullBaseline,
                interestScope,
                aoiInterestSet,
                useTransientBuffers);
            var entities = selection.Entities;
            var visibilityHints = selection.VisibilityHints;

            var stateHash = computeStateHash ? _stateHashProvider.ComputeStateHash() : 0u;
            var payload = new ShooterPureStateSnapshotPayload(
                ShooterPureStateSyncCodec.CurrentVersion,
                worldId,
                frame,
                frame,
                CreateSnapshotKind(isFullBaseline, isLowFrequencyFrame),
                isFullBaseline ? frame : baselineFrame,
                isFullBaseline ? stateHash : baselineHash,
                stateHash,
                activeSettings,
                entities,
                visibilityHints);
            if (useTransientBuffers)
            {
                payload.SetTransientCounts(selection.EntityCount, selection.VisibilityHintCount);
            }

            return payload;
        }

        private int BuildCandidatesFromSnapshot(
            bool isFullBaseline,
            bool isLowFrequencyFrame,
            ShooterPureStateInterestScope? interestScope,
            bool cullToAoiBoundary,
            out int frame)
        {
            var snapshot = _snapshotReadPort.GetSnapshot();
            var players = snapshot.Players ?? Array.Empty<ShooterPlayerSnapshot>();
            var bullets = snapshot.Bullets ?? Array.Empty<ShooterBulletSnapshot>();
            frame = snapshot.Frame <= 0 ? _state.CurrentFrame : snapshot.Frame;
            var sampleCount = GetWorldSamples(players, bullets, frame);
            return BuildCandidatesFromWorldSamples(
                sampleCount,
                isFullBaseline,
                isLowFrequencyFrame,
                interestScope,
                cullToAoiBoundary);
        }

        private int GetWorldSamples(
            ShooterPlayerSnapshot[] players,
            ShooterBulletSnapshot[] bullets,
            int frame)
        {
            if (_cachedWorldFrame == frame && _cachedWorldMutationRevision == SnapshotWorldRevision)
            {
                _worldCacheHitCount++;
                return _cachedWorldSampleCount;
            }

            _worldSampleBuffer = EnsureCapacity(_worldSampleBuffer, players.Length + bullets.Length);
            var index = 0;
            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                var flags = CreatePlayerFlags(in player);
                var entity = new ShooterPureStateEntityDelta(
                    player.PlayerId,
                    ShooterPackedEntityKinds.Player,
                    ShooterPureStateEntityLayers.KeyInteraction,
                    ShooterPureStateDeltaKinds.None,
                    player.PlayerId,
                    QuantizePosition(player.X),
                    QuantizePosition(player.Y),
                    QuantizeVelocity(player.AimX),
                    QuantizeVelocity(player.AimY),
                    player.Hp,
                    player.Score,
                    0,
                    flags);
                _worldSampleBuffer[index++] = new ShooterPureStateWorldSample(entity, player.X, player.Y, player.Alive);
            }

            for (var i = 0; i < bullets.Length; i++)
            {
                var bullet = bullets[i];
                var flags = (byte)(ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible);
                var entity = new ShooterPureStateEntityDelta(
                    bullet.BulletId,
                    ShooterPackedEntityKinds.Projectile,
                    ShooterPureStateEntityLayers.Combat,
                    ShooterPureStateDeltaKinds.None,
                    bullet.OwnerPlayerId,
                    QuantizePosition(bullet.X),
                    QuantizePosition(bullet.Y),
                    QuantizeVelocity(bullet.VelocityX),
                    QuantizeVelocity(bullet.VelocityY),
                    0,
                    0,
                    bullet.RemainingFrames,
                    flags);
                _worldSampleBuffer[index++] = new ShooterPureStateWorldSample(entity, bullet.X, bullet.Y, alive: true);
            }

            _cachedWorldFrame = frame;
            _cachedWorldMutationRevision = SnapshotWorldRevision;
            _cachedWorldSampleCount = index;
            _worldCacheRebuildCount++;
            return _cachedWorldSampleCount;
        }
 
        private int BuildCandidates(
            ISveltoWorldContext context,
            bool isFullBaseline,
            bool isLowFrequencyFrame,
            ShooterPureStateInterestScope? interestScope,
            bool cullToAoiBoundary)
        {
            var sampleCount = GetWorldSamples(context);
            return BuildCandidatesFromWorldSamples(
                sampleCount,
                isFullBaseline,
                isLowFrequencyFrame,
                interestScope,
                cullToAoiBoundary);
        }

        private int BuildCandidatesFromWorldSamples(
            int sampleCount,
            bool isFullBaseline,
            bool isLowFrequencyFrame,
            ShooterPureStateInterestScope? interestScope,
            bool cullToAoiBoundary)
        {
            var candidates = EnsureCandidateCapacity(sampleCount);
            var index = 0;
            for (var i = 0; i < sampleCount; i++)
            {
                var sample = _worldSampleBuffer[i];
                if (cullToAoiBoundary && !IsInsideAoiBoundary(sample.X, sample.Y, interestScope!.Value))
                {
                    continue;
                }

                var entity = sample.Entity;
                entity.DeltaKind = CreateDeltaKind(isFullBaseline);
                if (isLowFrequencyFrame &&
                    (entity.EntityKind == ShooterPackedEntityKinds.Projectile || !sample.Alive))
                {
                    entity.Flags |= ShooterPureStateEntityFlags.LowFrequency;
                }

                var priority = CreateWorldSamplePriority(in sample, interestScope);
                if (priority <= 0 && entity.EntityKind != ShooterPackedEntityKinds.Player)
                {
                    entity.Flags = (byte)(entity.Flags & ~ShooterPureStateEntityFlags.Visible);
                }

                var hint = new ShooterPureStateVisibilityHint(
                    entity.EntityId,
                    entity.EntityKind,
                    entity.EntityLayer,
                    entity.Flags,
                    priority);
                candidates[index++] = new ShooterPureStateCandidate(
                    entity,
                    hint,
                    priority,
                    _interestPolicy.ComputeDistanceSquared(sample.X, sample.Y, interestScope),
                    entity.EntityId,
                    sample.X,
                    sample.Y);
            }

            return index;
        }

        private int GetWorldSamples(ISveltoWorldContext context)
        {
            var frame = _state.CurrentFrame;
            var mutationRevision = _entities?.MutationRevision ?? -1;
            if (_cachedWorldFrame == frame && _cachedWorldMutationRevision == mutationRevision)
            {
                _worldCacheHitCount++;
                return _cachedWorldSampleCount;
            }

            var playerCollection = context.EntitiesDB.QueryEntities<ShooterSveltoPlayerComponent>((ExclusiveGroupStruct)ShooterSveltoGroups.Players);
            playerCollection.Deconstruct(out NB<ShooterSveltoPlayerComponent> players, out _, out var playerCount);
            var projectileCollection = context.EntitiesDB.QueryEntities<ShooterSveltoProjectileComponent>((ExclusiveGroupStruct)ShooterSveltoGroups.Projectiles);
            projectileCollection.Deconstruct(out NB<ShooterSveltoProjectileComponent> bullets, out _, out var bulletCount);
            var enemyCollection = context.EntitiesDB.QueryEntities<ShooterSveltoTransformComponent, ShooterSveltoHealthComponent>((ExclusiveGroupStruct)ShooterSveltoGroups.GameplayTargets);
            enemyCollection.Deconstruct(out NB<ShooterSveltoTransformComponent> enemyTransforms, out NB<ShooterSveltoHealthComponent> enemyHealths, out var enemyIds, out var enemyCount);
            _worldSampleBuffer = EnsureCapacity(_worldSampleBuffer, playerCount + bulletCount + enemyCount);
            var index = 0;

            var playerOrder = _orderBuffer.CreateSortedPlayerOrder(players, playerCount);
            for (var i = 0; i < playerCount; i++)
            {
                var player = players[playerOrder[i]];
                var flags = CreatePlayerFlags(in player);
                var entity = new ShooterPureStateEntityDelta(
                    player.PlayerId,
                    ShooterPackedEntityKinds.Player,
                    ShooterPureStateEntityLayers.KeyInteraction,
                    ShooterPureStateDeltaKinds.None,
                    player.PlayerId,
                    QuantizePosition(player.X),
                    QuantizePosition(player.Y),
                    QuantizeVelocity(player.AimX),
                    QuantizeVelocity(player.AimY),
                    player.Hp,
                    player.Score,
                    0,
                    flags);
                _worldSampleBuffer[index++] = new ShooterPureStateWorldSample(entity, player.X, player.Y, player.Alive);
            }

            var projectileOrder = _orderBuffer.CreateSortedProjectileOrder(bullets, bulletCount);
            for (var i = 0; i < bulletCount; i++)
            {
                var bullet = bullets[projectileOrder[i]];
                var flags = (byte)(ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible);
                var entity = new ShooterPureStateEntityDelta(
                    bullet.BulletId,
                    ShooterPackedEntityKinds.Projectile,
                    ShooterPureStateEntityLayers.Combat,
                    ShooterPureStateDeltaKinds.None,
                    bullet.OwnerPlayerId,
                    QuantizePosition(bullet.X),
                    QuantizePosition(bullet.Y),
                    QuantizeVelocity(bullet.VelocityX),
                    QuantizeVelocity(bullet.VelocityY),
                    0,
                    0,
                    bullet.RemainingFrames,
                    flags);
                _worldSampleBuffer[index++] = new ShooterPureStateWorldSample(entity, bullet.X, bullet.Y, alive: true);
            }

            var enemyOrder = _orderBuffer.CreateSortedEnemyOrder(enemyIds, enemyCount);
            for (var i = 0; i < enemyCount; i++)
            {
                var enemyIndex = enemyOrder[i];
                var entityId = checked((int)enemyIds[enemyIndex]);
                var transform = enemyTransforms[enemyIndex];
                var health = enemyHealths[enemyIndex];
                var flags = (byte)ShooterPureStateEntityFlags.Visible;
                if (health.Alive != 0)
                {
                    flags |= ShooterPureStateEntityFlags.Alive;
                }

                var entity = new ShooterPureStateEntityDelta(
                    entityId,
                    ShooterPackedEntityKinds.Enemy,
                    ShooterPureStateEntityLayers.Combat,
                    ShooterPureStateDeltaKinds.None,
                    0,
                    QuantizePosition(transform.X),
                    QuantizePosition(transform.Y),
                    QuantizeVelocity(transform.DirectionX),
                    QuantizeVelocity(transform.DirectionY),
                    health.Current,
                    0,
                    0,
                    flags);
                _worldSampleBuffer[index++] = new ShooterPureStateWorldSample(entity, transform.X, transform.Y, health.Alive != 0);
            }

            _cachedWorldFrame = frame;
            _cachedWorldMutationRevision = mutationRevision;
            _cachedWorldSampleCount = index;
            _worldCacheRebuildCount++;
            return _cachedWorldSampleCount;
        }

        private int CreateWorldSamplePriority(
            in ShooterPureStateWorldSample sample,
            ShooterPureStateInterestScope? interestScope)
        {
            var entity = sample.Entity;
            switch (entity.EntityKind)
            {
                case ShooterPackedEntityKinds.Player:
                {
                    var priority = sample.Alive ? 100 : 10;
                    if (!interestScope.HasValue) return priority;
                    var scope = interestScope.Value;
                    if (scope.ObserverPlayerId > 0 && entity.EntityId == scope.ObserverPlayerId) return 1000;
                    return _interestPolicy.IsInsideScope(sample.X, sample.Y, scope) ? priority + 200 : priority;
                }
                case ShooterPackedEntityKinds.Projectile:
                {
                    if (!interestScope.HasValue) return 80;
                    var scope = interestScope.Value;
                    if (scope.ObserverPlayerId > 0 && entity.OwnerId == scope.ObserverPlayerId) return 250;
                    return _interestPolicy.IsInsideScope(sample.X, sample.Y, scope) ? 180 : 1;
                }
                case ShooterPackedEntityKinds.Enemy:
                {
                    var priority = sample.Alive ? 70 : 5;
                    if (!interestScope.HasValue) return priority;
                    return _interestPolicy.IsInsideScope(sample.X, sample.Y, interestScope.Value) ? priority + 160 : 1;
                }
                default:
                    return 0;
            }
        }

        private ShooterPureStateSelection SelectCandidates(
            int candidateCount,
            int maxEntities,
            bool isFullBaseline,
            ShooterPureStateInterestScope? interestScope,
            AoiInterestSet? aoiInterestSet,
            bool useTransientBuffers)
        {
            if (aoiInterestSet == null || !interestScope.HasValue)
            {
                var selectedCount = Math.Min(maxEntities, candidateCount);
                _unscopedDespawnKeys.Clear();
                if (isFullBaseline)
                {
                    _unscopedReplicationState.Clear();
                }
                else if (_unscopedReplicationState.Replicated.Count > 0)
                {
                    BuildCandidateIndex(candidateCount);
                    foreach (var key in _unscopedReplicationState.Replicated)
                    {
                        if (!TryFindCandidate(key, candidateCount, out var candidate) ||
                            !IsAliveAndVisible(candidate.Entity))
                        {
                            _unscopedDespawnKeys.Add(key);
                        }
                    }
                }

                var selection = CreateSelection(
                    selectedCount + _unscopedDespawnKeys.Count,
                    selectedCount,
                    useTransientBuffers);
                for (var i = 0; i < selectedCount; i++)
                {
                    var candidate = _candidateBuffer[i];
                    selection.Entities[i] = candidate.Entity;
                    selection.VisibilityHints[i] = candidate.VisibilityHint;
                    if (IsAliveAndVisible(candidate.Entity))
                    {
                        _unscopedReplicationState.Replicated.Add(candidate.AoiKey);
                    }
                    else
                    {
                        _unscopedReplicationState.Replicated.Remove(candidate.AoiKey);
                    }
                }

                for (var i = 0; i < _unscopedDespawnKeys.Count; i++)
                {
                    var key = _unscopedDespawnKeys[i];
                    _unscopedReplicationState.Replicated.Remove(key);
                    selection.Entities[selectedCount + i] = TryFindCandidate(key, candidateCount, out var candidate)
                        ? CreateDespawnDelta(candidate.Entity)
                        : CreateDespawnDelta(key);
                }

                return selection;
            }

            var samples = EnsureAoiSampleCapacity(candidateCount);
            for (var i = 0; i < candidateCount; i++)
            {
                var candidate = _candidateBuffer[i];
                samples[i] = new AoiEntitySample(
                    candidate.AoiKey,
                    candidate.X,
                    candidate.Y,
                    candidate.Priority,
                    candidate.Entity.EntityLayer,
                    candidate.Entity.OwnerId,
                    candidate.Entity.Flags);
            }

            _aoiSampleView.Reset(samples, candidateCount);
            BuildCandidateIndex(candidateCount);
            var evaluation = aoiInterestSet.EvaluateTransient(_aoiSampleView, interestScope.Value.ToAoiScope(), isFullBaseline);
            var replicationState = GetObserverReplicationState(aoiInterestSet);
            if (isFullBaseline)
            {
                replicationState.Clear();
            }

            _aoiSelectedEntities.Clear();
            _aoiSelectedHints.Clear();
            var visibleChangeCount = Math.Min(evaluation.VisibleCount, evaluation.Changes.Count);

            // Leaves are lifecycle messages, not state-update budget consumers. Only emit a
            // despawn when this observer actually received the corresponding spawn.
            for (var i = visibleChangeCount; i < evaluation.Changes.Count; i++)
            {
                var change = evaluation.Changes[i];
                if (change.Transition == AoiInterestTransition.Leave && replicationState.Replicated.Remove(change.Key))
                {
                    _aoiSelectedEntities.Add(CreateDespawnDelta(change));
                }
            }

            if (visibleChangeCount == 0 || maxEntities <= 0)
            {
                replicationState.RotationCursor = 0;
                return CreateAoiSelection(useTransientBuffers);
            }

            // Keep at most half of the budget for the stable high-priority prefix and rotate
            // the rest. This keeps local/key interactions responsive while guaranteeing that
            // ordinary visible entities eventually receive a first Spawn and later updates.
            var reservedLimit = maxEntities / 2;
            var reservedCount = 0;
            while (reservedCount < visibleChangeCount && reservedCount < reservedLimit)
            {
                if (!TryFindCandidate(evaluation.Changes[reservedCount].Key, candidateCount, out var candidate) || candidate.Priority < 250)
                {
                    break;
                }

                AddVisibleCandidate(evaluation.Changes[reservedCount], in candidate, replicationState, isFullBaseline);
                reservedCount++;
            }

            var rotatingCount = visibleChangeCount - reservedCount;
            var rotatingBudget = maxEntities - reservedCount;
            if (rotatingCount > 0 && rotatingBudget > 0)
            {
                var start = replicationState.RotationCursor % rotatingCount;
                var selected = 0;
                for (var offset = 0; offset < rotatingCount && selected < rotatingBudget; offset++)
                {
                    var changeIndex = reservedCount + ((start + offset) % rotatingCount);
                    var change = evaluation.Changes[changeIndex];
                    if (!TryFindCandidate(change.Key, candidateCount, out var candidate))
                    {
                        continue;
                    }

                    AddVisibleCandidate(change, in candidate, replicationState, isFullBaseline);
                    selected++;
                }

                replicationState.RotationCursor = (start + selected) % rotatingCount;
            }

            return CreateAoiSelection(useTransientBuffers);
        }

        private ShooterObserverReplicationState GetObserverReplicationState(AoiInterestSet interestSet)
        {
            if (!_observerReplicationStates.TryGetValue(interestSet, out var state))
            {
                state = new ShooterObserverReplicationState();
                _observerReplicationStates.Add(interestSet, state);
            }

            return state;
        }

        private void AddVisibleCandidate(
            AoiInterestChange change,
            in ShooterPureStateCandidate candidate,
            ShooterObserverReplicationState replicationState,
            bool isFullBaseline)
        {
            var entity = candidate.Entity;
            var firstReplication = replicationState.Replicated.Add(change.Key);
            entity.DeltaKind = firstReplication || change.Transition == AoiInterestTransition.Enter || isFullBaseline
                ? ShooterPureStateDeltaKinds.Spawn
                : ShooterPureStateDeltaKinds.Update;
            entity.Flags = (byte)(entity.Flags | ShooterPureStateEntityFlags.Visible);
            _aoiSelectedEntities.Add(entity);

            var hint = candidate.VisibilityHint;
            hint.Flags = (byte)(hint.Flags | ShooterPureStateEntityFlags.Visible);
            _aoiSelectedHints.Add(hint);
        }

        private ShooterPureStateSelection CreateAoiSelection(bool useTransientBuffers)
        {
            var selection = CreateSelection(
                _aoiSelectedEntities.Count,
                _aoiSelectedHints.Count,
                useTransientBuffers);
            _aoiSelectedEntities.CopyTo(selection.Entities);
            _aoiSelectedHints.CopyTo(selection.VisibilityHints);
            return selection;
        }

        private ShooterPureStateSelection CreateSelection(
            int entityCount,
            int visibilityHintCount,
            bool useTransientBuffers)
        {
            if (!useTransientBuffers)
            {
                return new ShooterPureStateSelection(
                    new ShooterPureStateEntityDelta[entityCount],
                    entityCount,
                    new ShooterPureStateVisibilityHint[visibilityHintCount],
                    visibilityHintCount);
            }

            _transientEntities = EnsureCapacity(_transientEntities, entityCount);
            _transientVisibilityHints = EnsureCapacity(_transientVisibilityHints, visibilityHintCount);
            return new ShooterPureStateSelection(
                _transientEntities,
                entityCount,
                _transientVisibilityHints,
                visibilityHintCount);
        }

        private static T[] EnsureCapacity<T>(T[] buffer, int count)
        {
            if (count <= 0)
            {
                return buffer;
            }

            if (buffer.Length >= count)
            {
                return buffer;
            }

            var capacity = buffer.Length == 0 ? 16 : buffer.Length;
            while (capacity < count)
            {
                capacity = checked(capacity * 2);
            }

            return new T[capacity];
        }

        private bool TryFindCandidate(AoiEntityKey key, int candidateCount, out ShooterPureStateCandidate candidate)
        {
            if (_candidateIndexByKey.TryGetValue(key, out var index) && index < candidateCount)
            {
                candidate = _candidateBuffer[index];
                return true;
            }

            candidate = default;
            return false;
        }

        private static ShooterPureStateEntityDelta CreateDespawnDelta(AoiInterestChange change)
        {
            return new ShooterPureStateEntityDelta(
                change.Key.Id,
                change.Key.Kind,
                change.Layer,
                ShooterPureStateDeltaKinds.Despawn,
                change.OwnerId,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                (byte)(change.Flags & ~ShooterPureStateEntityFlags.Alive));
        }

        private static ShooterPureStateEntityDelta CreateDespawnDelta(ShooterPureStateEntityDelta entity)
        {
            var despawn = entity;
            despawn.DeltaKind = ShooterPureStateDeltaKinds.Despawn;
            despawn.Flags = (byte)(despawn.Flags &
                ~(ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible));
            return despawn;
        }

        private static ShooterPureStateEntityDelta CreateDespawnDelta(AoiEntityKey key)
        {
            return new ShooterPureStateEntityDelta(
                key.Id,
                key.Kind,
                key.Kind == ShooterPackedEntityKinds.Player
                    ? ShooterPureStateEntityLayers.KeyInteraction
                    : ShooterPureStateEntityLayers.Combat,
                ShooterPureStateDeltaKinds.Despawn,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0);
        }

        private static bool IsAliveAndVisible(ShooterPureStateEntityDelta entity)
        {
            return (entity.Flags & ShooterPureStateEntityFlags.Alive) != 0 &&
                (entity.Flags & ShooterPureStateEntityFlags.Visible) != 0;
        }

        private void BuildCandidateIndex(int candidateCount)
        {
            _candidateIndexByKey.Clear();
            for (var i = 0; i < candidateCount; i++)
            {
                _candidateIndexByKey[_candidateBuffer[i].AoiKey] = i;
            }
        }

        private ShooterPureStateCandidate[] EnsureCandidateCapacity(int count)
        {
            if (_candidateBuffer.Length >= count)
            {
                return _candidateBuffer;
            }

            var newCapacity = _candidateBuffer.Length == 0 ? 16 : _candidateBuffer.Length;
            while (newCapacity < count)
            {
                newCapacity = checked(newCapacity * 2);
            }

            _candidateBuffer = new ShooterPureStateCandidate[newCapacity];
            return _candidateBuffer;
        }

        private AoiEntitySample[] EnsureAoiSampleCapacity(int count)
        {
            if (_aoiSampleBuffer.Length >= count)
            {
                return _aoiSampleBuffer;
            }

            var newCapacity = _aoiSampleBuffer.Length == 0 ? 16 : _aoiSampleBuffer.Length;
            while (newCapacity < count)
            {
                newCapacity = checked(newCapacity * 2);
            }

            _aoiSampleBuffer = new AoiEntitySample[newCapacity];
            return _aoiSampleBuffer;
        }
 
        private static int ResolveMaxEntities(ShooterPureStateSyncSettings settings, int entityBudget, ShooterPureStateInterestScope? interestScope)
        {
            var maxEntities = Math.Min(settings.MaxEntityCount, Math.Max(0, entityBudget));
            if (interestScope.HasValue && interestScope.Value.MaxEntities > 0)
            {
                maxEntities = Math.Min(maxEntities, interestScope.Value.MaxEntities);
            }

            return maxEntities;
        }

        private static bool IsInsideAoiBoundary(float x, float y, ShooterPureStateInterestScope scope)
        {
            var radius = Math.Max(scope.VisibleRadius, scope.BoundaryRadius);
            if (radius <= 0f)
            {
                return true;
            }

            var dx = x - scope.CenterX;
            var dy = y - scope.CenterY;
            return (dx * dx) + (dy * dy) <= radius * radius;
        }

        private static int CreatePlayerPriority(in ShooterPlayerSnapshot player, ShooterPureStateInterestScope? interestScope)
        {
            var priority = player.Alive ? 100 : 10;
            if (!interestScope.HasValue)
            {
                return priority;
            }

            var scope = interestScope.Value;
            if (scope.ObserverPlayerId > 0 && player.PlayerId == scope.ObserverPlayerId)
            {
                return 1000;
            }

            return IsInsideScope(player.X, player.Y, scope) ? priority + 200 : priority;
        }

        private static int CreateBulletPriority(in ShooterBulletSnapshot bullet, ShooterPureStateInterestScope? interestScope)
        {
            if (!interestScope.HasValue)
            {
                return 80;
            }

            var scope = interestScope.Value;
            if (scope.ObserverPlayerId > 0 && bullet.OwnerPlayerId == scope.ObserverPlayerId)
            {
                return 250;
            }

            return IsInsideScope(bullet.X, bullet.Y, scope) ? 180 : 1;
        }

        private static bool IsInsideScope(float x, float y, ShooterPureStateInterestScope scope)
        {
            if (!scope.HasRadius)
            {
                return true;
            }

            return ComputeDistanceSquared(x, y, scope) <= scope.Radius * scope.Radius;
        }

        private static float ComputeDistanceSquared(float x, float y, ShooterPureStateInterestScope? interestScope)
        {
            return interestScope.HasValue ? ComputeDistanceSquared(x, y, interestScope.Value) : 0f;
        }

        private static float ComputeDistanceSquared(float x, float y, ShooterPureStateInterestScope interestScope)
        {
            var dx = x - interestScope.CenterX;
            var dy = y - interestScope.CenterY;
            return (dx * dx) + (dy * dy);
        }

        private static byte CreatePlayerFlags(in ShooterPlayerSnapshot player)
        {
            var flags = (byte)ShooterPureStateEntityFlags.Visible;
            if (player.Alive)
            {
                flags |= ShooterPureStateEntityFlags.Alive;
            }

            return flags;
        }

        private static byte CreatePlayerFlags(in ShooterSveltoPlayerComponent player)
        {
            var flags = (byte)ShooterPureStateEntityFlags.Visible;
            if (player.Alive)
            {
                flags |= ShooterPureStateEntityFlags.Alive;
            }

            return flags;
        }

        private static int CreatePlayerPriority(in ShooterSveltoPlayerComponent player, ShooterPureStateInterestScope? interestScope)
        {
            var priority = player.Alive ? 100 : 10;
            if (!interestScope.HasValue)
            {
                return priority;
            }

            var scope = interestScope.Value;
            if (scope.ObserverPlayerId > 0 && player.PlayerId == scope.ObserverPlayerId)
            {
                return 1000;
            }

            return IsInsideScope(player.X, player.Y, scope) ? priority + 200 : priority;
        }

        private static int CreateBulletPriority(in ShooterSveltoProjectileComponent bullet, ShooterPureStateInterestScope? interestScope)
        {
            if (!interestScope.HasValue)
            {
                return 80;
            }

            var scope = interestScope.Value;
            if (scope.ObserverPlayerId > 0 && bullet.OwnerPlayerId == scope.ObserverPlayerId)
            {
                return 250;
            }

            return IsInsideScope(bullet.X, bullet.Y, scope) ? 180 : 1;
        }

        private static int CreateDeltaKind(bool isFullBaseline)
        {
            return isFullBaseline ? ShooterPureStateDeltaKinds.Spawn : ShooterPureStateDeltaKinds.Update;
        }

        private static int CreateSnapshotKind(bool isFullBaseline, bool isLowFrequencyFrame)
        {
            if (isFullBaseline)
            {
                return ShooterPureStateSnapshotKinds.FullBaseline;
            }

            return isLowFrequencyFrame ? ShooterPureStateSnapshotKinds.LowFrequency : ShooterPureStateSnapshotKinds.Delta;
        }

        private static ShooterPureStateSyncSettings NormalizeSettings(ShooterPureStateSyncSettings settings)
        {
            var defaults = ShooterPureStateSyncSettings.Default;
            return new ShooterPureStateSyncSettings(
                settings.MaxEntityCount > 0 ? settings.MaxEntityCount : defaults.MaxEntityCount,
                settings.ActiveSyncBudget > 0 ? settings.ActiveSyncBudget : defaults.ActiveSyncBudget,
                settings.BaselineIntervalFrames > 0 ? settings.BaselineIntervalFrames : defaults.BaselineIntervalFrames,
                settings.DeltaIntervalFrames > 0 ? settings.DeltaIntervalFrames : defaults.DeltaIntervalFrames,
                settings.LowFrequencyIntervalFrames > 0 ? settings.LowFrequencyIntervalFrames : defaults.LowFrequencyIntervalFrames,
                settings.InterpolationDelayFrames > 0 ? settings.InterpolationDelayFrames : defaults.InterpolationDelayFrames);
        }

        private static int QuantizePosition(float value)
        {
            return (int)MathF.Round(value * PositionScale);
        }

        private static int QuantizeVelocity(float value)
        {
            return (int)MathF.Round(value * VelocityScale);
        }

        private readonly struct ShooterPureStateCandidate : IComparable<ShooterPureStateCandidate>
        {
            public ShooterPureStateCandidate(
                ShooterPureStateEntityDelta entity,
                ShooterPureStateVisibilityHint visibilityHint,
                int priority,
                float distanceSquared,
                int tieBreaker,
                float x,
                float y)
            {
                Entity = entity;
                VisibilityHint = visibilityHint;
                Priority = priority;
                DistanceSquared = distanceSquared;
                TieBreaker = tieBreaker;
                X = x;
                Y = y;
                AoiKey = new AoiEntityKey(entity.EntityKind, entity.EntityId);
            }

            public ShooterPureStateEntityDelta Entity { get; }

            public ShooterPureStateVisibilityHint VisibilityHint { get; }

            public int Priority { get; }

            public float DistanceSquared { get; }

            public int TieBreaker { get; }

            public float X { get; }

            public float Y { get; }

            public AoiEntityKey AoiKey { get; }

            public int CompareTo(ShooterPureStateCandidate other)
            {
                var priority = other.Priority.CompareTo(Priority);
                if (priority != 0)
                {
                    return priority;
                }

                var distance = DistanceSquared.CompareTo(other.DistanceSquared);
                return distance != 0 ? distance : TieBreaker.CompareTo(other.TieBreaker);
            }
        }

        private readonly struct ShooterPureStateWorldSample
        {
            public ShooterPureStateWorldSample(ShooterPureStateEntityDelta entity, float x, float y, bool alive)
            {
                Entity = entity;
                X = x;
                Y = y;
                Alive = alive;
            }

            public ShooterPureStateEntityDelta Entity { get; }

            public float X { get; }

            public float Y { get; }

            public bool Alive { get; }
        }

        private sealed class AoiSampleBufferView : IReadOnlyList<AoiEntitySample>
        {
            private AoiEntitySample[] _buffer = Array.Empty<AoiEntitySample>();
            private int _count;

            public int Count => _count;

            public AoiEntitySample this[int index]
            {
                get
                {
                    if ((uint)index >= (uint)_count)
                    {
                        throw new ArgumentOutOfRangeException(nameof(index));
                    }

                    return _buffer[index];
                }
            }

            public void Reset(AoiEntitySample[] buffer, int count)
            {
                _buffer = buffer ?? Array.Empty<AoiEntitySample>();
                _count = Math.Max(0, Math.Min(count, _buffer.Length));
            }

            public IEnumerator<AoiEntitySample> GetEnumerator()
            {
                for (var i = 0; i < _count; i++)
                {
                    yield return _buffer[i];
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private readonly struct ShooterPureStateSelection
        {
            public ShooterPureStateSelection(
                ShooterPureStateEntityDelta[] entities,
                int entityCount,
                ShooterPureStateVisibilityHint[] visibilityHints,
                int visibilityHintCount)
            {
                Entities = entities;
                EntityCount = entityCount;
                VisibilityHints = visibilityHints;
                VisibilityHintCount = visibilityHintCount;
            }

            public ShooterPureStateEntityDelta[] Entities { get; }

            public int EntityCount { get; }

            public ShooterPureStateVisibilityHint[] VisibilityHints { get; }

            public int VisibilityHintCount { get; }
        }

        private sealed class ShooterObserverReplicationState
        {
            public readonly HashSet<AoiEntityKey> Replicated = new HashSet<AoiEntityKey>();
            public int RotationCursor;

            public void Clear()
            {
                Replicated.Clear();
                RotationCursor = 0;
            }
        }
    }
}
