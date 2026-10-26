using System;
using System.Collections.Generic;
using AbilityKit.Core.Mathematics;
using Newtonsoft.Json;

namespace AbilityKit.Demo.Moba.Minions
{
    public static class MobaMinionEvents
    {
        public const string BaseDamage = "minion.base_damage";
        public const string Attack = "minion.attack";
        public const string Killed = "minion.killed";
    }

    public enum MobaMinionArchetype
    {
        None = 0,
        Melee = 1,
        Ranged = 2,
    }

    /// <summary>
    /// Published character ids used by the minion wave runtime.
    /// The character table remains the authority for the referenced attribute
    /// template and model ids; these constants only keep the runtime mapping
    /// stable and discoverable.
    /// </summary>
    public static class MobaMinionArchetypeCatalog
    {
        public const int MeleeCharacterId = 2001;
        public const int RangedCharacterId = 2002;
        public static int GetCharacterId(MobaMinionArchetype archetype)
        {
            return archetype switch
            {
                MobaMinionArchetype.Melee => MeleeCharacterId,
                MobaMinionArchetype.Ranged => RangedCharacterId,
                _ => 0,
            };
        }

    }

    public enum MobaMinionTerminalPolicy
    {
        Despawn = 1,
        BaseDamageEvent = 2,
        Hold = 3,
    }

    public enum MobaMinionAiState
    {
        FollowingRoute = 1,
        AcquiringTarget = 2,
        Attacking = 3,
        Dead = 4,
        Terminal = 5,
    }

    public static class MobaMinionAiStatePolicy
    {
        public static bool ShouldFollowRoute(MobaMinionAiState state) =>
            state == MobaMinionAiState.FollowingRoute;
    }

    public static class MobaMinionLaneSpacingPolicy
    {
        public static bool IsAheadWithinSafeSpacing(
            int actorId,
            float progress,
            int candidateActorId,
            float candidateProgress,
            float safeSpacing,
            float tieEpsilon = 0.0001f)
        {
            if (actorId <= 0 || candidateActorId <= 0 || actorId == candidateActorId ||
                safeSpacing < 0f || tieEpsilon < 0f)
                return false;

            var gap = candidateProgress - progress;
            if (Math.Abs(gap) <= tieEpsilon)
                return candidateActorId < actorId;

            return gap > 0f && gap <= safeSpacing + tieEpsilon;
        }
    }

    public sealed class MobaMinionStallTracker
    {
        private long _blockedSinceMs = -1L;
        private bool _reported;

        public bool Update(bool isBlocked, long elapsedMs, long timeoutMs, out long blockedForMs)
        {
            if (elapsedMs < 0L) throw new ArgumentOutOfRangeException(nameof(elapsedMs));
            if (timeoutMs < 0L) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
            if (!isBlocked)
            {
                Reset();
                blockedForMs = 0L;
                return false;
            }

            if (_blockedSinceMs < 0L) _blockedSinceMs = elapsedMs;
            blockedForMs = elapsedMs - _blockedSinceMs;
            if (_reported || blockedForMs < timeoutMs) return false;
            _reported = true;
            return true;
        }

        public void Reset()
        {
            _blockedSinceMs = -1L;
            _reported = false;
        }

        public long BlockedSinceMs => _blockedSinceMs;
        public bool HasReported => _reported;

        public void Restore(long blockedSinceMs, bool reported)
        {
            if (blockedSinceMs < -1L) throw new ArgumentOutOfRangeException(nameof(blockedSinceMs));
            if (blockedSinceMs < 0L && reported) throw new ArgumentException("Reported stall requires a start time.", nameof(reported));
            _blockedSinceMs = blockedSinceMs;
            _reported = reported;
        }
    }

    public readonly struct MobaMinionRuntimeView
    {
        public MobaMinionRuntimeView(int actorId, long waveId, Team team,
            MobaMinionArchetype archetype, int slot, MobaMinionAiState aiState)
        {
            ActorId = actorId;
            WaveId = waveId;
            Team = team;
            Archetype = archetype;
            Slot = slot;
            AiState = aiState;
        }

        public int ActorId { get; }
        public long WaveId { get; }
        public Team Team { get; }
        public MobaMinionArchetype Archetype { get; }
        public int Slot { get; }
        public MobaMinionAiState AiState { get; }
    }

