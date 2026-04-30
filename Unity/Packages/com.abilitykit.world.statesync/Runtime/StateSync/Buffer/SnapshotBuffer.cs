using System;
using System.Collections.Generic;
using AbilityKit.Ability.StateSync.Snapshot;

namespace AbilityKit.Ability.StateSync.Buffer
{
    public sealed class SnapshotBuffer
    {
        private readonly Dictionary<int, WorldStateSnapshot> _snapshots = new Dictionary<int, WorldStateSnapshot>();
        private readonly List<int> _capturedFrames = new List<int>();
        private readonly int _maxBufferSize;
        private readonly object _lock = new object();

        public int Count => _capturedFrames.Count;
        public int MaxBufferSize => _maxBufferSize;

        public SnapshotBuffer(int maxBufferSize = 128)
        {
            if (maxBufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(maxBufferSize));
            _maxBufferSize = maxBufferSize;
        }

        public void Store(int frame, WorldStateSnapshot snapshot)
        {
            lock (_lock)
            {
                if (_snapshots.ContainsKey(frame))
                {
                    _snapshots[frame] = snapshot.Clone();
                }
                else
                {
                    _snapshots[frame] = snapshot.Clone();
                    _capturedFrames.Add(frame);
                    _capturedFrames.Sort();

                    TrimBuffer();
                }
            }
        }

        public bool TryGet(int frame, out WorldStateSnapshot snapshot)
        {
            lock (_lock)
            {
                if (_snapshots.TryGetValue(frame, out var s))
                {
                    snapshot = s.Clone();
                    return true;
                }
                snapshot = null;
                return false;
            }
        }

        public WorldStateSnapshot Get(int frame)
        {
            TryGet(frame, out var snapshot);
            return snapshot;
        }

        public bool Contains(int frame)
        {
            lock (_lock)
            {
                return _snapshots.ContainsKey(frame);
            }
        }

        public IReadOnlyList<int> GetCapturedFrames()
        {
            lock (_lock)
            {
                return _capturedFrames.ToArray();
            }
        }

        public int GetLatestFrame()
        {
            lock (_lock)
            {
                return _capturedFrames.Count > 0 ? _capturedFrames[_capturedFrames.Count - 1] : -1;
            }
        }

        public int GetEarliestFrame()
        {
            lock (_lock)
            {
                return _capturedFrames.Count > 0 ? _capturedFrames[0] : -1;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _snapshots.Clear();
                _capturedFrames.Clear();
            }
        }

        public bool Remove(int frame)
        {
            lock (_lock)
            {
                if (_snapshots.Remove(frame))
                {
                    _capturedFrames.Remove(frame);
                    return true;
                }
                return false;
            }
        }

        public void RemoveBefore(int frame)
        {
            lock (_lock)
            {
                var framesToRemove = new List<int>();
                foreach (var f in _capturedFrames)
                {
                    if (f < frame) framesToRemove.Add(f);
                }

                foreach (var f in framesToRemove)
                {
                    _snapshots.Remove(f);
                    _capturedFrames.Remove(f);
                }
            }
        }

        public void RemoveAfter(int frame)
        {
            lock (_lock)
            {
                var framesToRemove = new List<int>();
                foreach (var f in _capturedFrames)
                {
                    if (f > frame) framesToRemove.Add(f);
                }

                foreach (var f in framesToRemove)
                {
                    _snapshots.Remove(f);
                    _capturedFrames.Remove(f);
                }
            }
        }

        private void TrimBuffer()
        {
            while (_capturedFrames.Count > _maxBufferSize)
            {
                int earliestFrame = _capturedFrames[0];
                _snapshots.Remove(earliestFrame);
                _capturedFrames.RemoveAt(0);
            }
        }

        public WorldStateSnapshot Interpolate(int frame1, int frame2, int targetFrame)
        {
            if (!TryGet(frame1, out var snap1) || !TryGet(frame2, out var snap2))
                return null;

            if (frame1 == frame2) return snap1.Clone();

            float t = (float)(targetFrame - frame1) / (frame2 - frame1);
            t = System.Math.Max(0f, System.Math.Min(1f, t));

            return InterpolateSnapshots(snap1, snap2, t);
        }

