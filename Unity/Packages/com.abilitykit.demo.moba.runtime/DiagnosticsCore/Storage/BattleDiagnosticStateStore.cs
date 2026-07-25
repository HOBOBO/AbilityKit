using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    /// <summary>
    /// 平台无关的世界/角色状态快照存储。
    /// 与 <see cref="BattleDiagnosticEventRingStore"/> 分离：事件 Store 存储变化历史，
    /// 状态 Store 只保留最近一次采样的当前帧快照，供 Inspector/Overview/Battlefield 查询。
    /// </summary>
    public interface IBattleDiagnosticStateReadStore
    {
        BattleDiagnosticSessionScope Scope { get; }
        long Revision { get; }
        int ActorCount { get; }
        int SnapshotFrame { get; }

        BattleDiagnosticWorldSummary? QueryWorld(int frame);
        BattleDiagnosticQueryResult<BattleDiagnosticActorSummary> QueryActors(long requestId, int frame);
    }

    public interface IBattleDiagnosticStateStore : IBattleDiagnosticStateReadStore
    {
        bool IsFrozen { get; }

        bool TryReplaceSnapshot(
            BattleDiagnosticWorldSummary world,
            IReadOnlyList<BattleDiagnosticActorSummary> actors);
        bool TryReplaceWorld(BattleDiagnosticWorldSummary world);
        bool TryReplaceActors(IReadOnlyList<BattleDiagnosticActorSummary> actors);
        void SetFrozen(bool frozen);
        void Clear();
    }

    public sealed class BattleDiagnosticStateStore :
        IBattleDiagnosticStateStore,
        IBattleDiagnosticStateSnapshotSource
    {
        private BattleDiagnosticWorldSummary _world;
        private List<BattleDiagnosticActorSummary> _actors;
        private long _revision;
        private bool _isFrozen;

        public BattleDiagnosticStateStore(BattleDiagnosticSessionScope scope)
        {
            if (!scope.IsValid)
            {
                throw new ArgumentException("A valid session scope is required.", nameof(scope));
            }

            Scope = scope;
            _actors = new List<BattleDiagnosticActorSummary>();
        }

        public BattleDiagnosticSessionScope Scope { get; }
        public long Revision => _revision;
        public bool IsFrozen => _isFrozen;
        public int ActorCount => _actors.Count;
        public int SnapshotFrame => HasWorld ? _world.Frame : BattleDiagnosticFrames.Invalid;

        public bool TryReplaceSnapshot(
            BattleDiagnosticWorldSummary world,
            IReadOnlyList<BattleDiagnosticActorSummary> actors)
        {
            if (_isFrozen || actors == null || world.Scope != Scope || world.ActorCount != actors.Count)
            {
                return false;
            }

            var replacement = new List<BattleDiagnosticActorSummary>(actors.Count);
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i].Scope != Scope || actors[i].Frame != world.Frame)
                {
                    return false;
                }

                replacement.Add(actors[i]);
            }

            _world = world;
            _actors = replacement;
            _revision++;
            return true;
        }

        public bool TryReplaceWorld(BattleDiagnosticWorldSummary world)
        {
            if (_isFrozen || world.Scope != Scope)
            {
                return false;
            }

            _world = world;
            _revision++;
            return true;
        }

        public bool TryReplaceActors(IReadOnlyList<BattleDiagnosticActorSummary> actors)
        {
            if (_isFrozen || actors == null)
            {
                return false;
            }

            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i].Scope != Scope)
                {
                    return false;
                }
            }

            _actors.Clear();
            for (int i = 0; i < actors.Count; i++)
            {
                _actors.Add(actors[i]);
            }

            _revision++;
            return true;
        }

        public void SetFrozen(bool frozen)
        {
            _isFrozen = frozen;
        }

        public void Clear()
        {
            if (_actors.Count == 0 && !HasWorld)
            {
                return;
            }

            _world = default;
            _actors.Clear();
            _revision++;
        }

        public BattleDiagnosticStateTrackSnapshot CaptureStateSnapshot()
        {
            var world = HasWorld ? _world : (BattleDiagnosticWorldSummary?)null;
            return new BattleDiagnosticStateTrackSnapshot(
                _revision,
                SnapshotFrame,
                world,
                new List<BattleDiagnosticActorSummary>(_actors));
        }

        public BattleDiagnosticWorldSummary? QueryWorld(int frame)
        {
            if (!BattleDiagnosticFrames.IsValid(frame) || !HasWorld)
            {
                return null;
            }

            return frame == 0 || frame == _world.Frame
                ? _world
                : (BattleDiagnosticWorldSummary?)null;
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorSummary> QueryActors(
            long requestId,
            int frame)
        {
            if (requestId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requestId));
            }

            if (!BattleDiagnosticFrames.IsValid(frame))
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticActorSummary>.Unavailable(
                    requestId,
                    _revision,
                    BattleDiagnosticDataAvailability.NotProduced,
                    "Invalid frame.");
            }

            if (!HasWorld)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticActorSummary>.Unavailable(
                    requestId,
                    _revision,
                    BattleDiagnosticDataAvailability.NotProduced,
                    "No state snapshot has been sampled yet.");
            }

            if (frame != 0 && frame != _world.Frame)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticActorSummary>.Unavailable(
                    requestId,
                    _revision,
                    BattleDiagnosticDataAvailability.NotCaptured,
                    $"Requested frame {frame} is unavailable; latest-only snapshot is frame {_world.Frame}.");
            }

            var copy = new List<BattleDiagnosticActorSummary>(_actors);
            return BattleDiagnosticQueryResult<BattleDiagnosticActorSummary>.FromItems(
                requestId,
                _revision,
                copy,
                false);
        }

        private bool HasWorld => !_world.Equals(default(BattleDiagnosticWorldSummary));
    }
}