    public readonly struct MobaMinionPendingRollbackState
    {
        public MobaMinionPendingRollbackState(long waveId, Team team, MobaMinionArchetype archetype,
            long dueElapsedMs, int slot)
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

    public readonly struct MobaMinionActiveRollbackState
    {
        public MobaMinionActiveRollbackState(int actorId, long waveId, Team team,
            MobaMinionArchetype archetype, int slot, MobaMinionAiState aiState,
            int waypointIndex, bool isTerminal, long blockedSinceMs, bool stallReported,
            int blockingActorId)
        {
            ActorId = actorId;
            WaveId = waveId;
            Team = team;
            Archetype = archetype;
            Slot = slot;
            AiState = aiState;
            WaypointIndex = waypointIndex;
            IsTerminal = isTerminal;
            BlockedSinceMs = blockedSinceMs;
            StallReported = stallReported;
            BlockingActorId = blockingActorId;
        }

        public int ActorId { get; }
        public long WaveId { get; }
        public Team Team { get; }
        public MobaMinionArchetype Archetype { get; }
        public int Slot { get; }
        public MobaMinionAiState AiState { get; }
        public int WaypointIndex { get; }
        public bool IsTerminal { get; }
        public long BlockedSinceMs { get; }
        public bool StallReported { get; }
        public int BlockingActorId { get; }
    }

    public readonly struct MobaMinionAttackRollbackState
    {
        public MobaMinionAttackRollbackState(int actorId, long nextAttackAtMs)
        {
            ActorId = actorId;
            NextAttackAtMs = nextAttackAtMs;
        }

        public int ActorId { get; }
        public long NextAttackAtMs { get; }
    }

    public readonly struct MobaMinionWaveRollbackState
    {
        public MobaMinionWaveRollbackState(long gameplayId, int committedWaveCount,
            int nextWaveOrdinal, int lastFrame, long elapsedMs,
            IReadOnlyList<MobaMinionPendingRollbackState> pending,
            IReadOnlyList<MobaMinionActiveRollbackState> active)
        {
            GameplayId = gameplayId;
            CommittedWaveCount = committedWaveCount;
            NextWaveOrdinal = nextWaveOrdinal;
            LastFrame = lastFrame;
            ElapsedMs = elapsedMs;
            Pending = pending ?? throw new ArgumentNullException(nameof(pending));
            Active = active ?? throw new ArgumentNullException(nameof(active));
        }

        public long GameplayId { get; }
        public int CommittedWaveCount { get; }
        public int NextWaveOrdinal { get; }
        public int LastFrame { get; }
        public long ElapsedMs { get; }
        public IReadOnlyList<MobaMinionPendingRollbackState> Pending { get; }
        public IReadOnlyList<MobaMinionActiveRollbackState> Active { get; }
    }

    public readonly struct MobaMinionCombatRollbackState
    {
        public MobaMinionCombatRollbackState(long gameplayId,
            IReadOnlyList<MobaMinionAttackRollbackState> attackCooldowns,
            IReadOnlyList<int> reportedDeaths)
        {
            GameplayId = gameplayId;
            AttackCooldowns = attackCooldowns ?? throw new ArgumentNullException(nameof(attackCooldowns));
            ReportedDeaths = reportedDeaths ?? throw new ArgumentNullException(nameof(reportedDeaths));
        }

        public long GameplayId { get; }
        public IReadOnlyList<MobaMinionAttackRollbackState> AttackCooldowns { get; }
        public IReadOnlyList<int> ReportedDeaths { get; }
    }

    public readonly struct MobaMinionAttackEvent
    {
        public MobaMinionAttackEvent(int attackerActorId, int targetActorId,
            long waveId, MobaMinionArchetype archetype, float damage, int frame)
        {
            AttackerActorId = attackerActorId;
            TargetActorId = targetActorId;
            WaveId = waveId;
            Archetype = archetype;
            Damage = damage;
            Frame = frame;
        }

        public int AttackerActorId { get; }
        public int TargetActorId { get; }
        public long WaveId { get; }
        public MobaMinionArchetype Archetype { get; }
        public float Damage { get; }
        public int Frame { get; }
    }

    public readonly struct MobaMinionKilledEvent
    {
        public MobaMinionKilledEvent(int actorId, int killerActorId, long waveId,
            MobaMinionArchetype archetype, int frame, int rewardPoints = 0)
        {
            ActorId = actorId;
            KillerActorId = killerActorId;
            WaveId = waveId;
            Archetype = archetype;
            Frame = frame;
            RewardPoints = rewardPoints;
        }

        public int ActorId { get; }
        public int KillerActorId { get; }
        public long WaveId { get; }
        public MobaMinionArchetype Archetype { get; }
        public int Frame { get; }
        public int RewardPoints { get; }
    }

    public readonly struct MobaMinionCompositionEntry
    {
        public MobaMinionCompositionEntry(MobaMinionArchetype archetype, int count, int spawnIntervalMs)
        {
            if (archetype == MobaMinionArchetype.None) throw new ArgumentOutOfRangeException(nameof(archetype));
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (spawnIntervalMs < 0) throw new ArgumentOutOfRangeException(nameof(spawnIntervalMs));
            Archetype = archetype;
            Count = count;
            SpawnIntervalMs = spawnIntervalMs;
        }

        public MobaMinionArchetype Archetype { get; }
        public int Count { get; }
        public int SpawnIntervalMs { get; }
    }

    public readonly struct MobaMinionWaveSchedule
    {
        public MobaMinionWaveSchedule(int firstWaveAtMs, int intervalMs,
            IReadOnlyList<MobaMinionCompositionEntry> composition, int maxWaves = 0,
            int maxActiveMinionsPerTeam = 60)
        {
            if (firstWaveAtMs < 0) throw new ArgumentOutOfRangeException(nameof(firstWaveAtMs));
            if (intervalMs <= 0) throw new ArgumentOutOfRangeException(nameof(intervalMs));
            if (composition == null || composition.Count == 0)
                throw new ArgumentException("At least one minion composition entry is required.", nameof(composition));
            if (maxWaves < 0) throw new ArgumentOutOfRangeException(nameof(maxWaves));
            if (maxActiveMinionsPerTeam <= 0) throw new ArgumentOutOfRangeException(nameof(maxActiveMinionsPerTeam));
            FirstWaveAtMs = firstWaveAtMs;
            IntervalMs = intervalMs;
            Composition = composition;
            MaxWaves = maxWaves;
            MaxActiveMinionsPerTeam = maxActiveMinionsPerTeam;
        }

        public int FirstWaveAtMs { get; }
        public int IntervalMs { get; }
        public IReadOnlyList<MobaMinionCompositionEntry> Composition { get; }
        public int MaxWaves { get; }
        public int MaxActiveMinionsPerTeam { get; }
    }

    public readonly struct MobaMinionWavePlan
    {
        public MobaMinionWavePlan(long gameplayId, int waveOrdinal, long dueElapsedMs,
            IReadOnlyList<MobaMinionCompositionEntry> composition)
        {
            GameplayId = gameplayId;
            WaveOrdinal = waveOrdinal;
            DueElapsedMs = dueElapsedMs;
            Composition = composition ?? throw new ArgumentNullException(nameof(composition));
        }

        public long GameplayId { get; }
        public int WaveOrdinal { get; }
        public long DueElapsedMs { get; }
        public IReadOnlyList<MobaMinionCompositionEntry> Composition { get; }

        public long WaveId => (GameplayId << 32) | (uint)WaveOrdinal;
    }

    public sealed class MobaMinionWaveScheduler
    {
        private readonly MobaMinionWaveSchedule _schedule;
        private long _gameplayId;
        private int _nextWaveOrdinal;

        public MobaMinionWaveScheduler(in MobaMinionWaveSchedule schedule)
        {
            _schedule = schedule;
        }

        public int NextWaveOrdinal => _nextWaveOrdinal;

        public void Reset(long gameplayId)
        {
            if (gameplayId <= 0) throw new ArgumentOutOfRangeException(nameof(gameplayId));
            _gameplayId = gameplayId;
            _nextWaveOrdinal = 0;
        }

        public void RestoreNextWaveOrdinal(int nextWaveOrdinal)
        {
            if (_gameplayId <= 0) throw new InvalidOperationException("Scheduler must be reset before restoring state.");
            if (nextWaveOrdinal < 0 || (_schedule.MaxWaves > 0 && nextWaveOrdinal > _schedule.MaxWaves))
                throw new ArgumentOutOfRangeException(nameof(nextWaveOrdinal));
            _nextWaveOrdinal = nextWaveOrdinal;
        }

        public bool TryPeekDueWave(long elapsedMs, out MobaMinionWavePlan plan)
        {
            if (_gameplayId <= 0 || elapsedMs < 0 ||
                (_schedule.MaxWaves > 0 && _nextWaveOrdinal >= _schedule.MaxWaves))
            {
                plan = default;
                return false;
            }

            var dueMs = (long)_schedule.FirstWaveAtMs + (long)_nextWaveOrdinal * _schedule.IntervalMs;
            if (elapsedMs < dueMs)
            {
                plan = default;
                return false;
            }

            plan = new MobaMinionWavePlan(_gameplayId, _nextWaveOrdinal, dueMs, _schedule.Composition);
            return true;
        }

        public bool CommitWave(in MobaMinionWavePlan plan)
        {
            if (_gameplayId <= 0 || plan.GameplayId != _gameplayId ||
                plan.WaveOrdinal != _nextWaveOrdinal ||
                (_schedule.MaxWaves > 0 && _nextWaveOrdinal >= _schedule.MaxWaves))
                return false;

            var dueMs = (long)_schedule.FirstWaveAtMs + (long)_nextWaveOrdinal * _schedule.IntervalMs;
            if (plan.DueElapsedMs != dueMs) return false;
            _nextWaveOrdinal = checked(_nextWaveOrdinal + 1);
            return true;
        }

        public bool TryCommitDueWave(long elapsedMs, out MobaMinionWavePlan plan)
        {
            if (!TryPeekDueWave(elapsedMs, out plan)) return false;
            return CommitWave(in plan);
        }
    }

    public static class MobaMinionWaveCapacityPolicy
    {
        public static bool CanCommitWave(int capacity, int activeCount, int pendingCount,
            IReadOnlyList<MobaMinionCompositionEntry> composition)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (activeCount < 0) throw new ArgumentOutOfRangeException(nameof(activeCount));
            if (pendingCount < 0) throw new ArgumentOutOfRangeException(nameof(pendingCount));
            if (composition == null) throw new ArgumentNullException(nameof(composition));

            var waveCount = 0;
            for (var i = 0; i < composition.Count; i++)
                waveCount = checked(waveCount + composition[i].Count);
            return (long)activeCount + pendingCount + waveCount <= capacity;
        }
    }

