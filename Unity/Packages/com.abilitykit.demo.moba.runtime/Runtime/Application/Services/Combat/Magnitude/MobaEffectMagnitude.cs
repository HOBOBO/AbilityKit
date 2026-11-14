using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Modifiers;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Combat.Magnitude
{
    public enum MobaEffectSourceRole : byte
    {
        AttributionActor = 0,
        SkillCaster = 1,
        Owner = 2,
        RootOwner = 3,
        Target = 4,
        Self = 5,
        Child = 6,
    }

    public enum MobaEffectEvaluationPolicy : byte
    {
        Realtime = 0,
        Snapshot = 1,
    }

    public enum MobaEffectMagnitudeCombine : byte
    {
        Add = 0,
        Multiply = 1,
        Override = 2,
    }

    [Serializable]
    public readonly struct MobaEffectMagnitudeSpec
    {
        public MobaEffectMagnitudeSpec(
            in MagnitudeSource baseSource,
            in MagnitudeSource secondarySource,
            MobaEffectMagnitudeCombine combine = MobaEffectMagnitudeCombine.Add,
            MobaEffectSourceRole sourceRole = MobaEffectSourceRole.AttributionActor,
            MobaEffectEvaluationPolicy evaluationPolicy = MobaEffectEvaluationPolicy.Realtime,
            BlackboardWriteTarget captureTarget = default,
            bool enabled = true,
            string baseSkillLevelParameter = null,
            string secondarySkillLevelParameter = null,
            string secondaryCoefficientSkillLevelParameter = null, bool useCurrentHeroLevel = false)
        {
            BaseSource = baseSource;
            SecondarySource = secondarySource;
            Combine = combine;
            SourceRole = sourceRole;
            EvaluationPolicy = evaluationPolicy;
            CaptureTarget = captureTarget;
            Enabled = enabled;
            BaseSkillLevelParameter = baseSkillLevelParameter;
            SecondarySkillLevelParameter = secondarySkillLevelParameter;
            SecondaryCoefficientSkillLevelParameter = secondaryCoefficientSkillLevelParameter;
            UseCurrentHeroLevel = useCurrentHeroLevel;
        }

        public MagnitudeSource BaseSource { get; }
        public MagnitudeSource SecondarySource { get; }
        public MobaEffectMagnitudeCombine Combine { get; }
        public MobaEffectSourceRole SourceRole { get; }
        public MobaEffectEvaluationPolicy EvaluationPolicy { get; }
        public BlackboardWriteTarget CaptureTarget { get; }
        public bool Enabled { get; }
        public bool UseCurrentHeroLevel { get; }
        public string BaseSkillLevelParameter { get; }
        public string SecondarySkillLevelParameter { get; }
        public string SecondaryCoefficientSkillLevelParameter { get; }

        public static MobaEffectMagnitudeSpec Fixed(float value, MobaEffectEvaluationPolicy policy = MobaEffectEvaluationPolicy.Realtime)
        {
            var source = MagnitudeSource.Fixed(value);
            return new MobaEffectMagnitudeSpec(in source, default, evaluationPolicy: policy);
        }

        public static MobaEffectMagnitudeSpec FixedPlusAttribute(
            float value,
            BattleAttributeType attribute,
            float coefficient,
            MobaEffectSourceRole role,
            MobaEffectEvaluationPolicy policy = MobaEffectEvaluationPolicy.Realtime)
        {
            var baseSource = MagnitudeSource.Fixed(value);
            var bonusSource = MagnitudeSource.Attribute(ModifierKey.FromPacked((uint)attribute), coefficient);
            return new MobaEffectMagnitudeSpec(in baseSource, in bonusSource, MobaEffectMagnitudeCombine.Add, role, policy);
        }
    }

    public readonly struct MobaEffectMagnitudeCapture
    {
        public MobaEffectMagnitudeCapture(float value, int sourceActorId)
        {
            Value = value;
            SourceActorId = sourceActorId;
            IsValid = true;
        }

        public float Value { get; }
        public int SourceActorId { get; }
        public bool IsValid { get; }
    }

    public static class MobaEffectMagnitudeResolver
    {
        public static bool TryEvaluate(
            in MobaEffectMagnitudeSpec spec,
            in MobaCombatExecutionContext executionContext,
            in ExecCtx<IWorldResolver> execCtx,
            int attributionActorId,
            int selfActorId,
            int targetActorId,
            in MobaEffectMagnitudeCapture capture,
            out float value,
            out MobaEffectMagnitudeCapture nextCapture,
            out string failure)
        {
            value = 0f;
            nextCapture = capture;
            failure = null;
            if (!spec.Enabled)
            {
                failure = "magnitude spec is disabled";
                return false;
            }

            if (spec.EvaluationPolicy == MobaEffectEvaluationPolicy.Snapshot && capture.IsValid)
            {
                value = capture.Value;
                return true;
            }

            var captureTarget = spec.CaptureTarget;
            if (spec.EvaluationPolicy == MobaEffectEvaluationPolicy.Snapshot &&
                IsValidCaptureTarget(in captureTarget) &&
                TryResolveSnapshotBoard(in execCtx, in captureTarget, targetActorId, out var snapshotBoard) &&
                snapshotBoard.IsSnapshotCaptured(captureTarget.KeyId) &&
                snapshotBoard.TryGetDouble(captureTarget.KeyId, out var capturedValue))
            {
                value = (float)capturedValue;
                nextCapture = new MobaEffectMagnitudeCapture(value, 0);
                return true;
            }

            var requiresActor = RequiresAttributeActor(in spec);
            var hasSourceActor = TryResolveSourceActorId(
                in spec, in executionContext, in execCtx,
                attributionActorId, selfActorId, targetActorId,
                out var sourceActorId, out failure);
            if (!hasSourceActor && requiresActor) return false;

            global::ActorEntity sourceActor = null;
            if (sourceActorId > 0 && execCtx.Context != null &&
                execCtx.Context.TryResolve<MobaActorLookupService>(out var actors) && actors != null)
                actors.TryGetActorEntity(sourceActorId, out sourceActor);
            if (requiresActor && (sourceActor == null || !sourceActor.hasAttributeGroup))
            {
                failure = $"magnitude source actor {sourceActorId} is unavailable or has no attributes";
                return false;
            }

            var modifierContext = new MobaMagnitudeModifierContext(sourceActor, in executionContext, in execCtx, spec.UseCurrentHeroLevel);
            if (!TryCalculateSource(spec.BaseSource, spec.BaseSkillLevelParameter, in executionContext, in execCtx, modifierContext, out var primary, out failure) ||
                !TryCalculateSource(spec.SecondarySource, spec.SecondarySkillLevelParameter, in executionContext, in execCtx, modifierContext, out var secondary, out failure))
                return false;
            if (!string.IsNullOrWhiteSpace(spec.SecondaryCoefficientSkillLevelParameter))
            {
                if (spec.SecondarySource.Type != MagnitudeSourceType.Attribute)
                {
                    failure = "skill level coefficient parameter requires an Attribute secondary magnitude source";
                    return false;
                }
                if (!TryReadSkillLevelParameter(spec.SecondaryCoefficientSkillLevelParameter, in executionContext, in execCtx, out var coefficient, out failure))
                    return false;
                secondary *= coefficient;
            }
            value = spec.Combine switch
            {
                MobaEffectMagnitudeCombine.Multiply => primary * secondary,
                MobaEffectMagnitudeCombine.Override => secondary,
                _ => primary + secondary,
            };

            if (spec.EvaluationPolicy == MobaEffectEvaluationPolicy.Snapshot)
            {
                nextCapture = new MobaEffectMagnitudeCapture(value, sourceActorId);
                if (IsValidCaptureTarget(in captureTarget))
                {
                    if (!TryResolveSnapshotBoard(
                            in execCtx, in captureTarget, targetActorId, out var snapshotOutputBoard))
                    {
                        failure = "magnitude snapshot capture target must use a skill_runtime Blackboard";
                        return false;
                    }
                    try
                    {
                        switch (captureTarget.KeyType)
                        {
                            case BlackboardKeyType.Int: snapshotOutputBoard.SetInt(captureTarget.KeyId, (int)Math.Round(value)); break;
                            case BlackboardKeyType.Float: snapshotOutputBoard.SetFloat(captureTarget.KeyId, value); break;
                            case BlackboardKeyType.Double: snapshotOutputBoard.SetDouble(captureTarget.KeyId, value); break;
                            default:
                                failure = $"magnitude snapshot capture target must be numeric, actual={captureTarget.KeyType}";
                                return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        failure = ex.Message;
                        return false;
                    }
                    snapshotOutputBoard.MarkSnapshotCaptured(captureTarget.KeyId);
                }
            }
            return true;
        }

        private static bool RequiresAttributeActor(in MobaEffectMagnitudeSpec spec)
        {
            return RequiresAttributeActor(spec.BaseSource.Type) || RequiresAttributeActor(spec.SecondarySource.Type);
        }

        private static bool RequiresAttributeActor(MagnitudeSourceType type)
        {
            return type == MagnitudeSourceType.Attribute || type == MagnitudeSourceType.Pipeline;
        }

        private static bool TryCalculateSource(
            in MagnitudeSource source,
            string skillLevelParameter,
            in MobaCombatExecutionContext executionContext,
            in ExecCtx<IWorldResolver> execCtx,
            IModifierContext modifierContext,
            out float value,
            out string failure)
        {
            value = 0f;
            failure = null;
            if (source.Type == MagnitudeSourceType.HeroLevelSkillParameter)
            {
                if (string.IsNullOrWhiteSpace(skillLevelParameter))
                {
                    failure = "hero level skill growth column name is empty";
                    return false;
                }
                var growthHandle = executionContext.SkillRuntimeHandle;
                if (!growthHandle.IsValid || execCtx.Context == null ||
                    !execCtx.Context.TryResolve<MobaSkillCastRuntimeService>(out var growthRuntimes) || growthRuntimes == null ||
                    !growthRuntimes.TryGet(in growthHandle, out var growthRuntime) || growthRuntime == null ||
                    !execCtx.Context.TryResolve<MobaConfigDatabase>(out var configs) || configs == null ||
                    !configs.TryGetHeroLevelSkillGrowthParameter(growthRuntime.SkillId, growthRuntime.ChampionLevel, skillLevelParameter, out value))
                {
                    failure = $"hero level skill growth column '{skillLevelParameter}' is unavailable for the current skill runtime";
                    return false;
                }
                value *= source.Coefficient;
                return true;
            }
            if (source.Type != MagnitudeSourceType.SkillLevelParameter)
            {
                value = source.Calculate(modifierContext.Level, modifierContext);
                return true;
            }

            if (!TryReadSkillLevelParameter(skillLevelParameter, in executionContext, in execCtx, out value, out failure))
                return false;
            value *= source.Coefficient;
            return true;
        }

        private static bool TryReadSkillLevelParameter(
            string parameter,
            in MobaCombatExecutionContext executionContext,
            in ExecCtx<IWorldResolver> execCtx,
            out float value,
            out string failure)
        {
            value = 0f;
            failure = null;
            if (string.IsNullOrWhiteSpace(parameter))
            {
                failure = "skill level parameter name is empty";
                return false;
            }
            var handle = executionContext.SkillRuntimeHandle;
            if (!handle.IsValid || execCtx.Context == null ||
                !execCtx.Context.TryResolve<MobaSkillCastRuntimeService>(out var runtimes) || runtimes == null ||
                !runtimes.TryGetLevelParameter(in handle, parameter, out value))
            {
                failure = $"skill level parameter '{parameter}' is unavailable for the current skill runtime";
                return false;
            }
            return true;
        }

        private static bool IsValidCaptureTarget(in BlackboardWriteTarget target)
        {
            return target.BoardId != 0 && target.KeyId != 0 &&
                   (target.KeyType == BlackboardKeyType.Int ||
                    target.KeyType == BlackboardKeyType.Float ||
                    target.KeyType == BlackboardKeyType.Double);
        }

        private static bool TryResolveSnapshotBoard(
            in ExecCtx<IWorldResolver> execCtx,
            in BlackboardWriteTarget target,
            int targetActorId,
            out MobaSkillRuntimeBlackboardAdapter board)
        {
            board = null;
            if (execCtx.Blackboards == null ||
                !execCtx.Blackboards.TryResolve(target.BoardId, out var raw) ||
                !(raw is MobaSkillRuntimeBlackboardAdapter resolved)) return false;
            board = target.BoardId == MobaSkillRuntimeTriggerBoards.Target && targetActorId > 0
                ? resolved.ForScopeOwner(targetActorId)
                : resolved;
            return true;
        }

        private static bool TryResolveSourceActorId(
            in MobaEffectMagnitudeSpec spec,
            in MobaCombatExecutionContext executionContext,
            in ExecCtx<IWorldResolver> execCtx,
            int attributionActorId,
            int selfActorId,
            int targetActorId,
            out int actorId,
            out string failure)
        {
            actorId = 0;
            failure = null;
            switch (spec.SourceRole)
            {
                case MobaEffectSourceRole.Target:
                    actorId = targetActorId;
                    break;
                case MobaEffectSourceRole.SkillCaster:
                    var runtimeHandle = executionContext.SkillRuntimeHandle;
                    if (runtimeHandle.IsValid && execCtx.Context != null &&
                        execCtx.Context.TryResolve<MobaSkillCastRuntimeService>(out var runtimes) && runtimes != null &&
                        runtimes.TryGet(in runtimeHandle, out var runtime) && runtime != null)
                        actorId = runtime.CasterActorId;
                    break;
                case MobaEffectSourceRole.Owner:
                    actorId = ResolveOwnerActorId(execCtx.Context, selfActorId, root: false);
                    break;
                case MobaEffectSourceRole.RootOwner:
                    actorId = ResolveOwnerActorId(execCtx.Context, selfActorId, root: true);
                    break;
                case MobaEffectSourceRole.Self:
                case MobaEffectSourceRole.Child:
                    actorId = selfActorId;
                    break;
                default:
                    actorId = attributionActorId;
                    break;
            }

            if (actorId > 0) return true;
            failure = $"cannot resolve actor for source role {spec.SourceRole}";
            return false;
        }

        private static int ResolveOwnerActorId(IWorldResolver services, int selfActorId, bool root)
        {
            if (selfActorId <= 0 || services == null ||
                !services.TryResolve<MobaActorLookupService>(out var actors) || actors == null ||
                !actors.TryGetActorEntity(selfActorId, out var actor) || actor == null)
                return selfActorId;
            if (!actor.hasOwnerLink || actor.ownerLink == null) return selfActorId;
            if (root && actor.ownerLink.RootOwnerActorId > 0) return actor.ownerLink.RootOwnerActorId;
            return actor.ownerLink.OwnerActorId > 0 ? actor.ownerLink.OwnerActorId : selfActorId;
        }

        private sealed class MobaMagnitudeModifierContext : IModifierContext
        {
            private readonly global::ActorEntity _actor;
            private readonly bool _useCurrentHeroLevel;
            private readonly MobaCombatExecutionContext _executionContext;
            private readonly ExecCtx<IWorldResolver> _execCtx;

            public MobaMagnitudeModifierContext(global::ActorEntity actor, in MobaCombatExecutionContext executionContext, in ExecCtx<IWorldResolver> execCtx, bool useCurrentHeroLevel = false)
            {
                _actor = actor;
                _useCurrentHeroLevel = useCurrentHeroLevel;
                _executionContext = executionContext;
                _execCtx = execCtx;
            }

            public float Level
            {
                get
                {
                    if (_useCurrentHeroLevel) return _actor != null && _actor.hasChampionLevel ? Math.Max(1, _actor.championLevel.Value) : 1;
                    var runtimeHandle = _executionContext.SkillRuntimeHandle;
                    if (runtimeHandle.IsValid && _execCtx.Context != null &&
                        _execCtx.Context.TryResolve<MobaSkillCastRuntimeService>(out var runtimes) &&
                        runtimes != null && runtimes.TryGet(in runtimeHandle, out var runtime) && runtime != null)
                        return Math.Max(1, runtime.SkillLevel);
                    return 1f;
                }
            }

            public float CurrentTime => TryGetFrameTime(out var time) ? time.Time : 0f;
            public float DeltaTime => TryGetFrameTime(out var time) ? time.DeltaTime : 0f;
            public float ElapsedTime
            {
                get
                {
                    if (!TryGetFrameTime(out var time) || _executionContext.Frame <= 0 ||
                        time.Frame.Value <= _executionContext.Frame) return 0f;
                    return Math.Max(0f,
                        time.FrameToTime(time.Frame) - time.FrameToTime(new FrameIndex(_executionContext.Frame)));
                }
            }
            public ModifierMetadata Metadata => ModifierMetadata.Empty;

            public float GetAttribute(ModifierKey key)
            {
                if (_actor == null || !_actor.hasAttributeGroup) return 0f;
                var type = (BattleAttributeType)key.Packed;
                return _actor.GetMobaAttrs().Get(type);
            }

            private bool TryGetFrameTime(out IFrameTime time)
            {
                time = null;
                return _execCtx.Context != null && _execCtx.Context.TryResolve(out time) && time != null;
            }

            public T GetData<T>(string key) where T : class => TryGetData<T>(key, out var value) ? value : null;
            public bool TryGetData<T>(string key, out T value) where T : class { value = null; return false; }
            public float GetFloat(string key) => TryGetFloat(key, out var value) ? value : 0f;

            public bool TryGetFloat(string key, out float value)
            {
                value = default;
                if (string.IsNullOrWhiteSpace(key) || _execCtx.NumericDomains == null) return false;
                var separator = key.IndexOf(':');
                if (separator <= 0 || separator >= key.Length - 1) return false;
                var domainId = key.Substring(0, separator);
                if (!_execCtx.NumericDomains.TryGetDomain(domainId, out var domain) || domain == null ||
                    !domain.TryGet(in _execCtx, key.Substring(separator + 1), out var raw)) return false;
                value = (float)raw;
                return true;
            }

            public int GetInt(string key) => TryGetInt(key, out var value) ? value : 0;
            public bool TryGetInt(string key, out int value)
            {
                if (TryGetFloat(key, out var raw)) { value = (int)raw; return true; }
                value = default;
                return false;
            }
        }
    }
}
