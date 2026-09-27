using System;
using System.Collections.Generic;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// 快照视图事件适配器
    /// 将帧快照分发器的事件适配到视图事件接收器
    /// 参考 view.runtime 的 BattleSnapshotViewAdapter 实现
    /// </summary>
    public sealed class SnapshotViewAdapter : IDisposable
    {
        private readonly FrameSnapshotDispatcher _snapshots;
        private readonly IBattleViewEventSink _sink;

        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        /// <summary>
        /// 构造快照视图适配器
        /// </summary>
        /// <param name="snapshots">快照分发器</param>
        /// <param name="sink">视图事件接收器</param>
        public SnapshotViewAdapter(FrameSnapshotDispatcher snapshots, IBattleViewEventSink sink)
        {
            _snapshots = snapshots;
            _sink = sink;

            if (_snapshots == null || _sink == null) return;

            Subscribe();
        }

        private void Subscribe()
        {
            _subscriptions.Add(_snapshots.Subscribe<EnterGameData>(MobaOpCodes.Snapshot.EnterGame, OnEnterGameSnapshot));
            _subscriptions.Add(_snapshots.Subscribe<ActorTransformData[]>(MobaOpCodes.Snapshot.ActorTransform, OnActorTransformSnapshot));
            _subscriptions.Add(_snapshots.Subscribe<ActorDespawnData[]>(MobaOpCodes.Snapshot.ActorDespawn, OnActorDespawnSnapshot));
            _subscriptions.Add(_snapshots.Subscribe<SkillStateData[]>(MobaOpCodes.Snapshot.SkillState, OnSkillStateSnapshot));
            _subscriptions.Add(_snapshots.Subscribe<ProjectileEventData[]>(MobaOpCodes.Snapshot.ProjectileEvent, OnProjectileEventSnapshot));
            _subscriptions.Add(_snapshots.Subscribe<AreaEventData[]>(MobaOpCodes.Snapshot.AreaEvent, OnAreaEventSnapshot));
            _subscriptions.Add(_snapshots.Subscribe<DamageEventData[]>(MobaOpCodes.Snapshot.DamageEvent, OnDamageEventSnapshot));
            _subscriptions.Add(_snapshots.Subscribe<PresentationCueData[]>(MobaOpCodes.Snapshot.PresentationCue, OnPresentationCueSnapshot));
            _subscriptions.Add(_snapshots.Subscribe<StateHashData>(MobaOpCodes.Snapshot.StateHash, OnStateHashSnapshot));
        }

        private void OnEnterGameSnapshot(int frameIndex, EnterGameData data)
        {
            var snapshot = new FrameSnapshotData(
                frameIndex, 
                timestamp: 0, 
                SnapshotType.Full, 
                enterGame: data);
            _sink.OnEnterGameSnapshot(in snapshot);
        }

        private void OnActorTransformSnapshot(int frameIndex, ActorTransformData[] data)
        {
            var snapshot = new FrameSnapshotData(
                frameIndex, 
                timestamp: 0, 
                SnapshotType.Delta, 
                actorTransforms: data);
            _sink.OnActorTransformSnapshot(in snapshot);
        }

        private void OnActorDespawnSnapshot(int frameIndex, ActorDespawnData[] data)
        {
            var snapshot = new FrameSnapshotData(
                frameIndex,
                timestamp: 0,
                SnapshotType.Delta,
                actorDespawns: data);
            _sink.OnActorDespawnSnapshot(in snapshot);
        }

        private void OnSkillStateSnapshot(int frameIndex, SkillStateData[] data)
        {
            var snapshot = new FrameSnapshotData(
                frameIndex,
                timestamp: 0,
                SnapshotType.Delta,
                skillStates: data);
            _sink.OnSkillStateSnapshot(in snapshot);
        }

        private void OnProjectileEventSnapshot(int frameIndex, ProjectileEventData[] data)
        {
            var snapshot = new FrameSnapshotData(
                frameIndex, 
                timestamp: 0, 
                SnapshotType.Delta, 
                projectileEvents: data);
            _sink.OnProjectileEventSnapshot(in snapshot);
        }

        private void OnAreaEventSnapshot(int frameIndex, AreaEventData[] data)
        {
            var snapshot = new FrameSnapshotData(
                frameIndex, 
                timestamp: 0, 
                SnapshotType.Delta, 
                areaEvents: data);
            _sink.OnAreaEventSnapshot(in snapshot);
        }

        private void OnDamageEventSnapshot(int frameIndex, DamageEventData[] data)
        {
            var snapshot = new FrameSnapshotData(
                frameIndex, 
                timestamp: 0, 
                SnapshotType.Delta, 
                damageEvents: data);
            _sink.OnDamageEventSnapshot(in snapshot);
        }

        private void OnPresentationCueSnapshot(int frameIndex, PresentationCueData[] data)
        {
            var snapshot = new FrameSnapshotData(
                frameIndex,
                timestamp: 0,
                SnapshotType.Delta,
                presentationCues: data);
            _sink.OnPresentationCueSnapshot(in snapshot);
        }

        private void OnStateHashSnapshot(int frameIndex, StateHashData data)
        {
            var snapshot = new FrameSnapshotData(
                frameIndex, 
                timestamp: 0, 
                SnapshotType.Delta, 
                stateHash: data);
            _sink.OnStateHashSnapshot(in snapshot);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            for (int i = 0; i < _subscriptions.Count; i++)
            {
                _subscriptions[i]?.Dispose();
            }
            _subscriptions.Clear();
        }
    }
}