    public sealed class MobaLaneRouteCursor
    {
        private readonly Vec3[] _waypoints;
        private readonly float[] _cumulativeDistances;
        private bool _isTerminal;

        public MobaLaneRouteCursor(IReadOnlyList<Vec3> waypoints, float acceptanceRadius, int initialWaypointIndex = 0)
        {
            if (waypoints == null || waypoints.Count < 2)
                throw new ArgumentException("A lane route requires at least two waypoints.", nameof(waypoints));
            if (acceptanceRadius <= 0f || float.IsNaN(acceptanceRadius) || float.IsInfinity(acceptanceRadius))
                throw new ArgumentOutOfRangeException(nameof(acceptanceRadius));
            _waypoints = new Vec3[waypoints.Count];
            for (var i = 0; i < waypoints.Count; i++) _waypoints[i] = waypoints[i];
            _cumulativeDistances = new float[_waypoints.Length];
            for (var i = 1; i < _waypoints.Length; i++)
            {
                _cumulativeDistances[i] = _cumulativeDistances[i - 1] +
                    (float)Math.Sqrt((_waypoints[i] - _waypoints[i - 1]).SqrMagnitude);
            }
            AcceptanceRadius = acceptanceRadius;
            if (initialWaypointIndex < 0 || initialWaypointIndex >= _waypoints.Length)
                throw new ArgumentOutOfRangeException(nameof(initialWaypointIndex));
            WaypointIndex = initialWaypointIndex;
            _isTerminal = initialWaypointIndex == _waypoints.Length - 1;
        }

