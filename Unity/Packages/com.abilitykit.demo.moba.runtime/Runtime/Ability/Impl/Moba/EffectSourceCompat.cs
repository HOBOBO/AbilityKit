using System;
using AbilityKit.Trace;

namespace AbilityKit.Ability.Impl.Moba
{
    /// <summary>
    /// 效果溯源种类枚举
    /// </summary>
    public enum EffectSourceKind
    {
        None = 0,
        SkillCast = 1,
        Buff = 2,
        Effect = 3,
        TriggerAction = 4,
        System = 5,
        Projectile = 6,
        Summon = 7,
    }

    /// <summary>
    /// 效果溯源结束原因枚举
    /// </summary>
    public enum EffectSourceEndReason
    {
        None = 0,
        Completed = 1,
        Cancelled = 2,
        Expired = 3,
        Dispelled = 4,
        Dead = 5,
        Replaced = 6,
        Interrupted = 7,
        Overridden = 8,
    }
}

namespace AbilityKit.Ability.Impl.Moba.EffectSource
{
    using AbilityKit.Ability.Impl.Moba;
    using AbilityKit.Ability.World.Services;

    /// <summary>
    /// Moba 溯源元数据
    /// </summary>
    public sealed class MobaTraceMetadata : TraceMetadata
    {
        public int BuffId;
        public int SkillId;
        public int Level;
        public long SourceActorId;
        public long TargetActorId;
        public long OriginContextId;
        public string DebugInfo;
    }

    /// <summary>
    /// Moba 溯源注册表
    /// 基于 AbilityKit.Trace.TraceTreeRegistry，提供与旧 EffectSourceRegistry 兼容的 API
    /// </summary>
    public sealed class MobaTraceRegistry : TraceTreeRegistry<MobaTraceMetadata>, IService
    {
        public MobaTraceRegistry() : base(null)
        {
        }

        public MobaTraceRegistry(ITraceMetadataStore<MobaTraceMetadata> metadataStore) : base(metadataStore)
        {
        }

        /// <summary>
        /// 创建根节点（兼容旧 API）
        /// </summary>
        public long CreateRoot(EffectSourceKind kind, int configId, int sourceActorId, int targetActorId, int frame, object originSource, object originTarget)
        {
            return CreateRoot(
                kind: (int)kind,
                sourceActorId: sourceActorId,
                targetActorId: targetActorId,
                configId: configId);
        }

        /// <summary>
        /// 创建根节点（简化版）
        /// </summary>
        public long CreateRoot(EffectSourceKind kind, int configId, int sourceActorId, int targetActorId, int frame)
        {
            return CreateRoot(
                kind: (int)kind,
                sourceActorId: sourceActorId,
                targetActorId: targetActorId,
                configId: configId);
        }

        /// <summary>
        /// 确保根节点存在（兼容旧 API）
        /// </summary>
        public bool EnsureRoot(long contextId, EffectSourceKind kind, int configId, int sourceActorId, int targetActorId, int frame, object originSource, object originTarget)
        {
            var snapshot = TryGetSnapshot(contextId);
            if (snapshot.IsValid)
                return true;

            CreateRoot(kind, configId, sourceActorId, targetActorId, frame, originSource, originTarget);
            return true;
        }

        /// <summary>
        /// 创建子节点（兼容旧 API）
        /// </summary>
        public long CreateChild(long parentContextId, EffectSourceKind kind, int configId, int sourceActorId, int targetActorId, int frame, object originSource, object originTarget)
        {
            return CreateChild(
                parentContextId: parentContextId,
                kind: (int)kind,
                sourceActorId: sourceActorId,
                targetActorId: targetActorId,
                configId: configId);
        }

        /// <summary>
        /// 创建技能施法根节点
        /// </summary>
        public long CreateSkillCastRoot(
            int skillId,
            int level,
            long sourceActorId,
            long targetActorId,
            long originContextId)
        {
            return CreateRoot(
                kind: (int)EffectSourceKind.SkillCast,
                sourceActorId: sourceActorId,
                targetActorId: targetActorId,
                configId: skillId);
        }

        /// <summary>
        /// 创建效果子节点
        /// </summary>
        public long CreateEffectChild(
            long parentContextId,
            int effectId,
            long sourceActorId,
            long targetActorId)
        {
            return CreateChild(
                parentContextId: parentContextId,
                kind: (int)EffectSourceKind.Effect,
                sourceActorId: sourceActorId,
                targetActorId: targetActorId,
                configId: effectId);
        }

        /// <summary>
        /// 创建 Buff 子节点
        /// </summary>
        public long CreateBuffChild(
            long parentContextId,
            int buffId,
            long sourceActorId,
            long targetActorId)
        {
            return CreateChild(
                parentContextId: parentContextId,
                kind: (int)EffectSourceKind.Buff,
                sourceActorId: sourceActorId,
                targetActorId: targetActorId,
                configId: buffId);
        }

        /// <summary>
        /// 结束节点
        /// </summary>
        public bool EndNode(long contextId, EffectSourceEndReason reason)
        {
            return End(contextId, (int)reason);
        }

        /// <summary>
        /// 结束节点（带帧号）
        /// </summary>
        public bool End(long contextId, int frame, EffectSourceEndReason reason)
        {
            return End(contextId, (int)reason);
        }

        protected override MobaTraceMetadata CreateMetadata(
            long rootId, int kind,
            long sourceActorId, long targetActorId,
            long originId, string originDisplay,
            long targetId, string targetDisplay,
            int configId)
        {
            return new MobaTraceMetadata
            {
                SkillId = configId,
                SourceActorId = sourceActorId,
                TargetActorId = targetActorId,
                OriginContextId = originId,
            };
        }

        protected override long GetSourceActorId(MobaTraceMetadata metadata) => metadata.SourceActorId;
        protected override long GetTargetActorId(MobaTraceMetadata metadata) => metadata.TargetActorId;
        protected override long GetOriginSourceId(MobaTraceMetadata metadata) => metadata.OriginContextId;
    }
}
