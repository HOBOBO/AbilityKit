using System;
using AbilityKit.Core.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Common.Log;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Effect;
using AbilityKit.Core.Math;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Core.Common.MotionSystem.Core;

namespace AbilityKit.Demo.Moba.Triggering
{
    using AbilityKit.Ability;
    public sealed class KnockAction : ITriggerAction
    {
        private readonly float _horizontalSpeed;
        private readonly float _verticalSpeed;
        private readonly int _durationMs;
        private readonly float _gravity;
        private readonly int _priority;

        private readonly KnockDirectionMode _directionMode;

        public KnockAction(float horizontalSpeed, float verticalSpeed, int durationMs, float gravity, int priority, KnockDirectionMode directionMode)
        {
            _horizontalSpeed = horizontalSpeed;
            _verticalSpeed = verticalSpeed;
            _durationMs = durationMs;
            _gravity = gravity;
            _priority = priority;
            _directionMode = directionMode;
        }

        public static KnockAction FromDef(ActionDef def)
        {
            var args = def?.Args;

            var horizontalSpeed = TriggerActionArgUtil.TryGetFloat(args, "horizontalSpeed", 5f);
            var verticalSpeed = TriggerActionArgUtil.TryGetFloat(args, "verticalSpeed", 5f);
            var durationMs = TriggerActionArgUtil.TryGetInt(args, "durationMs", 300);
            var gravity = TriggerActionArgUtil.TryGetFloat(args, "gravity", 9.8f);
            var priority = TriggerActionArgUtil.TryGetInt(args, "priority", 100);

            var directionMode = KnockDirectionMode.FromSourceToTarget;
            if (args != null && args.TryGetValue("directionMode", out var obj) && obj != null)
            {
                directionMode = TriggerActionArgUtil.ParseEnum(obj, KnockDirectionMode.FromSourceToTarget);
            }

            return new KnockAction(horizontalSpeed, verticalSpeed, durationMs, gravity, priority, directionMode);
        }

        public void Execute(TriggerContext context)
        {
            if (context == null) return;

            if (!TriggerActionArgUtil.TryResolveActorId(context.Target, out var targetActorId) || targetActorId <= 0)
            {
                Log.Warning("[Trigger] knock requires context.Target with valid actorId");
                return;
            }

            var actorRegistry = context.Services?.GetService(typeof(MobaActorRegistry)) as MobaActorRegistry;
            if (actorRegistry == null || !actorRegistry.TryGet(targetActorId, out var targetEntity) || targetEntity == null || !targetEntity.hasMotion)
            {
                Log.Warning("[Trigger] knock requires target has Motion component");
                return;
            }

            var m = targetEntity.motion;
            if (!m.Initialized || m.Pipeline == null)
            {
                Log.Warning("[Trigger] knock requires Motion initialized");
                return;
            }

            var dir = Vec3.Forward;

            if (_directionMode == KnockDirectionMode.UseAimDir)
            {
                var payload = context.Event.Payload;
                if (payload is IAbilityPipelineContext pc)
                {
                    dir = pc.GetAimDir();
                }
                else if (payload is IEffectContext ec && ec.TryGetSkill(out var skill))
                {
                    dir = skill.AimDir;
                }
            }
            else
            {
                Vec3 from = Vec3.Zero;
                Vec3 to = Vec3.Zero;

                if (_directionMode == KnockDirectionMode.FromAreaCenterToTarget)
                {
                    if (context.Event.Args != null && context.Event.Args.TryGetValue("area.center", out var c) && c is Vec3 v3)
                    {
                        from = v3;
                    }
                }

                if (from.Equals(Vec3.Zero))
                {
                    if (TriggerActionArgUtil.TryResolveActorId(context.Source, out var sourceActorId) && sourceActorId > 0)
                    {
                        var registry = context.Services?.GetService(typeof(MobaActorRegistry)) as MobaActorRegistry;
                        if (registry != null && registry.TryGet(sourceActorId, out var se) && se != null && se.hasTransform)
                        {
                            from = se.transform.Value.Position;
                        }
                    }
                }

                if (actorRegistry.TryGet(targetActorId, out var te) && te != null && te.hasTransform)
                {
                    to = te.transform.Value.Position;
                }

                var delta = to - from;
                if (delta.SqrMagnitude > 0f)
                {
                    dir = new Vec3(delta.X, 0f, delta.Z).Normalized;
                }
            }

            if (dir.SqrMagnitude <= 0f) dir = Vec3.Forward;

            var velocity = dir * _horizontalSpeed + Vec3.Up * _verticalSpeed;
            var duration = _durationMs > 0 ? (_durationMs / 1000f) : 0.1f;

            var knock = new KnockMotionSource(velocity, duration, gravity: _gravity, priority: _priority);
            m.Pipeline.AddSource(knock);

            targetEntity.ReplaceMotion(
                newPipeline: m.Pipeline,
                newState: m.State,
                newOutput: m.Output,
                newSolver: m.Solver,
                newPolicy: m.Policy,
                newEvents: m.Events,
                newInitialized: m.Initialized);

            var frameTime = context.Services?.GetService(typeof(IFrameTime)) as IFrameTime;
            if (context.Event.Args is System.Collections.Generic.IDictionary<string, object> dict && frameTime != null)
            {
                dict["knock.frame"] = frameTime.Frame.Value;
            }
        }

        private sealed class KnockMotionSource : IMotionSource, IMotionFinishEventSource
        {
            private readonly int _priority;
            private Vec3 _velocity;
            private float _gravity;
            private float _timeLeft;
            private bool _active;

            public KnockMotionSource(in Vec3 velocity, float duration, float gravity, int priority)
            {
                _velocity = velocity;
                _timeLeft = duration;
                _gravity = gravity;
                _priority = priority;
                _active = duration > 0f;
            }

            public int GroupId => MotionGroups.Control;
            public MotionStacking Stacking => MotionStacking.ExclusiveHighestPriority;
            public MotionFinishEvent FinishEvent => MotionFinishEvent.Expired;
            public int Priority => _priority;
            public bool IsActive => _active;

            public void Cancel()
            {
                _timeLeft = 0f;
                _active = false;
            }

            public void Tick(int id, ref MotionState state, float dt, ref Vec3 outDesiredDelta)
            {
                if (!_active) return;
                if (dt <= 0f) return;

                if (_timeLeft <= 0f)
                {
                    _active = false;
                    return;
                }

                var step = dt;
                if (step > _timeLeft) step = _timeLeft;
                _timeLeft -= dt;

                outDesiredDelta = outDesiredDelta + _velocity * step;

                if (_gravity > 0f)
                {
                    _velocity = _velocity + Vec3.Down * (_gravity * dt);
                }

                if (_timeLeft <= 0f) _active = false;
            }
        }

        public enum KnockDirectionMode
        {
            FromSourceToTarget = 0,
            FromAreaCenterToTarget = 1,
            UseAimDir = 2,
        }
    }
}
