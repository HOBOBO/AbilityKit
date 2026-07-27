using System;
using System.Collections.Generic;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Network.Runtime.LagCompensation
{
    /// <summary>
    /// Configuration for server-side rewind lag compensation.
    /// </summary>
    public readonly struct ServerRewindLagCompensationConfig
    {
        public readonly int MaxHistoryFrames;
        public readonly int MaxRewindFrames;
        public readonly float HitRadiusPadding;

        public ServerRewindLagCompensationConfig(int maxHistoryFrames = 120, int maxRewindFrames = 30, float hitRadiusPadding = 0f)
        {
            if (maxHistoryFrames <= 0) throw new ArgumentOutOfRangeException(nameof(maxHistoryFrames));
            if (maxRewindFrames < 0) throw new ArgumentOutOfRangeException(nameof(maxRewindFrames));
            if (hitRadiusPadding < 0f) throw new ArgumentOutOfRangeException(nameof(hitRadiusPadding));

            MaxHistoryFrames = maxHistoryFrames;
            MaxRewindFrames = maxRewindFrames;
            HitRadiusPadding = hitRadiusPadding;
        }

        public static ServerRewindLagCompensationConfig Default => new ServerRewindLagCompensationConfig(
            maxHistoryFrames: 120,
            maxRewindFrames: 30,
            hitRadiusPadding: 0f);
    }

    /// <summary>
    /// Sphere hitbox snapshot for one server-authoritative entity at a captured frame.
    /// </summary>
    public readonly struct LagCompensatedEntitySnapshot
    {
        public readonly int EntityId;
        public readonly Vec3 Position;
        public readonly float Radius;
        public readonly int LayerMask;
        public readonly bool IsAlive;

        public LagCompensatedEntitySnapshot(int entityId, in Vec3 position, float radius, int layerMask = -1, bool isAlive = true)
        {
            if (radius < 0f) throw new ArgumentOutOfRangeException(nameof(radius));

            EntityId = entityId;
            Position = position;
            Radius = radius;
            LayerMask = layerMask;
            IsAlive = isAlive;
        }
    }

    /// <summary>
    /// Input query for evaluating a client-reported hit against rewound server history.
    /// </summary>
    public readonly struct LagCompensationQuery
    {
        public readonly int ShooterEntityId;
        public readonly Vec3 Origin;
        public readonly Vec3 Direction;
        public readonly float MaxDistance;
        public readonly int TargetLayerMask;
        public readonly int RewindFrame;
        public readonly int ServerReceiveFrame;

        public LagCompensationQuery(
            int shooterEntityId,
            in Vec3 origin,
            in Vec3 direction,
            float maxDistance,
            int targetLayerMask,
            int rewindFrame,
            int serverReceiveFrame)
        {
            ShooterEntityId = shooterEntityId;
            Origin = origin;
            Direction = direction;
            MaxDistance = maxDistance;
            TargetLayerMask = targetLayerMask;
            RewindFrame = rewindFrame;
            ServerReceiveFrame = serverReceiveFrame;
        }
    }

    public enum LagCompensationResultReason
    {
        None = 0,
        Hit = 1,
        Miss = 2,
        InvalidQuery = 3,
        RewindWindowExceeded = 4,
        HistoryUnavailable = 5
    }

    /// <summary>
    /// Deterministic result of a server-side rewound hit evaluation.
    /// </summary>
    public readonly struct LagCompensationHitResult
    {
        public readonly bool Accepted;
        public readonly LagCompensationResultReason Reason;
        public readonly int RequestedFrame;
        public readonly int EvaluatedFrame;
        public readonly int HitEntityId;
        public readonly float Distance;
        public readonly Vec3 Point;

        public LagCompensationHitResult(
            bool accepted,
            LagCompensationResultReason reason,
            int requestedFrame,
            int evaluatedFrame,
            int hitEntityId,
            float distance,
            in Vec3 point)
        {
            Accepted = accepted;
            Reason = reason;
            RequestedFrame = requestedFrame;
            EvaluatedFrame = evaluatedFrame;
            HitEntityId = hitEntityId;
            Distance = distance;
            Point = point;
        }

        public static LagCompensationHitResult Reject(LagCompensationResultReason reason, int requestedFrame, int evaluatedFrame = -1)
        {
            return new LagCompensationHitResult(false, reason, requestedFrame, evaluatedFrame, 0, 0f, Vec3.Zero);
        }
    }

    /// <summary>
    /// Framework-level server rewind helper for favor-the-shooter hit validation.
    /// Demos provide authoritative entity snapshots; this service owns history and deterministic ray-sphere evaluation.
    /// </summary>
    public sealed class ServerRewindLagCompensationService
    {
        private readonly ServerRewindLagCompensationConfig _config;
        private readonly FrameSnapshot[] _history;
        private readonly List<LagCompensatedEntitySnapshot[]> _recycledEntityBuffers;
        private int _historyStart;
        private int _historyCount;

        public ServerRewindLagCompensationService()
            : this(ServerRewindLagCompensationConfig.Default)
        {
        }

        public ServerRewindLagCompensationService(ServerRewindLagCompensationConfig config)
        {
            if (config.MaxHistoryFrames <= 0) throw new ArgumentOutOfRangeException(nameof(config));

            _config = config;
            _history = new FrameSnapshot[config.MaxHistoryFrames];
            _recycledEntityBuffers = new List<LagCompensatedEntitySnapshot[]>(config.MaxHistoryFrames);
        }

        public NetworkSyncModel SyncModel => NetworkSyncModel.ServerRewindLagCompensation;

        public int CapturedFrameCount => _historyCount;

        public int OldestFrame => _historyCount == 0 ? -1 : GetHistory(0).Frame;

        public int LatestFrame => _historyCount == 0 ? -1 : GetHistory(_historyCount - 1).Frame;

        public void RecordFrame(int frame, IReadOnlyList<LagCompensatedEntitySnapshot> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var existing = FindExactFrameIndex(frame);
            if (existing >= 0)
            {
                var replacement = CopyEntities(entities, GetHistory(existing).Entities);
                SetHistory(existing, new FrameSnapshot(frame, replacement));
                return;
            }

            // A full history retains the newest frames. Recording an older frame would be
            // immediately trimmed by the original sorted-list implementation.
            if (_historyCount == _history.Length && frame < GetHistory(0).Frame)
            {
                return;
            }

            LagCompensatedEntitySnapshot[] reusable = null;
            if (_historyCount == _history.Length)
            {
                reusable = GetHistory(0).Entities;
                _history[_historyStart] = default;
                _historyStart = (_historyStart + 1) % _history.Length;
                _historyCount--;
            }

            var insertAt = FindInsertionIndex(frame);
            for (var i = _historyCount; i > insertAt; i--)
            {
                SetHistory(i, GetHistory(i - 1));
            }

            SetHistory(insertAt, new FrameSnapshot(frame, CopyEntities(entities, reusable)));
            _historyCount++;
        }

        public bool TryEvaluateHit(in LagCompensationQuery query, out LagCompensationHitResult result)
        {
            if (query.Direction.SqrMagnitude <= 0f || query.MaxDistance <= 0f)
            {
                result = LagCompensationHitResult.Reject(LagCompensationResultReason.InvalidQuery, query.RewindFrame);
                return false;
            }

            if (query.ServerReceiveFrame - query.RewindFrame > _config.MaxRewindFrames)
            {
                result = LagCompensationHitResult.Reject(LagCompensationResultReason.RewindWindowExceeded, query.RewindFrame);
                return false;
            }

            var frameIndex = FindFloorFrameIndex(query.RewindFrame);
            if (frameIndex < 0)
            {
                result = LagCompensationHitResult.Reject(LagCompensationResultReason.HistoryUnavailable, query.RewindFrame);
                return false;
            }

            var frame = GetHistory(frameIndex);
            var direction = query.Direction.Normalized;
            var bestDistance = float.PositiveInfinity;
            var bestEntity = 0;
            var bestPoint = Vec3.Zero;

            for (var i = 0; i < frame.Entities.Length; i++)
            {
                var entity = frame.Entities[i];
                if (!entity.IsAlive) continue;
                if (entity.EntityId == query.ShooterEntityId) continue;
                if ((entity.LayerMask & query.TargetLayerMask) == 0) continue;

                var radius = entity.Radius + _config.HitRadiusPadding;
                if (!TryRaycastSphere(in query.Origin, in direction, query.MaxDistance, in entity.Position, radius, out var distance)) continue;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                bestEntity = entity.EntityId;
                bestPoint = query.Origin + direction * distance;
            }

            if (bestEntity == 0)
            {
                result = new LagCompensationHitResult(
                    accepted: false,
                    reason: LagCompensationResultReason.Miss,
                    requestedFrame: query.RewindFrame,
                    evaluatedFrame: frame.Frame,
                    hitEntityId: 0,
                    distance: 0f,
                    point: Vec3.Zero);
                return false;
            }

            result = new LagCompensationHitResult(
                accepted: true,
                reason: LagCompensationResultReason.Hit,
                requestedFrame: query.RewindFrame,
                evaluatedFrame: frame.Frame,
                hitEntityId: bestEntity,
                distance: bestDistance,
                point: bestPoint);
            return true;
        }

        public void Clear()
        {
            for (var i = 0; i < _historyCount; i++)
            {
                var physicalIndex = ToPhysicalIndex(i);
                RecycleBuffer(_history[physicalIndex].Entities);
                _history[physicalIndex] = default;
            }

            _historyStart = 0;
            _historyCount = 0;
        }

        private int FindExactFrameIndex(int frame)
        {
            for (var i = 0; i < _historyCount; i++)
            {
                if (GetHistory(i).Frame == frame) return i;
            }

            return -1;
        }

        private int FindFloorFrameIndex(int frame)
        {
            var best = -1;
            for (var i = 0; i < _historyCount; i++)
            {
                if (GetHistory(i).Frame > frame) break;
                best = i;
            }

            return best;
        }

        private int FindInsertionIndex(int frame)
        {
            for (var i = 0; i < _historyCount; i++)
            {
                if (GetHistory(i).Frame > frame)
                {
                    return i;
                }
            }

            return _historyCount;
        }

        private FrameSnapshot GetHistory(int logicalIndex)
        {
            return _history[ToPhysicalIndex(logicalIndex)];
        }

        private void SetHistory(int logicalIndex, in FrameSnapshot snapshot)
        {
            _history[ToPhysicalIndex(logicalIndex)] = snapshot;
        }

        private int ToPhysicalIndex(int logicalIndex)
        {
            return (_historyStart + logicalIndex) % _history.Length;
        }

        private LagCompensatedEntitySnapshot[] CopyEntities(
            IReadOnlyList<LagCompensatedEntitySnapshot> entities,
            LagCompensatedEntitySnapshot[] preferred)
        {
            var buffer = AcquireBuffer(entities.Count, preferred);
            for (var i = 0; i < entities.Count; i++)
            {
                buffer[i] = entities[i];
            }

            return buffer;
        }

        private LagCompensatedEntitySnapshot[] AcquireBuffer(
            int count,
            LagCompensatedEntitySnapshot[] preferred)
        {
            if (preferred != null && preferred.Length == count)
            {
                return preferred;
            }

            RecycleBuffer(preferred);
            if (count == 0)
            {
                return Array.Empty<LagCompensatedEntitySnapshot>();
            }

            for (var i = _recycledEntityBuffers.Count - 1; i >= 0; i--)
            {
                var candidate = _recycledEntityBuffers[i];
                if (candidate.Length != count)
                {
                    continue;
                }

                _recycledEntityBuffers.RemoveAt(i);
                return candidate;
            }

            return new LagCompensatedEntitySnapshot[count];
        }

        private void RecycleBuffer(LagCompensatedEntitySnapshot[] buffer)
        {
            if (buffer == null || buffer.Length == 0 || _recycledEntityBuffers.Count >= _config.MaxHistoryFrames)
            {
                return;
            }

            _recycledEntityBuffers.Add(buffer);
        }

        private static bool TryRaycastSphere(
            in Vec3 origin,
            in Vec3 direction,
            float maxDistance,
            in Vec3 center,
            float radius,
            out float distance)
        {
            var toCenter = center - origin;
            var projection = Vec3.Dot(in toCenter, in direction);
            var radiusSqr = radius * radius;
            var closestSqr = toCenter.SqrMagnitude - projection * projection;

            if (closestSqr > radiusSqr)
            {
                distance = 0f;
                return false;
            }

            var offset = (float)Math.Sqrt(Math.Max(0f, radiusSqr - closestSqr));
            var entry = projection - offset;
            var exit = projection + offset;
            distance = entry >= 0f ? entry : exit;

            return distance >= 0f && distance <= maxDistance;
        }

        private readonly struct FrameSnapshot
        {
            public readonly int Frame;
            public readonly LagCompensatedEntitySnapshot[] Entities;

            public FrameSnapshot(int frame, LagCompensatedEntitySnapshot[] entities)
            {
                Frame = frame;
                Entities = entities;
            }
        }
    }
}
