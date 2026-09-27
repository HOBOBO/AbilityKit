using System;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// 战斗视图事件接收器基类
    /// 提供通用的视图事件处理逻辑
    /// 平台层（如 Unity/Console）可以继承此类并实现抽象方法
    /// </summary>
    public abstract class BaseBattleViewEventSink : IBattleViewEventSink
    {
        /// <summary>
        /// 本地角色 ID
        /// </summary>
        protected int LocalActorId { get; private set; }

        /// <summary>
        /// 是否已初始化
        /// </summary>
        protected bool IsInitialized { get; private set; }

        /// <summary>
        /// 最后处理的帧索引
        /// </summary>
        protected int LastProcessedFrame { get; private set; }

        /// <summary>
        /// 处理进入游戏快照
        /// </summary>
        public virtual void OnEnterGameSnapshot(in FrameSnapshotData snapshot)
        {
            if (!snapshot.EnterGame.HasValue)
            {
                return;
            }

            var enterGame = snapshot.EnterGame;
            LocalActorId = enterGame.LocalPlayerId;
            IsInitialized = true;

            OnEnterGame(enterGame);
        }

        /// <summary>
        /// 处理角色变换快照
        /// </summary>
        public virtual void OnActorTransformSnapshot(in FrameSnapshotData snapshot)
        {
            if (snapshot.ActorTransforms == null || snapshot.ActorTransforms.Count == 0)
            {
                return;
            }

            var transforms = snapshot.ActorTransforms;
            for (int i = 0; i < transforms.Count; i++)
            {
                var transform = transforms[i];
                OnActorTransform(transform.ActorId, transform.PositionX, transform.PositionY, transform.PositionZ, transform.RotationY, transform.Scale);
            }

            LastProcessedFrame = snapshot.FrameIndex;
        }

        /// <summary>
        /// 处理弹道事件快照
        /// </summary>
        public virtual void OnProjectileEventSnapshot(in FrameSnapshotData snapshot)
        {
            if (snapshot.ProjectileEvents == null || snapshot.ProjectileEvents.Count == 0)
            {
                return;
            }

            var events = snapshot.ProjectileEvents;
            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                OnProjectileEvent(in evt);
            }

            LastProcessedFrame = snapshot.FrameIndex;
        }

        /// <summary>
        /// 处理区域事件快照
        /// </summary>
        public virtual void OnAreaEventSnapshot(in FrameSnapshotData snapshot)
        {
            if (snapshot.AreaEvents == null || snapshot.AreaEvents.Count == 0)
            {
                return;
            }

            var events = snapshot.AreaEvents;
            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                OnAreaEvent(in evt);
            }

            LastProcessedFrame = snapshot.FrameIndex;
        }

        /// <summary>
        /// 处理伤害事件快照
        /// </summary>
        public virtual void OnDamageEventSnapshot(in FrameSnapshotData snapshot)
        {
            if (snapshot.DamageEvents == null || snapshot.DamageEvents.Count == 0)
            {
                return;
            }

            var events = snapshot.DamageEvents;
            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                OnDamageEvent(evt.AttackerId, evt.TargetId, evt.SourceId, evt.DamageType, evt.DamageValue, evt.TargetHpAfter, evt.IsKill);
            }

            LastProcessedFrame = snapshot.FrameIndex;
        }

        /// <summary>
        /// 处理表现 Cue 快照
        /// </summary>
        public virtual void OnPresentationCueSnapshot(in FrameSnapshotData snapshot)
        {
            if (snapshot.PresentationCues == null || snapshot.PresentationCues.Count == 0)
            {
                return;
            }

            var cues = snapshot.PresentationCues;
            for (int i = 0; i < cues.Count; i++)
            {
                var cue = cues[i];
                OnPresentationCue(in cue);
            }

            LastProcessedFrame = snapshot.FrameIndex;
        }

        /// <summary>
        /// 处理状态哈希快照
        /// </summary>
        public virtual void OnStateHashSnapshot(in FrameSnapshotData snapshot)
        {
            if (!snapshot.StateHash.HasValue)
            {
                return;
            }

            var stateHash = snapshot.StateHash;
            OnStateHash(stateHash.FrameIndex, stateHash.StateHash);
        }

        /// <summary>
        /// 处理角色生成快照
        /// </summary>
        public virtual void OnActorSpawnSnapshot(in FrameSnapshotData snapshot)
        {
            if (snapshot.ActorSpawns == null || snapshot.ActorSpawns.Count == 0)
            {
                return;
            }

            var spawns = snapshot.ActorSpawns;
            for (int i = 0; i < spawns.Count; i++)
            {
                var spawn = spawns[i];
                OnActorSpawn(spawn);
            }
        }

        /// <summary>
        /// Handles actor removal snapshots.
        /// </summary>
        public virtual void OnActorDespawnSnapshot(in FrameSnapshotData snapshot)
        {
            if (snapshot.ActorDespawns == null || snapshot.ActorDespawns.Count == 0)
            {
                return;
            }

            var despawns = snapshot.ActorDespawns;
            for (int i = 0; i < despawns.Count; i++)
            {
                var despawn = despawns[i];
                OnActorDespawn(in despawn);
            }

            LastProcessedFrame = snapshot.FrameIndex;
        }

        /// <summary>
        /// Handles skill presentation state snapshots.
        /// </summary>
        public virtual void OnSkillStateSnapshot(in FrameSnapshotData snapshot)
        {
            if (snapshot.SkillStates == null || snapshot.SkillStates.Count == 0)
            {
                return;
            }

            var states = snapshot.SkillStates;
            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                OnSkillState(in state);
            }

            LastProcessedFrame = snapshot.FrameIndex;
        }

        /// <summary>
        /// 处理触发器事件
        /// </summary>
        public virtual void OnTriggerEvent(in TriggerEventData evt)
        {
            // 基类不处理具体逻辑，由子类实现
        }

        /// <summary>
        /// 战斗开始
        /// </summary>
        public virtual void OnBattleStart(int frameIndex)
        {
            LastProcessedFrame = frameIndex;
        }

        /// <summary>
        /// 战斗结束
        /// </summary>
        public virtual void OnBattleEnd(int frameIndex, int winTeamId)
        {
        }

        /// <summary>
        /// 帧同步完成
        /// </summary>
        public virtual void OnFrameSyncComplete(int frameIndex)
        {
        }

        // ============== 抽象方法 - 平台实现 ==============

        /// <summary>
        /// 进入游戏处理（子类实现）
        /// </summary>
        protected abstract void OnEnterGame(in EnterGameData data);

        /// <summary>
        /// 角色变换处理（子类实现）
        /// </summary>
        protected abstract void OnActorTransform(int actorId, float x, float y, float z, float rotationY, float scale);

        /// <summary>
        /// 弹道事件处理（子类实现）
        /// </summary>
        protected abstract void OnProjectileEvent(in ProjectileEventData data);

        /// <summary>
        /// 区域事件处理（子类实现）
        /// </summary>
        protected abstract void OnAreaEvent(in AreaEventData data);

        /// <summary>
        /// 伤害事件处理（子类实现）
        /// </summary>
        protected abstract void OnDamageEvent(int attackerId, int targetId, int sourceId, int damageType, int damageValue, int targetHpAfter, bool isKill);

        /// <summary>
        /// 表现 Cue 处理（子类可覆盖）
        /// </summary>
        protected virtual void OnPresentationCue(in PresentationCueData data)
        {
        }

        /// <summary>
        /// 状态哈希处理（子类实现）
        /// </summary>
        protected abstract void OnStateHash(int frameIndex, uint stateHash);

        /// <summary>
        /// 角色生成处理（子类实现）
        /// </summary>
        protected abstract void OnActorSpawn(in ActorSpawnData data);

        /// <summary>
        /// Actor removal handling for platform implementations.
        /// </summary>
        protected virtual void OnActorDespawn(in ActorDespawnData data)
        {
        }

        /// <summary>
        /// Skill state handling for platform implementations.
        /// </summary>
        protected virtual void OnSkillState(in SkillStateData data)
        {
        }
    }
}
