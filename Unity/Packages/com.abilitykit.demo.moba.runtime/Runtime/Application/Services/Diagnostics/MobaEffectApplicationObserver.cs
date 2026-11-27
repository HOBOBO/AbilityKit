using System.Runtime.CompilerServices;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services.Observability;
using AbilityKit.ECS;
using AbilityKit.Effect;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 全量诊断开启时按实例对象记录应用证据。弱键避免延长已移除效果的生命周期。
    /// </summary>
    public sealed class MobaEffectApplicationObserver : IEffectApplicationObserver
    {
        private readonly ConditionalWeakTable<EffectInstance, Evidence> _applications =
            new ConditionalWeakTable<EffectInstance, Evidence>();

        private readonly IMobaBattleDiagnosticCapturePolicy _policy;
        private readonly IMobaRuntimeObjectKeyResolver _keys;
        private readonly MobaExecutionContextRegistry _contexts;

        /// <summary>
        /// 使用同一战斗作用域中的诊断开关、实体目录和执行上下文。
        /// </summary>
        public MobaEffectApplicationObserver(IMobaBattleDiagnosticCapturePolicy policy,
            IMobaRuntimeObjectKeyResolver keys, MobaExecutionContextRegistry contexts)
        {
            _policy = policy;
            _keys = keys;
            _contexts = contexts;
        }

        void IEffectApplicationObserver.OnApplication(EffectInstance instance, in EffectExecutionContext context)
        {
            if (_policy?.CaptureMode != BattleDiagnosticCaptureMode.Full) return;
            var frame = context.Time.Frame.Value;
            if (!BattleDiagnosticFrames.IsValid(frame)) return;
            var sourceId = context.Source is IUnitFacade unit ? unit.Id.ActorId :
                context.Source is ActorEntity entity && entity.hasActorId ? entity.actorId.Value : 0;
            var generation = sourceId > 0 && _keys != null &&
                _keys.TryResolve(MobaRuntimeObjectKind.Actor, sourceId, frame, out var key) ? key.Generation : 0;
            var source = BattleDiagnosticRuntimeObjectReference.Create(BattleDiagnosticRuntimeObjectKind.Actor,
                sourceId, generation);
            var rootId = context.SourceContextId > 0 && _contexts != null &&
                _contexts.TryGet(context.SourceContextId, out var node) ? node.RootContextId : 0L;
            _applications.Add(instance, new Evidence(new BattleDiagnosticEffectApplication(
                frame, source, context.SourceContextId, rootId)));
        }

        internal BattleDiagnosticEffectApplication Read(EffectInstance instance, int frame) =>
            _applications.TryGetValue(instance, out var evidence) && evidence.Application.Frame <= frame
                ? evidence.Application : default;

        private sealed class Evidence
        {
            public Evidence(BattleDiagnosticEffectApplication application) => Application = application;
            public BattleDiagnosticEffectApplication Application { get; }
        }
    }
}