        public int WaypointIndex { get; private set; }
        public float AcceptanceRadius { get; }
        public bool IsTerminal => _isTerminal;
        public Vec3 CurrentWaypoint => _waypoints[Math.Min(WaypointIndex, _waypoints.Length - 1)];

        public float GetProgress(in Vec3 position)
        {
            var endIndex = Math.Max(1, Math.Min(WaypointIndex, _waypoints.Length - 1));

            var start = _waypoints[endIndex - 1];
            var end = _waypoints[endIndex];
            var segment = end - start;
            var segmentLengthSquared = segment.SqrMagnitude;
            if (segmentLengthSquared <= 0.0001f) return _cumulativeDistances[endIndex];

            var offset = position - start;
            var projection = (offset.X * segment.X + offset.Y * segment.Y + offset.Z * segment.Z) /
                segmentLengthSquared;
            projection = Math.Max(0f, Math.Min(1f, projection));
            var segmentLength = _cumulativeDistances[endIndex] - _cumulativeDistances[endIndex - 1];
            return _cumulativeDistances[endIndex - 1] + projection * segmentLength;
        }

        public bool AdvanceIfReached(in Vec3 position)
        {
            if (IsTerminal) return false;
            var delta = position - CurrentWaypoint;
            if (delta.SqrMagnitude > AcceptanceRadius * AcceptanceRadius) return false;
            if (WaypointIndex >= _waypoints.Length - 1)
            {
                _isTerminal = true;
                return true;
            }

            WaypointIndex++;
            return true;
        }

