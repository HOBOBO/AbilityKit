using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Deterministic;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Runtime;

namespace AbilityKit.Demo.Moba.Services.StateMachine
{
    public sealed class MobaCharacterHfsmFacts
    {
        public bool Alive;
        public bool Controlled;
        public bool Moving;
        public long CastInstanceId;
        public int SkillId;
    }

    public static class MobaCharacterHfsmProfile
    {
        public const string Id = "moba.character.default";
        public const string ResourcePath = "moba/character_hfsm";

        public static StateMachineDefinition LoadDefinition(ITextAssetLoader loader)
        {
            if (loader != null && loader.TryLoadText(ResourcePath, out var json) &&
                !string.IsNullOrWhiteSpace(json))
            {
                var definition = DefinitionJson.Load(json);
                if (!string.Equals(definition.DefinitionId, Id, StringComparison.Ordinal))
                    throw new InvalidOperationException("MOBA character HFSM definition id does not match.");
                return definition;
            }
            return CreateDefinition();
        }

        public static StateMachineDefinition CreateDefinition()
        {
            var life = new MachineDefinition { Id = "life", InitialStateId = "alive" };
            life.States.Add(new StateDefinition { Id = "alive", ChildMachineId = "action" });
            life.States.Add(new StateDefinition { Id = "dead" });
            life.Transitions.Add(Edge("die", "alive", "dead", "dead", 100));
            life.Transitions.Add(Edge("respawn", "dead", "alive", "alive", 100));

            var action = new MachineDefinition { Id = "action", InitialStateId = "idle" };
            foreach (var id in new[] { "idle", "moving", "casting", "controlled" })
                action.States.Add(new StateDefinition { Id = id });

            foreach (var source in new[] { "idle", "moving", "casting" })
                action.Transitions.Add(Edge(source + ".control", source, "controlled", "controlled", 100));
            foreach (var source in new[] { "idle", "moving" })
                action.Transitions.Add(Edge(source + ".cast", source, "casting", "casting", 70));
            foreach (var source in new[] { "idle", "casting" })
                action.Transitions.Add(Edge(source + ".move", source, "moving", "moving", 40));
            foreach (var source in new[] { "moving", "casting" })
                action.Transitions.Add(Edge(source + ".idle", source, "idle", "idle", 10));
            action.Transitions.Add(Edge("controlled.cast", "controlled", "casting", "free.cast", 70));
            action.Transitions.Add(Edge("controlled.move", "controlled", "moving", "free.move", 40));
            action.Transitions.Add(Edge("controlled.idle", "controlled", "idle", "free.idle", 10));

            return new StateMachineDefinition
            {
                DefinitionId = Id,
                RootMachineId = life.Id,
                Machines = new List<MachineDefinition> { life, action }
            };
        }

        public static RuntimeBindings<MobaCharacterHfsmFacts> CreateBindings()
        {
            var bindings = new RuntimeBindings<MobaCharacterHfsmFacts>();
            bindings.RegisterCondition("dead", () => new FactCondition(f => !f.Alive));
            bindings.RegisterCondition("alive", () => new FactCondition(f => f.Alive));
            bindings.RegisterCondition("controlled", () => new FactCondition(f => f.Controlled));
            bindings.RegisterCondition("casting", () => new FactCondition(f => !f.Controlled && f.CastInstanceId > 0));
            bindings.RegisterCondition("moving", () => new FactCondition(f => !f.Controlled && f.CastInstanceId == 0 && f.Moving));
            bindings.RegisterCondition("idle", () => new FactCondition(f => !f.Controlled && f.CastInstanceId == 0 && !f.Moving));
            bindings.RegisterCondition("free.cast", () => new FactCondition(f => !f.Controlled && f.CastInstanceId > 0));
            bindings.RegisterCondition("free.move", () => new FactCondition(f => !f.Controlled && f.CastInstanceId == 0 && f.Moving));
            bindings.RegisterCondition("free.idle", () => new FactCondition(f => !f.Controlled && f.CastInstanceId == 0 && !f.Moving));
            return bindings;
        }

        private static TransitionDefinition Edge(string id, string from, string to, string condition, int priority)
        {
            return new TransitionDefinition
            {
                Id = id, FromStateId = from, ToStateId = to,
                ConditionKey = condition, Priority = priority
            };
        }

        private sealed class FactCondition : ITransitionCondition<MobaCharacterHfsmFacts>
        {
            private readonly Func<MobaCharacterHfsmFacts, bool> _predicate;
            public FactCondition(Func<MobaCharacterHfsmFacts, bool> predicate) => _predicate = predicate;
            public bool Evaluate(MobaCharacterHfsmFacts owner, in TransitionContext context) => _predicate(owner);
        }
    }

    public readonly struct MobaCharacterActionState
    {
        public readonly string Path;
        public readonly long InstanceId;
        public readonly int StartFrame;
        public readonly int LocalFrame;
        public readonly long CastInstanceId;
        public readonly int SkillId;

        public MobaCharacterActionState(string path, long instanceId, int startFrame, int localFrame,
            long castInstanceId, int skillId)
        {
            Path = path ?? string.Empty;
            InstanceId = instanceId;
            StartFrame = startFrame;
            LocalFrame = localFrame;
            CastInstanceId = castInstanceId;
            SkillId = skillId;
        }
    }

