using System;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba
{
    /// <summary>
    /// 鏁堟灉婧簮绉嶇被鏋氫妇
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
    /// 鏁堟灉婧簮缁撴潫鍘熷洜鏋氫妇
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

namespace AbilityKit.Demo.Moba.EffectSource
{
    using AbilityKit.Demo.Moba;
    using AbilityKit.Ability.World.Services;

    /// <summary>
    /// Moba 婧簮鍏冩暟鎹?    /// </summary>
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
    /// Moba 婧簮娉ㄥ唽琛?    /// 鍩轰簬 AbilityKit.Trace.TraceTreeRegistry锛屾彁渚涗笌鏃?EffectSourceRegistry 鍏煎鐨?API
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
        /// 鍒涘缓鏍硅妭鐐癸紙鍏煎鏃?API锛?        /// </summary>
        public long CreateRoot(EffectSourceKind kind, int configId, int sourceActorId, int targetActorId, int frame, object originSource, object originTarget)
        {
            return CreateRoot(
                kind: (int)kind,
                sourceActorId: sourceActorId,
                targetActorId: targetActorId,
                configId: configId);
        }

        /// <summary>
        /// 鍒涘缓鏍硅妭鐐癸紙绠€鍖栫増锛?        /// </summary>
        public long CreateRoot(EffectSourceKind kind, int configId, int sourceActorId, int targetActorId, int frame)
        {
            return CreateRoot(
                kind: (int)kind,
                sourceActorId: sourceActorId,
                targetActorId: targetActorId,
                configId: configId);
        }

        /// <summary>
        /// 纭繚鏍硅妭鐐瑰瓨鍦紙鍏煎鏃?API锛?        /// </summary>
        public bool EnsureRoot(long contextId, EffectSourceKind kind, int configId, int sourceActorId, int targetActorId, int frame, object originSource, object originTarget)
        {
            var snapshot = TryGetSnapshot(contextId);
            if (snapshot.IsValid)
                return true;

            CreateRoot(kind, configId, sourceActorId, targetActorId, frame, originSource, originTarget);
            return true;
        }

        /// <summary>
        /// 鍒涘缓瀛愯妭鐐癸紙鍏煎鏃?API锛?        /// </summary>
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
        /// 鍒涘缓鎶€鑳芥柦娉曟牴鑺傜偣
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
        /// 鍒涘缓鏁堟灉瀛愯妭鐐?        /// </summary>
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
        /// 鍒涘缓 Buff 瀛愯妭鐐?        /// </summary>
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
        /// 缁撴潫鑺傜偣
        /// </summary>
        public bool EndNode(long contextId, EffectSourceEndReason reason)
        {
            return End(contextId, (int)reason);
        }

        /// <summary>
        /// 缁撴潫鑺傜偣锛堝甫甯у彿锛?        /// </summary>
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