        public Vec3[] CopyWaypoints()
        {
            var copy = new Vec3[_waypoints.Length];
            Array.Copy(_waypoints, copy, _waypoints.Length);
            return copy;
        }

        public void RestoreState(int waypointIndex, bool isTerminal)
        {
            if (waypointIndex < 0 || waypointIndex >= _waypoints.Length)
                throw new ArgumentOutOfRangeException(nameof(waypointIndex));
            if (isTerminal && waypointIndex != _waypoints.Length - 1)
                throw new ArgumentException("A terminal route cursor must point at its final waypoint.", nameof(isTerminal));
            WaypointIndex = waypointIndex;
            _isTerminal = isTerminal;
        }
    }

    public sealed class MobaMinionRouteDefinition
    {
        public MobaMinionRouteDefinition(int routeId, IReadOnlyList<Vec3> waypoints,
            float acceptanceRadius, MobaMinionTerminalPolicy terminalPolicy)
        {
            if (routeId <= 0) throw new ArgumentOutOfRangeException(nameof(routeId));
            RouteId = routeId;
            Waypoints = waypoints ?? throw new ArgumentNullException(nameof(waypoints));
            AcceptanceRadius = acceptanceRadius;
            TerminalPolicy = terminalPolicy;
        }

        public int RouteId { get; }
        public IReadOnlyList<Vec3> Waypoints { get; }
        public float AcceptanceRadius { get; }
        public MobaMinionTerminalPolicy TerminalPolicy { get; }

        public MobaLaneRouteCursor CreateCursor() => new MobaLaneRouteCursor(Waypoints, AcceptanceRadius);
        public MobaLaneRouteCursor CreateCursor(int initialWaypointIndex) =>
            new MobaLaneRouteCursor(Waypoints, AcceptanceRadius, initialWaypointIndex);
    }

    /// <summary>
    /// Versioned route resource envelope. A route set belongs to exactly one
    /// battle map; keeping that identity beside the routes prevents accidental
    /// reuse when more maps are added.
    /// </summary>
    public sealed class MobaMinionRouteResource
    {
        internal MobaMinionRouteResource(int version, int mapId, int defaultRouteId, bool isLegacy,
            IReadOnlyList<MobaMinionRouteDefinition> routes, in MobaMinionWaveSchedule schedule)
        {
            Version = version;
            MapId = mapId;
            DefaultRouteId = defaultRouteId;
            IsLegacy = isLegacy;
            Routes = routes ?? throw new ArgumentNullException(nameof(routes));
            Schedule = schedule;
        }

        public int Version { get; }
        public int MapId { get; }
        public int DefaultRouteId { get; }
        public bool IsLegacy { get; }
        public IReadOnlyList<MobaMinionRouteDefinition> Routes { get; }
        public MobaMinionWaveSchedule Schedule { get; }
    }

    public static class MobaMinionRouteResourceLoader
    {
        public const int LegacyVersion = 1;
        public const int CurrentVersion = 4;
        private const int LegacyDefaultMapId = 1;

        public static IReadOnlyList<MobaMinionRouteDefinition> Load(string json)
        {
            return LoadResource(json).Routes;
        }

