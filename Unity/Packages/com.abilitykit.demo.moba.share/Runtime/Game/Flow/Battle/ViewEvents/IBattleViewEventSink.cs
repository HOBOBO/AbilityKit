using System;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// 战斗视图事件接收器接口
    /// 定义平台无关的视图事件处理契约
    /// 
    /// 这是 Share 层定义的核心接口，由各个平台（Unity/Console）实现
    /// 用于接收战斗逻辑层产生的所有视图相关事件
    /// </summary>
    public interface IBattleViewEventSink
    {
        // ============== 快照事件 ==============

        /// <summary>
        /// 处理进入游戏快照
        /// 收到此事件后，视图层需要初始化所有游戏对象
        /// </summary>
        /// <param name="snapshot">快照数据</param>
        void OnEnterGameSnapshot(in FrameSnapshotData snapshot);

        /// <summary>
        /// 处理角色变换快照
        /// 更新所有角色的位置、旋转、缩放
        /// </summary>
        /// <param name="snapshot">快照数据</param>
        void OnActorTransformSnapshot(in FrameSnapshotData snapshot);

        /// <summary>
        /// Handles actor removal snapshots.
        /// </summary>
        void OnActorDespawnSnapshot(in FrameSnapshotData snapshot);

        /// <summary>
        /// Handles platform-neutral skill presentation states.
        /// </summary>
        void OnSkillStateSnapshot(in FrameSnapshotData snapshot);

        /// <summary>
        /// 处理弹道事件快照
        /// 生成/更新/销毁弹道对象
        /// </summary>
        /// <param name="snapshot">快照数据</param>
        void OnProjectileEventSnapshot(in FrameSnapshotData snapshot);

        /// <summary>
        /// 处理区域事件快照
        /// 显示/隐藏/更新区域效果
        /// </summary>
        /// <param name="snapshot">快照数据</param>
        void OnAreaEventSnapshot(in FrameSnapshotData snapshot);

        /// <summary>
        /// 处理伤害事件快照
        /// 播放伤害数字、特效
        /// </summary>
        /// <param name="snapshot">快照数据</param>
        void OnDamageEventSnapshot(in FrameSnapshotData snapshot);

        /// <summary>
        /// 处理表现 Cue 快照
        /// </summary>
        /// <param name="snapshot">快照数据</param>
        void OnPresentationCueSnapshot(in FrameSnapshotData snapshot);
 
        /// <summary>
        /// 处理状态哈希快照（用于调试/验证）
        /// </summary>
        /// <param name="snapshot">快照数据</param>
        void OnStateHashSnapshot(in FrameSnapshotData snapshot);

        // ============== 触发器事件 ==============

        /// <summary>
        /// 处理技能触发事件
        /// </summary>
        /// <param name="evt">触发器事件</param>
        void OnTriggerEvent(in TriggerEventData evt);

        // ============== 生命周期事件 ==============

        /// <summary>
        /// 战斗开始
        /// </summary>
        /// <param name="frameIndex">开始的帧索引</param>
        void OnBattleStart(int frameIndex);

        /// <summary>
        /// 战斗结束
        /// </summary>
        /// <param name="frameIndex">结束的帧索引</param>
        /// <param name="winTeamId">获胜队伍 ID</param>
        void OnBattleEnd(int frameIndex, int winTeamId);

        /// <summary>
        /// 帧同步完成
        /// 所有快照事件已处理完毕
        /// </summary>
        /// <param name="frameIndex">帧索引</param>
        void OnFrameSyncComplete(int frameIndex);
    }

    /// <summary>
    /// 触发器事件数据
    /// 平台无关的触发器事件表示
    /// </summary>
    public readonly struct TriggerEventData
    {
        /// <summary>
        /// 事件类型
        /// </summary>
        public int EventType { get; }

        /// <summary>
        /// 触发器 ID
        /// </summary>
        public int TriggerId { get; }

        /// <summary>
        /// 释放者 ID
        /// </summary>
        public int CasterId { get; }

        /// <summary>
        /// 目标 ID
        /// </summary>
        public int TargetId { get; }

        /// <summary>
        /// 关联技能 ID
        /// </summary>
        public int SkillId { get; }

        /// <summary>
        /// 帧索引
        /// </summary>
        public int FrameIndex { get; }

        /// <summary>
        /// 事件参数（键值对）
        /// </summary>
        public TriggerEventParam[] Params { get; }

        public TriggerEventData(
            int eventType,
            int triggerId,
            int casterId,
            int targetId,
            int skillId,
            int frameIndex,
            TriggerEventParam[] @params = null)
        {
            EventType = eventType;
            TriggerId = triggerId;
            CasterId = casterId;
            TargetId = targetId;
            SkillId = skillId;
            FrameIndex = frameIndex;
            Params = @params ?? Array.Empty<TriggerEventParam>();
        }
    }

    /// <summary>
    /// 触发器事件参数
    /// </summary>
    public readonly struct TriggerEventParam
    {
        public string Key { get; }
        public TriggerEventParamValue Value { get; }

        public TriggerEventParam(string key, in TriggerEventParamValue value)
        {
            Key = key;
            Value = value;
        }
    }

    /// <summary>
    /// 触发器事件参数值
    /// </summary>
    public readonly struct TriggerEventParamValue
    {
        public TriggerEventParamKind Kind { get; }
        public int IntValue { get; }
        public float FloatValue { get; }
        public string StringValue { get; }

        public TriggerEventParamValue(int value)
        {
            Kind = TriggerEventParamKind.Int;
            IntValue = value;
            FloatValue = default;
            StringValue = null;
        }

        public TriggerEventParamValue(float value)
        {
            Kind = TriggerEventParamKind.Float;
            IntValue = default;
            FloatValue = value;
            StringValue = null;
        }

        public TriggerEventParamValue(string value)
        {
            Kind = TriggerEventParamKind.String;
            IntValue = default;
            FloatValue = default;
            StringValue = value;
        }
    }

    /// <summary>
    /// 触发器事件参数类型
    /// </summary>
    public enum TriggerEventParamKind
    {
        Int = 0,
        Float = 1,
        String = 2,
    }
}
