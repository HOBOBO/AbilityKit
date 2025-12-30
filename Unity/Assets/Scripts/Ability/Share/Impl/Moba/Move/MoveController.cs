using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Share;

namespace AbilityKit.Ability.Share.Impl.Moba.Move
{
    public sealed class MoveController
    {
        private readonly List<IMoveTask> _tasks = new List<IMoveTask>(4);
        private readonly MovePolicy _policy;
        private readonly InputMoveTask _input;

        private MobaMoveDisableMask _disableMask;

        public Vec3 LastDeltaInput { get; private set; }
        public Vec3 LastDeltaAbility { get; private set; }
        public Vec3 LastDeltaControl { get; private set; }
        public Vec3 LastDeltaFinal { get; private set; }

        public MobaMoveDisableMask DisableMask => _disableMask;

        public readonly struct TaskSnapshot
        {
            [BinaryMember(0)] public readonly MobaMoveKind Kind;
            [BinaryMember(1)] public readonly int Priority;
            [BinaryMember(2)] public readonly float TimeLeft;
            [BinaryMember(3)] public readonly Vec3 Velocity;
            [BinaryMember(4)] public readonly float Gravity;

            public TaskSnapshot(MobaMoveKind kind, int priority, float timeLeft, in Vec3 velocity, float gravity)
            {
                Kind = kind;
                Priority = priority;
                TimeLeft = timeLeft;
                Velocity = velocity;
                Gravity = gravity;
            }
        }

        public readonly struct ControllerSnapshot
        {
            [BinaryMember(0)] public readonly int DisableMask;
            [BinaryMember(1)] public readonly float InputDx;
            [BinaryMember(2)] public readonly float InputDz;
            [BinaryMember(3)] public readonly TaskSnapshot[] Tasks;

            public ControllerSnapshot(int disableMask, float inputDx, float inputDz, TaskSnapshot[] tasks)
            {
                DisableMask = disableMask;
                InputDx = inputDx;
                InputDz = inputDz;
                Tasks = tasks;
            }
        }

        public MoveController(MovePolicy policy, float inputSpeed)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _input = new InputMoveTask(inputSpeed);
            _tasks.Add(_input);
            _disableMask = MobaMoveDisableMask.None;
        }

        public void SetDisableMask(MobaMoveDisableMask mask)
        {
            _disableMask = mask;
        }

        public void AddDisable(MobaMoveDisableMask mask)
        {
            _disableMask |= mask;
        }

        public void RemoveDisable(MobaMoveDisableMask mask)
        {
            _disableMask &= ~mask;
        }

        public void SetInput(float dx, float dz)
        {
            _input.SetInput(dx, 0f, dz);
        }

        public void Dash(in Vec3 velocity, float duration, int priority)
        {
            _policy.TryAddTask(_tasks, new DashMoveTask(velocity, duration, priority));
        }

        public void Knock(in Vec3 velocity, float duration, float gravity, int priority)
        {
            _policy.TryAddTask(_tasks, new KnockMoveTask(velocity, duration, gravity, priority));
        }

        public ControllerSnapshot ExportState()
        {
            var tmp = new List<TaskSnapshot>(4);

            for (int i = 0; i < _tasks.Count; i++)
            {
                var t = _tasks[i];
                if (t == null) continue;
                if (ReferenceEquals(t, _input)) continue;

                if (t is DashMoveTask dash)
                {
                    tmp.Add(new TaskSnapshot(MobaMoveKind.Dash, dash.Priority, dash.TimeLeft, dash.Velocity, 0f));
                    continue;
                }

                if (t is KnockMoveTask knock)
                {
                    tmp.Add(new TaskSnapshot(MobaMoveKind.Knock, knock.Priority, knock.TimeLeft, knock.Velocity, knock.Gravity));
                    continue;
                }
            }

            var input = _input.Input;
            return new ControllerSnapshot((int)_disableMask, input.X, input.Z, tmp.ToArray());
        }

        public void ImportState(in ControllerSnapshot snapshot)
        {
            _disableMask = (MobaMoveDisableMask)snapshot.DisableMask;
            _input.SetInput(snapshot.InputDx, 0f, snapshot.InputDz);

            for (int i = _tasks.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(_tasks[i], _input))
                {
                    _tasks.RemoveAt(i);
                }
            }

            if (snapshot.Tasks == null || snapshot.Tasks.Length == 0) return;

            for (int i = 0; i < snapshot.Tasks.Length; i++)
            {
                var t = snapshot.Tasks[i];
                if (t.TimeLeft <= 0f) continue;

                switch (t.Kind)
                {
                    case MobaMoveKind.Dash:
                        _tasks.Add(new DashMoveTask(t.Velocity, t.TimeLeft, t.Priority));
                        break;
                    case MobaMoveKind.Knock:
                        _tasks.Add(new KnockMoveTask(t.Velocity, t.TimeLeft, t.Gravity, t.Priority));
                        break;
                }
            }
        }

        public Vec3 Tick(float dt)
        {
            var allowLocomotion = _policy.ShouldApplyLocomotion(_tasks);
            var deltaInput = Vec3.Zero;
            var deltaAbility = Vec3.Zero;
            var deltaControl = Vec3.Zero;

            for (int i = _tasks.Count - 1; i >= 0; i--)
            {
                var t = _tasks[i];
                if (t == null)
                {
                    _tasks.RemoveAt(i);
                    continue;
                }

                if (t.IsFinished)
                {
                    _tasks.RemoveAt(i);
                    continue;
                }

                var d = t.Tick(dt);

                if (!allowLocomotion && t.Group == MobaMoveGroup.Locomotion)
                {
                    // Strategy 1: still tick, but contribution suppressed.
                    continue;
                }

                switch (t.Group)
                {
                    case MobaMoveGroup.Locomotion:
                        deltaInput = deltaInput + d;
                        break;
                    case MobaMoveGroup.Ability:
                        deltaAbility = deltaAbility + d;
                        break;
                    case MobaMoveGroup.Control:
                        deltaControl = deltaControl + d;
                        break;
                }
            }

            if ((_disableMask & MobaMoveDisableMask.Input) != 0) deltaInput = Vec3.Zero;
            if ((_disableMask & MobaMoveDisableMask.Ability) != 0) deltaAbility = Vec3.Zero;
            if ((_disableMask & MobaMoveDisableMask.Control) != 0) deltaControl = Vec3.Zero;

            var final = deltaInput + deltaAbility + deltaControl;

            if ((_disableMask & MobaMoveDisableMask.Horizontal) != 0)
            {
                final = new Vec3(0f, final.Y, 0f);
            }

            if ((_disableMask & MobaMoveDisableMask.Vertical) != 0)
            {
                final = new Vec3(final.X, 0f, final.Z);
            }

            LastDeltaInput = deltaInput;
            LastDeltaAbility = deltaAbility;
            LastDeltaControl = deltaControl;
            LastDeltaFinal = final;

            return final;
        }
    }
}