        public static MobaMinionRouteResource LoadResource(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Route resource JSON is required.", nameof(json));
            var source = JsonConvert.DeserializeObject<RouteResource>(json)
                ?? throw new InvalidOperationException("Route resource cannot be null.");
            if (source.Version < LegacyVersion || source.Version > CurrentVersion)
                throw new InvalidOperationException($"Unsupported route resource version {source.Version}.");
            var isLegacy = source.Version == LegacyVersion;
            var mapId = source.MapId > 0 ? source.MapId : (isLegacy ? LegacyDefaultMapId : 0);
            if (mapId <= 0)
                throw new InvalidOperationException("Route resource must declare a positive MapId.");
            if (source.Routes == null || source.Routes.Length == 0) throw new InvalidOperationException("Route resource must contain routes.");
            const int defaultRouteVersion = 3;
            var defaultRouteId = source.DefaultRouteId > 0
                ? source.DefaultRouteId
                : (source.Version < defaultRouteVersion ? source.Routes[0]?.RouteId ?? 0 : 0);
            if (defaultRouteId <= 0)
                throw new InvalidOperationException("Route resource must declare a positive DefaultRouteId.");

            var result = new List<MobaMinionRouteDefinition>(source.Routes.Length);
            var ids = new HashSet<int>();
            for (var i = 0; i < source.Routes.Length; i++)
            {
                var route = source.Routes[i] ?? throw new InvalidOperationException($"Route at index {i} is null.");
                if (route.RouteId <= 0 || !ids.Add(route.RouteId)) throw new InvalidOperationException($"Route id {route.RouteId} is invalid or duplicated.");
                if (route.AcceptanceRadius <= 0f || float.IsNaN(route.AcceptanceRadius) || float.IsInfinity(route.AcceptanceRadius))
                    throw new InvalidOperationException($"Route {route.RouteId} has an invalid acceptance radius.");
                if (!Enum.TryParse(route.TerminalPolicy, true, out MobaMinionTerminalPolicy terminalPolicy) ||
                    !Enum.IsDefined(typeof(MobaMinionTerminalPolicy), terminalPolicy))
                    throw new InvalidOperationException($"Route {route.RouteId} has an invalid terminal policy.");
                if (route.Waypoints == null || route.Waypoints.Length < 2)
                    throw new InvalidOperationException($"Route {route.RouteId} must contain at least two waypoints.");

                var points = new Vec3[route.Waypoints.Length];
                for (var p = 0; p < route.Waypoints.Length; p++)
                {
                    var point = route.Waypoints[p] ?? throw new InvalidOperationException($"Route {route.RouteId} waypoint {p} is null.");
                    points[p] = new Vec3(point.X, point.Y, point.Z);
                    if (p > 0 && (points[p] - points[p - 1]).SqrMagnitude <= 0.0001f)
                        throw new InvalidOperationException($"Route {route.RouteId} contains adjacent duplicate waypoints.");
                }

                result.Add(new MobaMinionRouteDefinition(route.RouteId, points, route.AcceptanceRadius, terminalPolicy));
            }

            result.Sort((left, right) => left.RouteId.CompareTo(right.RouteId));
            if (!result.Exists(route => route.RouteId == defaultRouteId))
                throw new InvalidOperationException($"Route resource default route {defaultRouteId} was not found.");

            var schedule = source.Schedule == null
                ? CreateLegacySchedule(source.Version)
                : ParseSchedule(source.Schedule, source.Version >= CurrentVersion);
            if (source.Version >= 3 && source.Schedule == null)
                throw new InvalidOperationException("Route resource version 3 requires a WaveSchedule.");

            return new MobaMinionRouteResource(source.Version, mapId, defaultRouteId, isLegacy, result, in schedule);
        }

        private static MobaMinionWaveSchedule CreateLegacySchedule(int version)
        {
            if (version >= CurrentVersion)
                throw new InvalidOperationException("Current route resource version requires a WaveSchedule.");

            return new MobaMinionWaveSchedule(
                0,
                30_000,
                new[]
                {
                    new MobaMinionCompositionEntry(MobaMinionArchetype.Melee, 3, 250),
                    new MobaMinionCompositionEntry(MobaMinionArchetype.Ranged, 3, 250),
                }, maxActiveMinionsPerTeam: 60);
        }

