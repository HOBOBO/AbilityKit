using System;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// 触发器视图事件桥接器
    /// 将触发器系统的事件桥接到视图事件接收器
    /// 参考 view.runtime 的 BattleTriggerEventViewBridge 实现
    /// </summary>
    public sealed class TriggerEventBridge : IDisposable
    {
        private readonly IBattleViewEventSink _sink;
        private bool _isEnabled = true;

        /// <summary>
        /// 构造触发器事件桥接器
        /// </summary>
        /// <param name="sink">视图事件接收器</param>
        public TriggerEventBridge(IBattleViewEventSink sink)
        {
            _sink = sink;
        }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        /// <summary>
        /// 处理触发器事件
        /// 由触发器系统在事件发生时调用
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="triggerId">触发器 ID</param>
        /// <param name="casterId">释放者 ID</param>
        /// <param name="targetId">目标 ID</param>
        /// <param name="skillId">技能 ID</param>
        /// <param name="frameIndex">帧索引</param>
        /// <param name="param0">参数0</param>
        /// <param name="param1">参数1</param>
        /// <param name="param2">参数2</param>
        /// <param name="param3">参数3</param>
        public void OnTriggerEvent(int eventType, int triggerId, int casterId, int targetId, int skillId, int frameIndex,
            int param0 = 0, int param1 = 0, int param2 = 0, int param3 = 0)
        {
            if (!_isEnabled || _sink == null) return;

            var evt = new TriggerEventData(eventType, triggerId, casterId, targetId, skillId, frameIndex, null);
            _sink.OnTriggerEvent(in evt);
        }

        /// <summary>
        /// 处理伤害结果事件
        /// </summary>
        public void OnDamageResult(int targetId, int damageType, int damageValue, int targetHpAfter, bool isKill)
        {
            if (!_isEnabled || _sink == null) return;

            var damageData = new DamageEventData(
                attackerId: 0,
                targetId: targetId,
                sourceId: 0,
                damageType: damageType,
                damageValue: damageValue,
                targetHpAfter: targetHpAfter,
                isKill: isKill
            );

            // 构造快照数据
            var snapshot = new FrameSnapshotData(
                frameIndex: 0,
                timestamp: 0,
                type: SnapshotType.Delta,
                enterGame: default,
                actorTransforms: null,
                projectileEvents: null,
                areaEvents: null,
                damageEvents: new DamageEventData[] { damageData },
                stateHash: default
            );

            _sink.OnDamageEventSnapshot(in snapshot);
        }

        /// <summary>
        /// 处理弹道命中事件
        /// </summary>
        public void OnProjectileHit(int projectileId, int ownerId, int targetId, float x, float y, float z)
        {
            if (!_isEnabled || _sink == null) return;

            var projectileData = new ProjectileEventData(
                kind: ProjectilePresentationEventKind.Hit,
                projectileActorId: projectileId,
                ownerActorId: ownerId,
                templateId: 0,
                launcherActorId: ownerId,
                rootActorId: ownerId,
                x: x, y: y, z: z,
                hitCollider: targetId,
                exitReason: 0,
                projectileId: projectileId,
                forwardX: 0,
                forwardY: 0,
                forwardZ: 0
            );

            var snapshot = new FrameSnapshotData(
                frameIndex: 0,
                timestamp: 0,
                type: SnapshotType.Delta,
                enterGame: default,
                actorTransforms: null,
                projectileEvents: new ProjectileEventData[] { projectileData },
                areaEvents: null,
                damageEvents: null,
                stateHash: default
            );

            _sink.OnProjectileEventSnapshot(in snapshot);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _isEnabled = false;
        }
    }

    /// <summary>
    /// 触发器事件处理器
    /// 提供通用的触发器事件处理逻辑
    /// </summary>
    public sealed class TriggerEventHandler
    {
        private readonly IBattleViewEventSink _sink;
        private readonly int _currentFrame;

        public TriggerEventHandler(IBattleViewEventSink sink, int currentFrame)
        {
            _sink = sink;
            _currentFrame = currentFrame;
        }

        /// <summary>
        /// 处理技能释放事件
        /// </summary>
        public void HandleSkillCast(int skillId, int casterId, int targetId)
        {
            if (_sink == null) return;

            var evt = new TriggerEventData(
                eventType: (int)TriggerEventType.SkillCast,
                triggerId: skillId,
                casterId: casterId,
                targetId: targetId,
                skillId: skillId,
                frameIndex: _currentFrame
            );

            _sink.OnTriggerEvent(in evt);
        }

        /// <summary>
        /// 处理技能命中事件
        /// </summary>
        public void HandleSkillHit(int skillId, int casterId, int targetId)
        {
            if (_sink == null) return;

            var evt = new TriggerEventData(
                eventType: (int)TriggerEventType.SkillHit,
                triggerId: skillId,
                casterId: casterId,
                targetId: targetId,
                skillId: skillId,
                frameIndex: _currentFrame
            );

            _sink.OnTriggerEvent(in evt);
        }

        /// <summary>
        /// 处理单位死亡事件
        /// </summary>
        public void HandleUnitDeath(int unitId, int killerId)
        {
            if (_sink == null) return;

            var evt = new TriggerEventData(
                eventType: (int)TriggerEventType.UnitDeath,
                triggerId: 0,
                casterId: killerId,
                targetId: unitId,
                skillId: 0,
                frameIndex: _currentFrame
            );

            _sink.OnTriggerEvent(in evt);
        }
    }

    /// <summary>
    /// 触发器事件类型
    /// </summary>
    public static class TriggerEventType
    {
        public const int SkillCast = 1;
        public const int SkillHit = 2;
        public const int SkillEnd = 3;
        public const int SkillInterrupt = 4;
        public const int UnitDeath = 10;
        public const int UnitRevive = 11;
        public const int BuffApply = 20;
        public const int BuffRemove = 21;
        public const int BuffStack = 22;
    }
}
