using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.Moba.Conponents;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaOngoingEffectService : IService
    {
        private readonly MobaConfigDatabase _config;
        private readonly MobaEffectExecutionService _effectExec;
        private readonly MobaActorLookupService _actors;

        private static long _nextInstanceId;

        public MobaOngoingEffectService(MobaConfigDatabase config, MobaEffectExecutionService effectExec, MobaActorLookupService actors)
        {
            _config = config;
            _effectExec = effectExec;
            _actors = actors;
        }

        public IRunningAction Start(int ongoingEffectId, int sourceActorId, int targetActorId)
        {
            return Start(ongoingEffectId, sourceActorId, targetActorId, ownerKey: 0);
        }

        public IRunningAction Start(int ongoingEffectId, int sourceActorId, int targetActorId, long ownerKey)
        {
            if (ongoingEffectId <= 0) return null;
            if (targetActorId <= 0) return null;
            if (_config == null) return null;
            if (_effectExec == null) return null;

            if (!_config.TryGetOngoingEffect(ongoingEffectId, out var cfg) || cfg == null)
            {
                return null;
            }

            // ECS-driven mode: ownerKey == 0 means this ongoing effect is not bound to a cancellation owner.
            // We track it on ActorEntity and let MobaOngoingEffectTickSystem drive tick/expire.
            if (ownerKey == 0)
            {
                if (_actors == null) return null;
                if (!_actors.TryGetActorEntity(targetActorId, out var e) || e == null) return null;

                if (!e.hasOngoingEffects)
                {
                    e.AddOngoingEffects(new List<OngoingEffectRuntime>());
                }

                var list = e.ongoingEffects.Active;
                if (list == null)
                {
                    list = new List<OngoingEffectRuntime>();
                    e.ReplaceOngoingEffects(list);
                }

                var ecsInstanceId = ++_nextInstanceId;
                var rt = new OngoingEffectRuntime
                {
                    InstanceId = ecsInstanceId,
                    OngoingEffectId = ongoingEffectId,
                    SourceActorId = sourceActorId,
                    RemainingMs = cfg.DurationMs,
                    NextTickMs = cfg.PeriodMs,
                    OwnerKey = 0,
                };
                list.Add(rt);

                // apply immediately
                if (cfg.OnApplyEffectId > 0)
                {
                    var ctx = OngoingEffectRunningAction.BuildEffectContext(sourceActorId, targetActorId);
                    _effectExec.Execute(cfg.OnApplyEffectId, ctx, AbilityKit.Ability.Impl.Moba.EffectExecuteMode.InternalOnly);
                }

                return null;
            }

            var instanceId = ++_nextInstanceId;
            return new OngoingEffectRunningAction(_effectExec, _actors, instanceId, ongoingEffectId, cfg.DurationMs, cfg.PeriodMs, cfg.OnApplyEffectId, cfg.OnTickEffectId, cfg.OnRemoveEffectId, sourceActorId, targetActorId, ownerKey);
        }

        private sealed class OngoingEffectRunningAction : IRunningAction
        {
            private readonly MobaEffectExecutionService _exec;
            private readonly MobaActorLookupService _actors;

            private readonly long _instanceId;
            private readonly int _ongoingEffectId;
            private readonly int _durationMs;
            private readonly int _periodMs;
            private readonly int _onApplyEffectId;
            private readonly int _onTickEffectId;
            private readonly int _onRemoveEffectId;
            private readonly int _sourceActorId;
            private readonly int _targetActorId;
            private readonly long _ownerKey;

            private OngoingEffectRuntime _rt;

            private int _elapsedMs;
            private int _nextTickMs;
            private bool _done;
            private bool _applied;

            public OngoingEffectRunningAction(
                MobaEffectExecutionService exec,
                MobaActorLookupService actors,
                long instanceId,
                int ongoingEffectId,
                int durationMs,
                int periodMs,
                int onApplyEffectId,
                int onTickEffectId,
                int onRemoveEffectId,
                int sourceActorId,
                int targetActorId,
                long ownerKey)
            {
                _exec = exec;
                _actors = actors;
                _instanceId = instanceId;
                _ongoingEffectId = ongoingEffectId;
                _durationMs = durationMs;
                _periodMs = periodMs;
                _onApplyEffectId = onApplyEffectId;
                _onTickEffectId = onTickEffectId;
                _onRemoveEffectId = onRemoveEffectId;
                _sourceActorId = sourceActorId;
                _targetActorId = targetActorId;
                _ownerKey = ownerKey;

                _elapsedMs = 0;
                _nextTickMs = periodMs > 0 ? periodMs : 0;

                TryAttachToActor();
            }

            public bool IsDone => _done;

            public void Tick(float deltaTime)
            {
                if (_done) return;

                if (!_applied)
                {
                    _applied = true;
                    TryExecuteEffect(_onApplyEffectId);
                }

                if (deltaTime <= 0f) return;

                var addMs = (int)MathF.Round(deltaTime * 1000f);
                if (addMs <= 0) return;

                _elapsedMs += addMs;

                if (_rt != null)
                {
                    _rt.RemainingMs = _durationMs > 0 ? global::System.Math.Max(0, _durationMs - _elapsedMs) : 0;
                    _rt.NextTickMs = _nextTickMs;
                }

                if (_durationMs > 0 && _elapsedMs >= _durationMs)
                {
                    _done = true;
                    RemoveInternal();
                    return;
                }

                if (_periodMs > 0 && _onTickEffectId > 0)
                {
                    while (_nextTickMs > 0 && _elapsedMs >= _nextTickMs)
                    {
                        TryExecuteEffect(_onTickEffectId);
                        _nextTickMs += _periodMs;

                        if (_rt != null)
                        {
                            _rt.NextTickMs = _nextTickMs;
                        }

                        if (_durationMs > 0 && _elapsedMs >= _durationMs)
                        {
                            _done = true;
                            RemoveInternal();
                            return;
                        }
                    }
                }
            }

            public void Cancel()
            {
                if (_done) return;
                _done = true;
                RemoveInternal();
            }

            public void Dispose()
            {
                if (_done) return;
                _done = true;
                RemoveInternal();
            }

            private void RemoveInternal()
            {
                TryExecuteEffect(_onRemoveEffectId);
                TryDetachFromActor();
            }

            private void TryAttachToActor()
            {
                if (_actors == null) return;
                if (!_actors.TryGetActorEntity(_targetActorId, out var e) || e == null) return;

                if (!e.hasOngoingEffects)
                {
                    e.AddOngoingEffects(new List<OngoingEffectRuntime>());
                }

                var list = e.ongoingEffects.Active;
                if (list == null)
                {
                    list = new List<OngoingEffectRuntime>();
                    e.ReplaceOngoingEffects(list);
                }

                _rt = new OngoingEffectRuntime
                {
                    InstanceId = _instanceId,
                    OngoingEffectId = _ongoingEffectId,
                    SourceActorId = _sourceActorId,
                    RemainingMs = _durationMs,
                    NextTickMs = _nextTickMs,
                    OwnerKey = _ownerKey,
                };

                list.Add(_rt);
            }

            private void TryDetachFromActor()
            {
                if (_rt == null) return;
                if (_actors == null) return;
                if (!_actors.TryGetActorEntity(_targetActorId, out var e) || e == null) return;
                if (!e.hasOngoingEffects) return;

                var list = e.ongoingEffects.Active;
                if (list == null || list.Count == 0) return;

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var it = list[i];
                    if (it == null) { list.RemoveAt(i); continue; }
                    if (it.InstanceId == _instanceId)
                    {
                        list.RemoveAt(i);
                        break;
                    }
                }

                _rt = null;
            }

            private void TryExecuteEffect(int effectId)
            {
                if (effectId <= 0) return;

                try
                {
                    var ctx = BuildEffectContext(_sourceActorId, _targetActorId);
                    _exec.Execute(effectId, ctx, AbilityKit.Ability.Impl.Moba.EffectExecuteMode.InternalOnly);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[OngoingEffect] Execute failed. ongoingEffectId={_ongoingEffectId} effectId={effectId} ex={ex.Message}");
                }
            }

            internal static MobaEffectPipelineContext BuildEffectContext(int sourceActorId, int targetActorId)
            {
                var ctx = new MobaEffectPipelineContext();
                ctx.Initialize(
                    abilityInstance: null,
                    sourceActorId: sourceActorId,
                    targetActorId: targetActorId,
                    contextKind: 0,
                    sourceContextId: 0,
                    worldServices: null,
                    eventBus: null);
                return ctx;
            }
        }

        public void Dispose()
        {
        }
    }
}
