using AbilityKit.Demo.Moba.Components;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Events.Buff
{
    /// <summary>
    /// Buff 事件参数
    /// </summary>
    public sealed class BuffEventArgs : AbilityKit.Demo.Moba.Services.IMobaRuntimeContextPayload
    {
        /// <summary>事件 ID</summary>
        public string EventId;

        /// <summary>来源 ActorId</summary>
        public int SourceActorId;

        /// <summary>目标 ActorId</summary>
        public int TargetActorId;

        /// <summary>BuffId</summary>
        public int BuffId;

        /// <summary>EffectId</summary>
        public int EffectId;

        /// <summary>阶段</summary>
        public string Stage;

        /// <summary>堆叠层数</summary>
        public int StackCount;

        /// <summary>持续时间（秒）</summary>
        public float DurationSeconds;

        /// <summary>移除原因</summary>
        public TraceLifecycleReason RemoveReason;

        /// <summary>trace/source 上下文 ID，用于溯源链和 owner 绑定</summary>
        public long SourceContextId;

        /// <summary>运行时上下文 ID，用于从上下文注册中心读取实时值或快照值</summary>
        public long RuntimeContextId;

        /// <summary>运行时上下文版本</summary>
        public long RuntimeContextVersion;
        public AbilityKit.Context.ContextEntityReference RuntimeContextIdentity;

        public bool TryGetRuntimeContext(out AbilityKit.Demo.Moba.Services.MobaRuntimeContextReference reference)
        {
            reference = AbilityKit.Demo.Moba.Services.MobaRuntimeContextReference.FromIdentityOrLegacy(
                RuntimeContextId, RuntimeContextVersion, RuntimeContextIdentity);
            return reference.IsValid;
        }

        /// <summary>Buff 运行时</summary>
        public BuffRuntime Runtime;
    }
}
