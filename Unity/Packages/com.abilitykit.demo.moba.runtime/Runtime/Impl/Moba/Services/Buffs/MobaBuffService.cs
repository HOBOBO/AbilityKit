using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO;
using AbilityKit.Ability.Impl.Moba.Conponents;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaBuffService : IService
    {
        private enum BuffCommandKind
        {
            Apply = 0,
            Remove = 1,
        }

        private sealed class BuffCommand
        {
            public long Seq;
            public BuffCommandKind Kind;

            public global::ActorEntity Target;
            public int BuffId;
            public int SourceActorId;
            public int DurationOverrideMs;
            public object OriginSource;
            public object OriginTarget;
            public long ParentContextId;

            public EffectSourceEndReason RemoveReason;
        }

        private readonly MobaConfigDatabase _configs;
        private readonly AbilityKit.Triggering.Eventing.IEventBus _eventBus;
        private readonly ITriggerActionRunner _actionRunner;
        private readonly MobaPeriodicEffectService _ongoing;
        private readonly EffectSourceRegistry _effectSource;
        private readonly MobaActorLookupService _actors;
        private readonly IWorldResolver _services;

        private readonly BuffRepository _repo;
        private readonly BuffContextService _ctx;
        private readonly BuffEventPublisher _events;
        private readonly BuffPeriodicEffectBinder _periodicBinder;
        private readonly BuffStageEffectExecutor _stageEffects;
        private readonly BuffStackingPolicyApplier _stacking;

        private long _nextCommandSeq;
        private readonly List<BuffCommand> _pending = new List<BuffCommand>(32);
        private int _draining;

        public MobaBuffService(MobaConfigDatabase configs, AbilityKit.Triggering.Eventing.IEventBus eventBus, ITriggerActionRunner actionRunner, MobaPeriodicEffectService ongoing, EffectSourceRegistry effectSource, IFrameTime frameTime, MobaActorLookupService actors, MobaEffectInvokerService invoker, IWorldResolver services)
        {
            _configs = configs;
            _eventBus = eventBus;
            _actionRunner = actionRunner;
            _ongoing = ongoing;
            _effectSource = effectSource;
            _actors = actors;
            _services = services;

            _repo = new BuffRepository();
            _ctx = new BuffContextService(effectSource, actionRunner, frameTime);
            _events = new BuffEventPublisher(eventBus);
            _periodicBinder = new BuffPeriodicEffectBinder(ongoing, actionRunner);
            _stageEffects = new BuffStageEffectExecutor(invoker);
            _stacking = new BuffStackingPolicyApplier();
        }

        public global::ActorEntity TryGetActorEntity(int actorId)
        {
            if (_actors != null && _actors.TryGetActorEntity(actorId, out var e) && e != null)
            {
                return e;
            }
            return null;
        }

        public bool ApplyBuffImmediate(global::ActorEntity target, int buffId, int sourceActorId, int durationOverrideMs)
        {
            return ApplyBuffImmediate(target, buffId, sourceActorId, durationOverrideMs, originSource: null, originTarget: null, parentContextId: 0);
        }

        public bool ApplyBuffImmediate(global::ActorEntity target, int buffId, int sourceActorId, int durationOverrideMs, object originSource, object originTarget, long parentContextId)
        {
            if (!EnqueueApply(target, buffId, sourceActorId, durationOverrideMs, originSource, originTarget, parentContextId))
            {
                return false;
            }

            DrainPending(maxCommands: 256);
            return true;
        }

        public bool RemoveBuffImmediate(global::ActorEntity target, int buffId, int sourceActorId, EffectSourceEndReason reason)
        {
            if (!EnqueueRemove(target, buffId, sourceActorId, reason))
            {
                return false;
            }

            DrainPending(maxCommands: 256);
            return true;
        }

        public void DrainPending(int maxCommands)
        {
            if (maxCommands <= 0) return;

            // protect against re-entrancy if drain triggers effects that call ApplyBuffImmediate again.
            if (_draining > 0) return;

            _draining++;
            try
            {
                var executed = 0;
                var cursor = 0;
                while (cursor < _pending.Count)
                {
                    if (executed >= maxCommands)
                    {
                        Log.Warning($"[MobaBuffService] DrainPending exceeded maxCommands={maxCommands}. pending={_pending.Count}.");
                        break;
                    }

                    var cmd = _pending[cursor++];
                    if (cmd == null) continue;

                    try
                    {
                        switch (cmd.Kind)
                        {
                            case BuffCommandKind.Apply:
                                ExecuteApply(cmd.Target, cmd.BuffId, cmd.SourceActorId, cmd.DurationOverrideMs, cmd.OriginSource, cmd.OriginTarget, cmd.ParentContextId);
                                break;
                            case BuffCommandKind.Remove:
                                ExecuteRemove(cmd.Target, cmd.BuffId, cmd.SourceActorId, cmd.RemoveReason);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex, $"[MobaBuffService] Execute buff command failed. kind={cmd.Kind} buffId={cmd.BuffId}");
                    }

                    executed++;
                }

                if (cursor > 0)
                {
                    _pending.RemoveRange(0, cursor);
                }
            }
            finally
            {
                _draining--;
            }
        }

        private bool EnqueueApply(global::ActorEntity target, int buffId, int sourceActorId, int durationOverrideMs, object originSource, object originTarget, long parentContextId)
        {
            if (target == null) return false;
            if (!target.hasActorId) return false;
            if (buffId <= 0) return false;

            _pending.Add(new BuffCommand
            {
                Seq = ++_nextCommandSeq,
                Kind = BuffCommandKind.Apply,
                Target = target,
                BuffId = buffId,
                SourceActorId = sourceActorId,
                DurationOverrideMs = durationOverrideMs,
                OriginSource = originSource,
                OriginTarget = originTarget,
                ParentContextId = parentContextId,
            });
            return true;
        }

        private bool EnqueueRemove(global::ActorEntity target, int buffId, int sourceActorId, EffectSourceEndReason reason)
        {
            if (target == null) return false;
            if (buffId <= 0) return false;

            _pending.Add(new BuffCommand
            {
                Seq = ++_nextCommandSeq,
                Kind = BuffCommandKind.Remove,
                Target = target,
                BuffId = buffId,
                SourceActorId = sourceActorId,
                RemoveReason = reason,
            });
            return true;
        }

        private bool ExecuteApply(global::ActorEntity target, int buffId, int sourceActorId, int durationOverrideMs, object originSource, object originTarget, long parentContextId)
        {
            if (target == null) return false;
            if (!target.hasActorId) return false;
            if (buffId <= 0) return false;

            if (_configs == null) return false;
            if (!_configs.TryGetBuff(buffId, out var buff) || buff == null) return false;

            if (target.hasApplyBuffRequest && target.applyBuffRequest != null && target.applyBuffRequest.BuffId == buffId)
            {
                target.RemoveApplyBuffRequest();
            }

            if (!target.hasBuffs)
            {
                target.AddBuffs(new List<BuffRuntime>());
            }

            var list = _repo.GetOrCreateList(target);

            var duration = durationOverrideMs > 0 ? durationOverrideMs : buff.DurationMs;
            var durationSeconds = duration > 0 ? duration / 1000f : 0f;
            var targetActorId = target.actorId.Value;

            // 通过 request 施加时，补齐 parentContextId 与 origin actorId（如果能解析的话）。
            // 中文注释：originSource/originTarget 可能是 actorId(int) 或其他对象；这里仅在是 int 时写入。
            var originSourceActorId = originSource is int osi ? osi : 0;
            var originTargetActorId = originTarget is int oti ? oti : 0;
            target.ReplaceApplyBuffRequest(buffId, sourceActorId, durationOverrideMs, parentContextId, originSourceActorId, originTargetActorId);

            var existingIndex = BuffRepository.FindExistingBuffIndex(list, buff.Id);
            if (existingIndex >= 0)
            {
                var rt = list[existingIndex];
                var applied = _stacking.ApplyToExisting(rt, buff, sourceActorId, durationSeconds, _ctx);
                _ctx.EnsureBuffContext(rt, buff.Id, sourceActorId, targetActorId, originSource, originTarget, parentContextId);
                _periodicBinder.TryStartPeriodicEffectByBuff(buff, rt, sourceActorId, targetActorId);
                _events.PublishApplyOrRefresh(buff, sourceActorId, targetActorId, durationSeconds, rt);
                if (applied)
                {
                    _stageEffects.Execute(buff.OnAddEffects, buff.Id, sourceActorId, targetActorId, rt.SourceContextId);
                    _events.PublishPerEffect(MobaBuffTriggering.Events.ApplyOrRefresh, buff.OnAddEffects, stage: "add", sourceActorId: sourceActorId, targetActorId: targetActorId, rt);
                }
                return true;
            }

            var created = _stacking.CreateNewRuntime(buff, sourceActorId, durationSeconds);
            {
                _ctx.EnsureBuffContext(created, buff.Id, sourceActorId, targetActorId, originSource, originTarget, parentContextId);
            }
            list.Add(created);
            _periodicBinder.TryStartPeriodicEffectByBuff(buff, created, sourceActorId, targetActorId);
            _events.PublishApplyOrRefresh(buff, sourceActorId, targetActorId, durationSeconds, created);
            _stageEffects.Execute(buff.OnAddEffects, buff.Id, sourceActorId, targetActorId, created.SourceContextId);
            _events.PublishPerEffect(MobaBuffTriggering.Events.ApplyOrRefresh, buff.OnAddEffects, stage: "add", sourceActorId: sourceActorId, targetActorId: targetActorId, created);
            return true;
        }

        private bool ExecuteRemove(global::ActorEntity target, int buffId, int sourceActorId, EffectSourceEndReason reason)
        {
            if (target == null) return false;
            if (buffId <= 0) return false;

            if (target.hasApplyBuffRequest && target.applyBuffRequest != null && target.applyBuffRequest.BuffId == buffId)
            {
                target.RemoveApplyBuffRequest();
            }

            if (!target.hasBuffs) return false;

            var list = target.buffs.Active;
            if (list == null || list.Count == 0) return false;

            var removed = false;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var b = list[i];
                if (b == null) continue;
                if (b.BuffId != buffId) continue;

                removed = true;

                var normalizedReason = reason;
                if (normalizedReason == EffectSourceEndReason.None) normalizedReason = EffectSourceEndReason.Dispelled;

                _ctx.EndByRuntimeNoClear(b, normalizedReason);

                if (_configs != null)
                {
                    if (_configs.TryGetBuff(b.BuffId, out var buff) && buff != null)
                    {
                        _events.PublishRemove(buff, sourceActorId, target.actorId.Value, b, normalizedReason);
                        _stageEffects.Execute(buff.OnRemoveEffects, buff.Id, sourceActorId, target.actorId.Value, b.SourceContextId);
                    }
                }

                b.SourceContextId = 0;

                list.RemoveAt(i);
            }

            return removed;
        }

        public void Dispose()
        {
            _pending.Clear();
        }
    }
}
