using System;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 效果执行的正式链路输入。
    /// 用于描述一次新的效果执行如何挂接到既有的溯源/上下文链路上。
    /// </summary>
    public readonly struct MobaEffectLineageInput
    {
        public MobaEffectLineageInput(
            EffectContextKind contextKind,
            MobaTraceKind originKind,
            int sourceActorId,
            int targetActorId,
            long parentContextId,
            long rootContextId,
            long ownerContextId,
            int originConfigId)
        {
            ContextKind = contextKind;
            OriginKind = originKind;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            ParentContextId = parentContextId;
            RootContextId = rootContextId;
            OwnerContextId = ownerContextId;
            OriginConfigId = originConfigId;
        }

        public EffectContextKind ContextKind { get; }
        public MobaTraceKind OriginKind { get; }
        public int SourceActorId { get; }
        public int TargetActorId { get; }
        /// <summary>
        /// 效果执行应挂接到的父溯源节点。为 0 时，表示执行可能创建新的根节点。
        /// </summary>
        public long ParentContextId { get; }
        /// <summary>
        /// 该链路已知的根溯源节点。为 0 时，ParentContextId 作为有效根节点使用。
        /// </summary>
        public long RootContextId { get; }
        /// <summary>该链路传递的所有权上下文标识。</summary>
        public long OwnerContextId { get; }
        /// <summary>所有权上下文标识的兼容别名。</summary>
        public long OwnerKey => OwnerContextId;
        public int OriginConfigId { get; }

        public long EffectiveRootContextId => RootContextId != 0 ? RootContextId : ParentContextId;
        public bool HasExecutionSource => SourceActorId > 0 && ParentContextId != 0;
        /// <summary>仅表示当前输入可创建新的效果根节点，不表示存在可借用的父节点。</summary>
        public bool CanCreateRootExecution => SourceActorId > 0 && ParentContextId == 0;
        public bool IsValid => ContextKind != EffectContextKind.Unknown
                               || SourceActorId != 0
                               || TargetActorId != 0
                               || ParentContextId != 0
                               || RootContextId != 0
                               || OwnerContextId != 0
                               || OriginConfigId != 0;

        public MobaEffectTraceInput ToTraceInput()
        {
            return new MobaEffectTraceInput(
                ContextKind,
                OriginKind,
                SourceActorId,
                TargetActorId,
                ParentContextId,
                RootContextId,
                OwnerContextId,
                OriginConfigId);
        }

        public static MobaEffectLineageInput FromInvocation(IMobaTriggerInvocationContext invocation)
        {
            if (invocation == null) throw new ArgumentNullException(nameof(invocation));
            return new MobaEffectLineageInput(
                invocation.Kind,
                MobaTraceKind.EffectExecution,
                invocation.SourceActorId,
                invocation.TargetActorId,
                invocation.SourceContextId,
                invocation.SourceContextId,
                0,
                0);
        }
    }
}
