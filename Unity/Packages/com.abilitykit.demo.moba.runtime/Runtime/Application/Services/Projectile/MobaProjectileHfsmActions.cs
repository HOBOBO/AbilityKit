using System;
using System.Globalization;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services.StateMachine;
using UnityHFSM.Extension;

namespace AbilityKit.Demo.Moba.Services.Projectile
{
    internal static class MobaProjectileHfsmActions
    {
        public static void Register(MobaActorStateMachineRuntimeRegistry registry)
        {
            registry.RegisterAction(
                "projectile.moveRelative",
                (blackboard, argument) => new MoveProjectileRelativeBehaviour(blackboard, argument));
            registry.RegisterAction(
                "projectile.resume",
                (blackboard, _) => new ResumeProjectileBehaviour(blackboard));
        }
    }

    internal abstract class ProjectileActionBehaviourBase
    {
        protected ProjectileActionBehaviourBase(MobaActorStateMachineBlackboard blackboard)
        {
            Blackboard = blackboard ?? throw new ArgumentNullException(nameof(blackboard));
        }

        protected MobaActorStateMachineBlackboard Blackboard { get; }

        protected bool TryResolve(out IProjectileService projectiles, out ProjectileId projectileId)
        {
            projectiles = null;
            projectileId = default;
            var actor = Blackboard.Actor;
            if (actor == null || !actor.hasActorId || Blackboard.Services == null) return false;
            if (!Blackboard.Services.TryResolve<MobaProjectileLinkService>(out var links) || links == null) return false;
            if (!Blackboard.Services.TryResolve<IProjectileService>(out projectiles) || projectiles == null) return false;
            return links.TryGetProjectileId(actor.actorId.Value, out projectileId);
        }
    }

    internal sealed class MoveProjectileRelativeBehaviour : ProjectileActionBehaviourBase, IRollbackActionBehaviour
    {
        private const string SnapshotKind = "MobaProjectile.MoveRelative";

        private readonly Vec3 _localOffset;
        private readonly float _duration;
        private readonly float _slotSpacing;
        private Vec3 _start;
        private Vec3 _target;
        private float _elapsed;
        private bool _initialized;

        public MoveProjectileRelativeBehaviour(MobaActorStateMachineBlackboard blackboard, string argument)
            : base(blackboard)
        {
            ParseArgument(argument, out _localOffset, out _duration, out _slotSpacing);
        }

        public void Reset()
        {
            _elapsed = 0f;
            _initialized = TryInitialize(useCurrentPositionAsStart: true);
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext context)
        {
            if (!_initialized && !TryInitialize(useCurrentPositionAsStart: true))
                return ActionBehaviourStatus.Failure;
            if (!TryResolve(out var projectiles, out var projectileId))
                return ActionBehaviourStatus.Failure;

            _elapsed += context.GetScaledDelta(useUnscaled: false);
            var progress = _duration <= 0f ? 1f : MathUtil.Min(1f, _elapsed / _duration);
            var position = Vec3.Lerp(in _start, in _target, progress);
            if (!projectiles.TrySetPosition(projectileId, in position))
                return ActionBehaviourStatus.Failure;
            return progress >= 1f ? ActionBehaviourStatus.Success : ActionBehaviourStatus.Running;
        }

        public ActionBehaviourSnapshot CaptureSnapshot()
        {
            return new ActionBehaviourSnapshot(SnapshotKind, floatValue: _elapsed, booleanValue: _initialized);
        }

        public void RestoreSnapshot(ActionBehaviourSnapshot snapshot)
        {
            if (snapshot == null || !string.Equals(snapshot.Kind, SnapshotKind, StringComparison.Ordinal))
                throw new InvalidOperationException($"Cannot restore projectile move snapshot '{snapshot?.Kind}'.");

            _elapsed = snapshot.FloatValue;
            _initialized = snapshot.BooleanValue && TryInitialize(useCurrentPositionAsStart: false);
        }

        private bool TryInitialize(bool useCurrentPositionAsStart)
        {
            if (!TryResolve(out var projectiles, out var projectileId)
                || !projectiles.TryGetRuntimeState(projectileId, out var state))
            {
                return false;
            }

            var forward = state.Direction.SqrMagnitude > 0f ? state.Direction.Normalized : Vec3.Forward;
            var up = Vec3.Up;
            var right = Vec3.Cross(in up, in forward).Normalized;
            if (right.SqrMagnitude <= 0f) right = Vec3.Right;

            var origin = ResolveLauncherOrigin(state.LauncherActorId, state.Position);
            var slotOffset = state.PatternSlotCount > 1
                ? (state.PatternSlotIndex - (state.PatternSlotCount - 1) * 0.5f) * _slotSpacing
                : 0f;
            _target = origin
                      + right * (_localOffset.X + slotOffset)
                      + Vec3.Up * _localOffset.Y
                      + forward * _localOffset.Z;

            if (useCurrentPositionAsStart || _duration <= 0f || _elapsed <= 0f)
            {
                _start = state.Position;
                return true;
            }

            var progress = MathUtil.Min(0.999999f, _elapsed / _duration);
            _start = (state.Position - _target * progress) * (1f / (1f - progress));
            return true;
        }

        private Vec3 ResolveLauncherOrigin(int launcherActorId, in Vec3 fallback)
        {
            if (launcherActorId <= 0
                || !Blackboard.Services.TryResolve<MobaActorRegistry>(out var registry)
                || registry == null
                || !registry.TryGet(launcherActorId, out var launcher)
                || launcher == null
                || !launcher.hasTransform)
            {
                return fallback;
            }

            return launcher.transform.Value.Position;
        }

        private static void ParseArgument(string argument, out Vec3 offset, out float duration, out float slotSpacing)
        {
            var parts = (argument ?? string.Empty).Split(',');
            if (parts.Length != 5)
            {
                throw new FormatException(
                    "projectile.moveRelative expects 'offsetX,offsetY,offsetZ,durationSeconds,slotSpacing'.");
            }

            offset = new Vec3(Parse(parts[0]), Parse(parts[1]), Parse(parts[2]));
            duration = MathUtil.Max(0f, Parse(parts[3]));
            slotSpacing = MathUtil.Max(0f, Parse(parts[4]));
        }

        private static float Parse(string value) =>
            float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    internal sealed class ResumeProjectileBehaviour : ProjectileActionBehaviourBase, IRollbackActionBehaviour
    {
        private const string SnapshotKind = "MobaProjectile.Resume";
        private bool _done;
        private bool _succeeded;

        public ResumeProjectileBehaviour(MobaActorStateMachineBlackboard blackboard)
            : base(blackboard)
        {
        }

        public void Reset()
        {
            _done = false;
            _succeeded = false;
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext context)
        {
            if (!_done)
            {
                _done = true;
                _succeeded = TryResolve(out var projectiles, out var projectileId)
                             && projectiles.ResumeSimulation(projectileId);
            }

            return _succeeded ? ActionBehaviourStatus.Success : ActionBehaviourStatus.Failure;
        }

        public ActionBehaviourSnapshot CaptureSnapshot() =>
            new ActionBehaviourSnapshot(SnapshotKind, integerValue: _succeeded ? 1 : 0, booleanValue: _done);

        public void RestoreSnapshot(ActionBehaviourSnapshot snapshot)
        {
            if (snapshot == null || !string.Equals(snapshot.Kind, SnapshotKind, StringComparison.Ordinal))
                throw new InvalidOperationException($"Cannot restore projectile resume snapshot '{snapshot?.Kind}'.");
            _done = snapshot.BooleanValue;
            _succeeded = snapshot.IntegerValue != 0;
        }
    }
}
