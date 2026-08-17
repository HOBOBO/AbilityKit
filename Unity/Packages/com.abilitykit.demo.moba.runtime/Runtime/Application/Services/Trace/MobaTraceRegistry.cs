using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaTraceRegistry))]
    public sealed class MobaTraceRegistry : TraceTreeRegistry<MobaTraceMetadata>, IService
    {
        private readonly IMobaTraceEndpointResolver _endpoints;
        private readonly IMobaTraceWriter _writer;
        private readonly IMobaTraceLifecycle _lifecycle;
        private readonly IMobaTraceQuery _query;

        [WorldInject(required: false)] private IMobaBattleDiagnosticEventSink _eventCollector = null;
        [WorldInject(required: false)] private IFrameTime _frameTime = null;

        public MobaTraceRegistry()
            : base(new DictionaryTraceMetadataStore<MobaTraceMetadata>())
        {
            _endpoints = new MobaTraceEndpointResolver();
            _writer = new MobaTraceWriter(this);
            _lifecycle = new MobaTraceLifecycle(this);
            _query = new MobaTraceQuery(this);
            RegistryEvent += OnRegistryEvent;
        }

        protected override int Frame => _frameTime != null ? _frameTime.Frame.Value : 0;

        public TraceEndpoint ResolveEndpoint(MobaTraceKind kind, int configId)
        {
            return _endpoints.ResolveEndpoint(kind, configId);
        }

        public long CreateRootContext(
            MobaTraceKind kind,
            int configId,
            long sourceActorId = 0,
            long targetActorId = 0,
            object originSource = null,
            object originTarget = null)
        {
            return _writer.CreateRootContext(kind, configId, sourceActorId, targetActorId, originSource, originTarget);
        }

        public long CreateChildContext(
            long parentContextId,
            MobaTraceKind kind,
            int configId,
            long sourceActorId = 0,
            long targetActorId = 0,
            object originSource = null,
            object originTarget = null)
        {
            return _writer.CreateChildContext(parentContextId, kind, configId, sourceActorId, targetActorId, originSource, originTarget);
        }

        public bool EndContext(long contextId, TraceLifecycleReason reason)
        {
            return _lifecycle.EndContext(contextId, reason);
        }

        public bool EndContext(long contextId, int reason = 0)
        {
            return _lifecycle.EndContext(contextId, reason);
        }

        public TraceRootScope CreateEffectRoot(
            int effectConfigId,
            int triggerPlanId,
            int sourceActorId,
            int targetActorId,
            EffectContextKind contextKind)
        {
            return _writer.CreateEffectRoot(effectConfigId, triggerPlanId, sourceActorId, targetActorId, contextKind);
        }

        public TraceTreeScope CreateActionChild(
            long parentRootId,
            int actionId,
            int sourceActorId,
            int targetActorId)
        {
            return _writer.CreateActionChild(parentRootId, actionId, sourceActorId, targetActorId);
        }

        public List<TraceSnapshot<MobaTraceMetadata>> GetChain(long rootId)
        {
            return _query.GetChain(rootId);
        }

        public bool ValidateChain(long rootId)
        {
            return _query.ValidateChain(rootId);
        }

        public MobaTraceValidationResult ValidateChainDetailed(long rootId)
        {
            return _query.ValidateChainDetailed(rootId);
        }

        public bool TrySetSkillPhaseLocation(
            long contextId,
            int skillId,
            int castFlowId,
            string phaseId)
        {
            if (contextId == 0 || !TryGetNodeSnapshot(contextId, out var snapshot) ||
                !(snapshot.Metadata is MobaTraceMetadata metadata))
            {
                return false;
            }

            metadata.SkillId = skillId;
            metadata.CastFlowId = castFlowId;
            metadata.PhaseId = phaseId ?? string.Empty;
            return true;
        }

        public override string GetKindName(int kind)
        {
            return ((MobaTraceKind)kind).ToString();
        }

        protected override MobaTraceMetadata CreateMetadata(
            long rootId,
            int kind,
            long sourceActorId,
            long targetActorId,
            long originId,
            string originDisplay,
            long targetId,
            string targetDisplay,
            int configId)
        {
            return new MobaTraceMetadata
            {
                RootId = rootId,
                ParentId = 0,
                TraceKind = (MobaTraceKind)kind,
                ConfigId = configId,
                SourceActorId = sourceActorId,
                TargetActorId = targetActorId,
                SourceId = sourceActorId,
                TargetId = targetActorId,
                OriginSourceId = originId,
                OriginSource = originDisplay,
                OriginTargetId = targetId,
                OriginTarget = targetDisplay
            };
        }

        protected override long GetSourceActorId(MobaTraceMetadata metadata) => metadata.SourceActorId;
        protected override long GetTargetActorId(MobaTraceMetadata metadata) => metadata.TargetActorId;
        protected override long GetOriginSourceId(MobaTraceMetadata metadata) => metadata.OriginSourceId;
        protected override string GetOriginSourceDisplay(MobaTraceMetadata metadata) => metadata.OriginSource;
        protected override long GetOriginTargetId(MobaTraceMetadata metadata) => metadata.OriginTargetId;
        protected override string GetOriginTargetDisplay(MobaTraceMetadata metadata) => metadata.OriginTarget;

        // ===== TraceNode 诊断 Producer =====

        internal void AttachDiagnosticCollector(IMobaBattleDiagnosticEventSink collector)
        {
            _eventCollector = collector;
        }

        internal void AttachFrameTime(IFrameTime frameTime)
        {
            _frameTime = frameTime;
        }

        private void OnRegistryEvent(TraceRegistryEvent evt)
        {
            if (_eventCollector == null) return;
            if (evt.Kind != TraceRegistryEventKind.RootCreated
                && evt.Kind != TraceRegistryEventKind.ChildCreated
                && evt.Kind != TraceRegistryEventKind.NodeEnded)
                return;

            try
            {
                if (evt.Kind == TraceRegistryEventKind.NodeEnded)
                {
                    if (!TryResolveTraceNodeFields(evt.ContextId, out var kind, out var configId, out var sourceActorId, out var targetActorId))
                        return;
                    var draft = CreateTraceNodeEndedDraft(
                        evt.ContextId,
                        evt.RootId,
                        evt.ParentId,
                        kind,
                        configId,
                        sourceActorId,
                        targetActorId,
                        evt.Reason);
                    _eventCollector.TryCollect(in draft);
                }
                else
                {
                    if (!TryResolveTraceNodeFields(evt.ContextId, out var kind, out var configId, out var sourceActorId, out var targetActorId))
                        return;
                    var draft = CreateTraceNodeStartedDraft(
                        evt.ContextId,
                        evt.RootId,
                        evt.ParentId,
                        kind,
                        configId,
                        sourceActorId,
                        targetActorId);
                    _eventCollector.TryCollect(in draft);
                }
            }
            catch (Exception) { }
        }

        private bool TryResolveTraceNodeFields(
            long contextId,
            out int kind,
            out int configId,
            out long sourceActorId,
            out long targetActorId)
        {
            kind = 0;
            configId = 0;
            sourceActorId = 0;
            targetActorId = 0;
            if (!TryGetNodeSnapshot(contextId, out var snapshot))
                return false;
            kind = snapshot.Kind;
            if (snapshot.Metadata is MobaTraceMetadata metadata)
            {
                configId = metadata.ConfigId;
                sourceActorId = metadata.SourceActorId;
                targetActorId = metadata.TargetActorId;
            }
            return true;
        }

        internal static MobaBattleDiagnosticEventDraft CreateTraceNodeStartedDraft(
            long contextId,
            long rootContextId,
            long parentContextId,
            int traceKind,
            int configId,
            long sourceActorId,
            long targetActorId)
        {
            var resolvedRoot = rootContextId != 0L ? rootContextId : contextId;
            var summary = $"traceKind={traceKind}, configId={configId}, contextId={contextId}, parentContextId={parentContextId}";

            return new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.TraceNodeStarted,
                BattleDiagnosticEventChannel.Skill,
                BattleDiagnosticEventOutcome.Succeeded,
                sourceActorId,
                targetActorId,
                configId,
                resolvedRoot,
                contextId,
                summary: summary);
        }

        internal static MobaBattleDiagnosticEventDraft CreateTraceNodeEndedDraft(
            long contextId,
            long rootContextId,
            long parentContextId,
            int traceKind,
            int configId,
            long sourceActorId,
            long targetActorId,
            int reason)
        {
            var resolvedRoot = rootContextId != 0L ? rootContextId : contextId;
            var summary = $"traceKind={traceKind}, configId={configId}, contextId={contextId}, parentContextId={parentContextId}, reason={reason}";

            return new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.TraceNodeEnded,
                BattleDiagnosticEventChannel.Skill,
                BattleDiagnosticEventOutcome.Succeeded,
                sourceActorId,
                targetActorId,
                configId,
                resolvedRoot,
                contextId,
                summary: summary);
        }
    }

    /// <summary>
    /// 运行时类型名称常量。
    /// 用于 trace、诊断和上下文分类中的统一字符串标识。
    /// </summary>
    public static class MobaRuntimeKindNames
    {
        public const string Actor = "actor";
        public const string Skill = "skill";
        public const string SkillPipeline = "skill.pipeline";
        public const string Effect = "effect";
        public const string Action = "action";
        public const string Buff = "buff";
        public const string Projectile = "projectile";
        public const string Area = "area";
        public const string Summon = "summon";
        public const string Presentation = "presentation";
        public const string DamageAttack = "damage.attack";
        public const string DamageCalc = "damage.calc";
        public const string DamageResult = "damage.result";
        public const string ProjectileHit = "projectile.hit";
        public const string AreaEnter = "area.enter";
        public const string Unit = "unit";
        public const string UnitDeath = "unit.death";
    }
}