        private WorldStateSnapshot InterpolateSnapshots(WorldStateSnapshot a, WorldStateSnapshot b, float t)
        {
            var result = new WorldStateSnapshot
            {
                Version = WorldStateSnapshot.CurrentVersion,
                Frame = a.Frame,
                Timestamp = (long)(a.Timestamp + (b.Timestamp - a.Timestamp) * t),
                WorldFlags = t < 0.5f ? a.WorldFlags : b.WorldFlags,
                ActiveTriggerCount = t < 0.5f ? a.ActiveTriggerCount : b.ActiveTriggerCount,
                Entities = new List<EntityStateSnapshot>(),
                Projectiles = new List<ProjectileStateSnapshot>(),
                Abilities = new List<AbilityStateSnapshot>()
            };

            var bEntities = new Dictionary<long, EntityStateSnapshot>();
            foreach (var entity in b.Entities)
            {
                bEntities[entity.EntityId] = entity;
            }

            foreach (var entityA in a.Entities)
            {
                if (bEntities.TryGetValue(entityA.EntityId, out var entityB))
                {
                    result.Entities.Add(InterpolateEntity(entityA, entityB, t));
                }
                else
                {
                    result.Entities.Add(entityA);
                }
            }

            var bProjectiles = new Dictionary<long, ProjectileStateSnapshot>();
            foreach (var proj in b.Projectiles)
            {
                bProjectiles[proj.ProjectileId] = proj;
            }

            foreach (var projA in a.Projectiles)
            {
                if (bProjectiles.TryGetValue(projA.ProjectileId, out var projB))
                {
                    result.Projectiles.Add(InterpolateProjectile(projA, projB, t));
                }
                else
                {
                    result.Projectiles.Add(projA);
                }
            }

            return result;
        }

        private EntityStateSnapshot InterpolateEntity(EntityStateSnapshot a, EntityStateSnapshot b, float t)
        {
            return new EntityStateSnapshot(a.EntityId)
            {
                position = LerpVec3(a.position, b.position, t),
                rotation = LerpQuat(a.rotation, b.rotation, t),
                velocity = LerpVec3(a.velocity, b.velocity, t),
                healthPercent = (byte)(a.healthPercent + (b.healthPercent - a.healthPercent) * t),
                StateFlags = t < 0.5f ? a.StateFlags : b.StateFlags,
                ActiveAbilityMask = t < 0.5f ? a.ActiveAbilityMask : b.ActiveAbilityMask,
                Cooldowns = t < 0.5f ? a.Cooldowns : b.Cooldowns,
                BuffTimers = t < 0.5f ? a.BuffTimers : b.BuffTimers,
                TeamId = a.TeamId,
                ControlFlags = t < 0.5f ? a.ControlFlags : b.ControlFlags
            };
        }

        private ProjectileStateSnapshot InterpolateProjectile(ProjectileStateSnapshot a, ProjectileStateSnapshot b, float t)
        {
            return new ProjectileStateSnapshot
            {
                ProjectileId = a.ProjectileId,
                OwnerId = a.OwnerId,
                StartPosition = a.StartPosition,
                CurrentPosition = LerpVec3(a.CurrentPosition, b.CurrentPosition, t),
                Direction = LerpVec3(a.Direction, b.Direction, t),
                Speed = a.Speed + (b.Speed - a.Speed) * t,
                RemainingLifetime = a.RemainingLifetime + (b.RemainingLifetime - a.RemainingLifetime) * t,
                ConfigId = a.ConfigId,
                State = t < 0.5f ? a.State : b.State
            };
        }

        private Vec3 LerpVec3(Vec3 a, Vec3 b, float t)
        {
            return new Vec3(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t,
                a.Z + (b.Z - a.Z) * t);
        }

        private Quat LerpQuat(Quat a, Quat b, float t)
        {
            float dot = a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;
            float s = dot >= 0 ? 1f : -1f;

            return new Quat(
                a.X + (b.X * s - a.X) * t,
                a.Y + (b.Y * s - a.Y) * t,
                a.Z + (b.Z * s - a.Z) * t,
                a.W + (b.W * s - a.W) * t);
        }
    }
}