    public sealed class MobaCharacterHfsmSnapshot
    {
        public RuntimeSnapshot Machine;
        public MobaCharacterActionState Action;
    }

    public sealed class MobaCharacterHfsmRuntime : IDisposable
    {
        private readonly int _actorId;
        private readonly StateMachineDefinition _definition;
        private readonly MobaCharacterHfsmFacts _facts = new MobaCharacterHfsmFacts();
        private readonly StateMachineRuntime<MobaCharacterHfsmFacts> _machine;
        private MobaCharacterActionState _action;
        private bool _disposed;

        public MobaCharacterHfsmRuntime(int actorId, StateMachineDefinition definition, int frame, Fixed64 time)
        {
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            _actorId = actorId;
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _machine = new StateMachineRuntime<MobaCharacterHfsmFacts>(_facts, definition,
                MobaCharacterHfsmProfile.CreateBindings());
            _facts.Alive = true;
            _machine.Initialize(frame, time);
            UpdateAction(frame);
        }

        public long DefinitionHash => _machine.DefinitionHash;
        public int Frame => _machine.CurrentFrame;
        public Fixed64 Time => _machine.CurrentTime;
        public MobaCharacterActionState Action => _action;

        public void Tick(int frame, Fixed64 time, bool alive, bool controlled, bool moving,
            long castInstanceId, int skillId)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MobaCharacterHfsmRuntime));
            if (frame <= Frame) return;
            _facts.Alive = alive;
            _facts.Controlled = controlled;
            _facts.Moving = moving;
            _facts.CastInstanceId = castInstanceId;
            _facts.SkillId = skillId;
            _machine.Tick(frame, time);
            UpdateAction(frame);
        }

        public MobaCharacterHfsmSnapshot CaptureSnapshot() => new MobaCharacterHfsmSnapshot
        {
            Machine = _machine.CaptureSnapshot(), Action = _action
        };

        public void RestoreSnapshot(MobaCharacterHfsmSnapshot snapshot)
        {
            if (snapshot?.Machine == null) throw new ArgumentNullException(nameof(snapshot));
            var path = ActivePath(snapshot.Machine, _definition);
            var action = snapshot.Action;
            if (snapshot.Machine.DefinitionHash != DefinitionHash || !snapshot.Machine.Initialized ||
                !string.Equals(path, action.Path, StringComparison.Ordinal) ||
                action.StartFrame < 0 || action.StartFrame > snapshot.Machine.Frame ||
                action.LocalFrame != snapshot.Machine.Frame - action.StartFrame ||
                action.InstanceId != (((long)_actorId << 32) | (uint)action.StartFrame) ||
                (path.EndsWith("/casting", StringComparison.Ordinal) && action.CastInstanceId <= 0) ||
                (!path.EndsWith("/casting", StringComparison.Ordinal) &&
                    (action.CastInstanceId != 0 || action.SkillId != 0)))
                throw new InvalidOperationException("Character HFSM action does not match the restored machine.");
            _machine.RestoreSnapshot(snapshot.Machine);
            _action = action;
        }

        public static string ActivePath(RuntimeSnapshot snapshot, StateMachineDefinition definition)
        {
            if (snapshot?.Machines == null || definition?.Machines == null) return string.Empty;
            var parts = new List<string>();
            var machineId = definition.RootMachineId;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (!string.IsNullOrEmpty(machineId) && visited.Add(machineId))
            {
                var currentId = machineId;
                var machine = definition.Machines.Find(m => m.Id == currentId);
                var active = snapshot.Machines.Find(m => m.MachineId == currentId);
                if (machine == null || active == null || string.IsNullOrEmpty(active.ActiveStateId))
                    return string.Empty;
                parts.Add(currentId + "/" + active.ActiveStateId);
                var state = machine.States.Find(s => s.Id == active.ActiveStateId);
                if (state == null) return string.Empty;
                machineId = state.ChildMachineId;
            }
            return string.IsNullOrEmpty(machineId) ? string.Join("/", parts) : string.Empty;
        }

        private void UpdateAction(int frame)
        {
            var path = string.Join("/", _machine.GetActivePath());
            var cast = path.EndsWith("/casting", StringComparison.Ordinal) ? _facts.CastInstanceId : 0L;
            if (!string.Equals(path, _action.Path, StringComparison.Ordinal) || cast != _action.CastInstanceId)
            {
                // The actor/frame identity is stable across rollback and independent of allocation order.
                var instanceId = ((long)_actorId << 32) | (uint)frame;
                _action = new MobaCharacterActionState(path, instanceId, frame, 0, cast,
                    cast > 0 ? _facts.SkillId : 0);
            }
            else
            {
                _action = new MobaCharacterActionState(path, _action.InstanceId, _action.StartFrame,
                    frame - _action.StartFrame, cast, _action.SkillId);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_machine.IsInitialized && !_machine.IsFaulted) _machine.Shutdown();
        }
    }
}