        private static MobaMinionWaveSchedule ParseSchedule(WaveScheduleDto source, bool requireCapacity)
        {
            if (source.Composition == null || source.Composition.Length == 0)
                throw new InvalidOperationException("WaveSchedule must contain at least one composition entry.");

            var composition = new MobaMinionCompositionEntry[source.Composition.Length];
            for (var i = 0; i < source.Composition.Length; i++)
            {
                var entry = source.Composition[i] ?? throw new InvalidOperationException($"WaveSchedule composition entry {i} is null.");
                if (!Enum.TryParse(entry.Archetype, true, out MobaMinionArchetype archetype) ||
                    !Enum.IsDefined(typeof(MobaMinionArchetype), archetype) || archetype == MobaMinionArchetype.None)
                    throw new InvalidOperationException($"WaveSchedule composition entry {i} has an invalid archetype '{entry.Archetype}'.");

                composition[i] = new MobaMinionCompositionEntry(archetype, entry.Count, entry.SpawnIntervalMs);
            }

            if (requireCapacity && source.MaxActiveMinionsPerTeam <= 0)
                throw new InvalidOperationException("WaveSchedule must declare a positive MaxActiveMinionsPerTeam.");

            return new MobaMinionWaveSchedule(
                source.FirstWaveAtMs,
                source.IntervalMs,
                composition,
                source.MaxWaves,
                source.MaxActiveMinionsPerTeam > 0 ? source.MaxActiveMinionsPerTeam : 60);
        }

        private sealed class RouteResource
        {
            public int Version { get; set; }
            public int MapId { get; set; }
            public int DefaultRouteId { get; set; }
            public WaveScheduleDto Schedule { get; set; }
            public RouteDto[] Routes { get; set; }
        }

        private sealed class WaveScheduleDto
        {
            public int FirstWaveAtMs { get; set; }
            public int IntervalMs { get; set; }
            public int MaxWaves { get; set; }
            public int MaxActiveMinionsPerTeam { get; set; }
            public WaveCompositionDto[] Composition { get; set; }
        }

        private sealed class WaveCompositionDto
        {
            public string Archetype { get; set; }
            public int Count { get; set; }
            public int SpawnIntervalMs { get; set; }
        }

        private sealed class RouteDto
        {
            public int RouteId { get; set; }
            public float AcceptanceRadius { get; set; }
            public string TerminalPolicy { get; set; }
            public PointDto[] Waypoints { get; set; }
        }

        private sealed class PointDto
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
        }
    }

    public readonly struct MobaMinionBaseDamageEvent
    {
        public MobaMinionBaseDamageEvent(long waveId, int actorId, int sourceTeamId, int targetTeamId, int damage, int frame)
        {
            if (waveId <= 0) throw new ArgumentOutOfRangeException(nameof(waveId));
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (sourceTeamId <= 0) throw new ArgumentOutOfRangeException(nameof(sourceTeamId));
            if (targetTeamId <= 0 || targetTeamId == sourceTeamId) throw new ArgumentOutOfRangeException(nameof(targetTeamId));
            if (damage <= 0) throw new ArgumentOutOfRangeException(nameof(damage));
            WaveId = waveId;
            ActorId = actorId;
            SourceTeamId = sourceTeamId;
            TargetTeamId = targetTeamId;
            Damage = damage;
            Frame = frame;
        }

        // Kept for callers compiled against the Phase 0 contract. TeamId is
        // the source team; the target defaults to the opposing team.
        public MobaMinionBaseDamageEvent(long waveId, int actorId, int teamId, int damage, int frame)
        {
            if (waveId <= 0) throw new ArgumentOutOfRangeException(nameof(waveId));
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (teamId <= 0) throw new ArgumentOutOfRangeException(nameof(teamId));
            if (damage <= 0) throw new ArgumentOutOfRangeException(nameof(damage));
            WaveId = waveId;
            ActorId = actorId;
            SourceTeamId = teamId;
            TargetTeamId = teamId == 1 ? 2 : 1;
            Damage = damage;
            Frame = frame;
        }

        public long WaveId { get; }
        public int ActorId { get; }
        public int SourceTeamId { get; }
        public int TargetTeamId { get; }
        public int TeamId => SourceTeamId;
        public int Damage { get; }
        public int Frame { get; }
    }
}
