using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Impl.Moba.Conponents;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.Share.Impl.Moba;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Buffs
{
    [WorldSystem(order: MobaSystemOrder.BuffsApply, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaBuffApplySystem : WorldSystemBase
    {
        private MobaConfigDatabase _configs;
        private IUnitResolver _units;
        private IEventBus _eventBus;
        private ITriggerActionRunner _actionRunner;
        private MobaOngoingEffectService _ongoing;
        private EffectSourceRegistry _effectSource;
        private IFrameTime _frameTime;
        private MobaEffectExecutionService _effectExec;

        private global::Entitas.IGroup<global::ActorEntity> _group;

        public MobaBuffApplySystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _configs);
            Services.TryResolve(out _units);
            Services.TryResolve(out _eventBus);
            Services.TryResolve(out _actionRunner);
            Services.TryResolve(out _ongoing);
            Services.TryResolve(out _effectSource);
            Services.TryResolve(out _frameTime);
            Services.TryResolve(out _effectExec);
            _group = Contexts.Actor().GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId, ActorComponentsLookup.ApplyBuffRequest));
        }

        protected override void OnExecute()
        {
            if (_configs == null || _units == null) return;

            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0) return;

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null || !e.hasActorId || !e.hasApplyBuffRequest) continue;

                var req = e.applyBuffRequest;
                e.RemoveApplyBuffRequest();

                if (req.BuffId <= 0) continue;

                if (!_units.TryResolve(new EcsEntityId(e.actorId.Value), out var unit) || unit == null) continue;

                if (!_configs.TryGetBuff(req.BuffId, out var buff) || buff == null) continue;

                if (!e.hasBuffs)
                {
                    e.AddBuffs(new List<BuffRuntime>());
                }

                var list = e.buffs.Active;
                if (list == null)
                {
                    list = new List<BuffRuntime>();
                    e.ReplaceBuffs(list);
                }

                var duration = req.DurationOverrideMs > 0 ? req.DurationOverrideMs : buff.DurationMs;

                var durationSeconds = duration > 0 ? duration / 1000f : 0f;

                var targetActorId = e.actorId.Value;
                var existingIndex = FindExistingBuffIndex(list, buff.Id);
                if (existingIndex >= 0)
                {
                    var rt = list[existingIndex];
                    var applied = ApplyToExisting(rt, buff, req, durationSeconds, _effectSource, _actionRunner, GetFrame(), req.SourceId, targetActorId);
                    EnsureBuffContext(rt, buff.Id, req.SourceId, targetActorId, _effectSource, GetFrame());
                    TryStartOngoingEffectByBuff(buff, rt, req.SourceId, targetActorId, duration);
                    PublishBuffEvent(_eventBus, _effectSource, MobaBuffTriggering.Events.ApplyOrRefresh, buff, req.SourceId, targetActorId, durationSeconds, rt);
                    if (applied)
                    {
                        ResetInterval(rt, buff);
                        ExecuteStageEffects(buff.OnAddEffects, stage: "add", sourceActorId: req.SourceId, targetActorId: targetActorId, unit, GetFrame(), rt);
                    }
                }
                else
                {
                    var rt = CreateNewRuntime(buff, req, durationSeconds);
                    EnsureBuffContext(rt, buff.Id, req.SourceId, targetActorId, _effectSource, GetFrame());
                    list.Add(rt);
                    TryStartOngoingEffectByBuff(buff, rt, req.SourceId, targetActorId, duration);
                    PublishBuffEvent(_eventBus, _effectSource, MobaBuffTriggering.Events.ApplyOrRefresh, buff, req.SourceId, targetActorId, durationSeconds, rt);
                    ResetInterval(rt, buff);
                    ExecuteStageEffects(buff.OnAddEffects, stage: "add", sourceActorId: req.SourceId, targetActorId: targetActorId, unit, GetFrame(), rt);
                }
            }
        }

        private void TryStartOngoingEffectByBuff(BuffMO buff, BuffRuntime runtime, int sourceActorId, int targetActorId, int durationOverrideMs)
        {
            if (_ongoing == null) return;
            if (_actionRunner == null) return;
            if (buff == null || runtime == null) return;
            if (buff.OngoingEffectId <= 0) return;
            if (runtime.SourceContextId == 0) return;

            try
            {
                var running = _ongoing.Start(buff.OngoingEffectId, sourceActorId, targetActorId, ownerKey: runtime.SourceContextId);
                if (running != null)
                {
                    _actionRunner.Add(running, runtime.SourceContextId);
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[MobaBuffApplySystem] TryStartOngoingEffectByBuff failed (buffId={buff.Id}, ongoingEffectId={buff.OngoingEffectId})");
            }
        }

        private static void ResetInterval(BuffRuntime rt, BuffMO buff)
        {
            if (rt == null) return;
            if (buff == null) return;
            rt.IntervalRemainingSeconds = buff.IntervalMs > 0 ? buff.IntervalMs / 1000f : 0f;
        }

        private void ExecuteStageEffects(IReadOnlyList<int> effectIds, string stage, int sourceActorId, int targetActorId, IUnitFacade targetUnit, int frame, BuffRuntime runtime)
        {
            // 涓枃娉ㄩ噴锛欱uff 鐨勫闃舵鏁堟灉鎵ц鍏ュ彛銆?
            // stage: add/remove/interval 绛夛紝涓昏鐢ㄤ簬浜嬩欢 args 鏍囪瘑鏉ユ簮闃舵銆?
            if (_effectExec == null) return;
            if (effectIds == null || effectIds.Count == 0) return;

            for (int i = 0; i < effectIds.Count; i++)
            {
                var effectId = effectIds[i];
                if (effectId <= 0) continue;
                var ctx = BuildEffectContext(sourceActorId, targetActorId, targetUnit);
                _effectExec.Execute(effectId, ctx, EffectExecuteMode.InternalOnly);
                PublishBuffEffectEvent(_eventBus, _effectSource, MobaBuffTriggering.Events.ApplyOrRefresh, effectId, stage, sourceActorId, targetActorId, runtime);
            }
        }

        private static SkillPipelineContext BuildEffectContext(int sourceActorId, int targetActorId, IUnitFacade targetUnit)
        {
            // 涓枃娉ㄩ噴锛欱uff 鎵ц effect 鏃跺鐢?SkillPipelineContext 浣滀负閫氱敤鐨?effect 鎵ц涓婁笅鏂囥€?
            // 杩欓噷 skillId/slot 缃?0锛孉im 缃浂鍚戦噺銆?
            var ctx = new SkillPipelineContext();
            var req = new SkillCastRequest(
                skillId: 0,
                skillSlot: 0,
                casterActorId: sourceActorId,
                targetActorId: targetActorId,
                aimPos: Vec3.Zero,
                aimDir: Vec3.Forward,
                worldServices: null,
                eventBus: null,
                casterUnit: null,
                targetUnit: targetUnit);
            ctx.Initialize(abilityInstance: null, in req);
            return ctx;
        }

        private static int FindExistingBuffIndex(List<BuffRuntime> list, int buffId)
        {
            if (list == null) return -1;

            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b == null) continue;
                if (b.BuffId == buffId) return i;
            }

            return -1;
        }

        private static bool ApplyToExisting(BuffRuntime existing, BuffMO buff, ApplyBuffRequestComponent req, float durationSeconds, EffectSourceRegistry effectSource, ITriggerActionRunner actionRunner, int frame, int sourceActorId, int targetActorId)
        {
            if (existing == null) return false;

            switch (buff.StackingPolicy)
            {
                case BuffStackingPolicy.IgnoreIfExists:
                    return false;
                case BuffStackingPolicy.Replace:
                    CancelAndEnd(existing, effectSource, actionRunner, frame);
                    existing.SourceId = req.SourceId;
                    existing.StackCount = 0;
                    existing.Remaining = durationSeconds;
                    AddStack(existing, buff.MaxStacks);
                    return true;
                case BuffStackingPolicy.AddStack:
                    AddStack(existing, buff.MaxStacks);
                    RefreshRemaining(existing, buff.RefreshPolicy, durationSeconds);
                    existing.SourceId = req.SourceId;
                    return true;
                case BuffStackingPolicy.RefreshDuration:
                    RefreshRemaining(existing, buff.RefreshPolicy, durationSeconds);
                    existing.SourceId = req.SourceId;
                    return true;
                case BuffStackingPolicy.None:
                default:
                    return false;
            }
        }

        private static BuffRuntime CreateNewRuntime(BuffMO buff, ApplyBuffRequestComponent req, float durationSeconds)
        {
            var rt = new BuffRuntime
            {
                BuffId = buff.Id,
                Remaining = durationSeconds,
                IntervalRemainingSeconds = 0,
                SourceId = req.SourceId,
                StackCount = 0,
                SourceContextId = 0,
            };

            AddStack(rt, buff.MaxStacks);
            return rt;
        }

        private static void RefreshRemaining(BuffRuntime rt, BuffRefreshPolicy policy, float durationSeconds)
        {
            if (rt == null) return;

            switch (policy)
            {
                case BuffRefreshPolicy.ResetRemaining:
                    rt.Remaining = durationSeconds;
                    return;
                case BuffRefreshPolicy.AddRemaining:
                    rt.Remaining += durationSeconds;
                    return;
                case BuffRefreshPolicy.KeepRemaining:
                case BuffRefreshPolicy.None:
                default:
                    return;
            }
        }

        private static void AddStack(BuffRuntime rt, int maxStacks)
        {
            if (rt == null) return;

            if (maxStacks <= 0) maxStacks = int.MaxValue;
            if (rt.StackCount >= maxStacks) return;

            rt.StackCount++;
        }

        private static void PublishBuffEvent(IEventBus bus, EffectSourceRegistry effectSource, string eventId, BuffMO buff, int sourceActorId, int targetActorId, float durationSeconds, BuffRuntime runtime)
        {
            if (bus == null) return;
            if (buff == null) return;
            if (string.IsNullOrEmpty(eventId)) return;

            PublishOnce(bus, effectSource, eventId, buff, sourceActorId, targetActorId, durationSeconds, runtime);
        }

        private static void PublishOnce(IEventBus bus, EffectSourceRegistry effectSource, string eventId, BuffMO buff, int sourceActorId, int targetActorId, float durationSeconds, BuffRuntime runtime)
        {
            if (bus == null) return;
            if (buff == null) return;
            if (string.IsNullOrEmpty(eventId)) return;

            var args = PooledTriggerArgs.Rent();
            args[EffectTriggering.Args.Source] = sourceActorId;
            args[EffectTriggering.Args.Target] = targetActorId;
            args[EffectSourceKeys.SourceActorId] = sourceActorId;
            args[EffectSourceKeys.TargetActorId] = targetActorId;
            args[MobaBuffTriggering.Args.BuffId] = buff.Id;
            args[MobaBuffTriggering.Args.EffectId] = 0;
            args[MobaBuffTriggering.Args.DurationSeconds] = durationSeconds;
            args[MobaBuffTriggering.Args.StackCount] = runtime != null ? runtime.StackCount : 0;
            if (runtime != null && runtime.SourceContextId != 0)
            {
                args[EffectSourceKeys.SourceContextId] = runtime.SourceContextId;

                EffectOriginArgsHelper.FillFromRegistry(args, runtime.SourceContextId, effectSource);
            }

            bus.Publish(new TriggerEvent(eventId, payload: runtime, args: args));
        }

        private static void PublishBuffEffectEvent(IEventBus bus, EffectSourceRegistry effectSource, string baseEventId, int effectId, string stage, int sourceActorId, int targetActorId, BuffRuntime runtime)
        {
            // 涓枃娉ㄩ噴锛氶拡瀵瑰崟涓?effectId 鍙戝竷 buff 浜嬩欢锛堝惈 buff.apply.<effectId>锛夈€?
            if (bus == null) return;
            if (string.IsNullOrEmpty(baseEventId)) return;
            if (effectId <= 0) return;

            void Fill(PooledTriggerArgs args)
            {
                args[EffectTriggering.Args.Source] = sourceActorId;
                args[EffectTriggering.Args.Target] = targetActorId;
                args[EffectSourceKeys.SourceActorId] = sourceActorId;
                args[EffectSourceKeys.TargetActorId] = targetActorId;
                args[MobaBuffTriggering.Args.BuffId] = runtime != null ? runtime.BuffId : 0;
                args[MobaBuffTriggering.Args.EffectId] = effectId;
                args[MobaBuffTriggering.Args.Stage] = stage;
                args[MobaBuffTriggering.Args.StackCount] = runtime != null ? runtime.StackCount : 0;
                if (runtime != null && runtime.SourceContextId != 0)
                {
                    args[EffectSourceKeys.SourceContextId] = runtime.SourceContextId;

                    EffectOriginArgsHelper.FillFromRegistry(args, runtime.SourceContextId, effectSource);
                }
            }

            var args1 = PooledTriggerArgs.Rent();
            Fill(args1);
            bus.Publish(new TriggerEvent(baseEventId, payload: runtime, args: args1));

            var args2 = PooledTriggerArgs.Rent();
            Fill(args2);
            bus.Publish(new TriggerEvent(MobaBuffTriggering.Events.WithEffect(baseEventId, effectId), payload: runtime, args: args2));
        }

        private int GetFrame()
        {
            try
            {
                return _frameTime != null ? _frameTime.Frame.Value : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static void EnsureBuffContext(BuffRuntime rt, int buffId, int sourceActorId, int targetActorId, EffectSourceRegistry effectSource, int frame)
        {
            if (rt == null) return;
            if (rt.SourceContextId != 0) return;
            if (effectSource == null) return;

            rt.SourceContextId = effectSource.CreateRoot(
                kind: EffectSourceKind.Buff,
                configId: buffId,
                sourceActorId: sourceActorId,
                targetActorId: targetActorId,
                frame: frame);
        }

        private static void CancelAndEnd(BuffRuntime rt, EffectSourceRegistry effectSource, ITriggerActionRunner actionRunner, int frame)
        {
            if (rt == null) return;

            if (rt.SourceContextId != 0)
            {
                try
                {
                    actionRunner?.CancelByOwnerKey(rt.SourceContextId);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[MobaBuffApplySystem] CancelAndEnd CancelByOwnerKey failed (sourceContextId={rt.SourceContextId})");
                }

                try
                {
                    effectSource?.End(rt.SourceContextId, frame, EffectSourceEndReason.Cancelled);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[MobaBuffApplySystem] CancelAndEnd EffectSource.End failed (sourceContextId={rt.SourceContextId}, frame={frame})");
                }

                rt.SourceContextId = 0;
            }
        }
    }
}
