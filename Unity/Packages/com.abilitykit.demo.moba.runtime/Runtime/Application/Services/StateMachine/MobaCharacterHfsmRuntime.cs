using System;
using System.Collections.Generic;
using System.Globalization;
using AbilityKit.Core.Mathematics;
using AbilityKit.Ability.Config;
using AbilityKit.Deterministic;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Runtime;
using AbilityKit.HFSM.Inspection;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services.StateMachine
{
    public sealed class MobaCharacterHfsmActionPayload : IMobaActorContextProvider
    {
        public int ActorId;
        public string StateId;
        public int Frame;

        public bool TryGetSourceActorId(out int actorId) { actorId = ActorId; return actorId > 0; }
        public bool TryGetTargetActorId(out int actorId) { actorId = 0; return false; }
    }

    public sealed class MobaCharacterHfsmFacts
    {
        public int ActorId;
        public MobaCharacterHfsmParameterService Parameters;
        public bool Alive;
        public bool Controlled;
        public bool Moving;
        public long CastInstanceId;
        public int SkillId;
        public int SkillSlot;
        public int BasicAttackSlot;
        public MobaCharacterBindings Bindings;
        public string ModeId;
        public long ModeInstanceId;
        public Vec3 AimPos;
        public Vec3 AimDir;
        // Simulation-frame information is exposed to HFSM conditions so a
        // state-owned action can finish on its authored frame boundary. The
        // values are derived from the authoritative HFSM action snapshot;
        // they are never written by presentation code.
        public int CurrentFrame;
        public int ActionLocalFrame;
        public Action<int, int, string, int> ExecuteLogicTrigger;
    }

    public static class MobaCharacterHfsmProfile
    {
        public const string Id = "moba.character.default";
        public const string ResourcePath = "moba/character_hfsm_default";
        public const string GenericResourcePath = "moba/character_hfsm_default";

        public static StateMachineDefinition LoadDefinition(ITextAssetLoader loader, int heroId)
        {
            if (loader != null && loader.TryLoadText("moba/character_hfsm_" + heroId, out var json) &&
                !string.IsNullOrWhiteSpace(json))
            {
                var definition = DefinitionJson.Load(json);
                if (!string.Equals(definition.DefinitionId, HeroDefinitionId(heroId), StringComparison.Ordinal))
                    throw new InvalidOperationException($"Character HFSM definition id does not match hero {heroId}.");
                return MobaCharacterBindings.Load(definition, loader);
            }
            return LoadDefinition(loader);
        }

        public static string HeroDefinitionId(int heroId) => "moba.character." + heroId;

        public static StateMachineDefinition LoadDefinition(ITextAssetLoader loader)
        {
            if (loader == null || !loader.TryLoadText(GenericResourcePath, out var json) ||
                string.IsNullOrWhiteSpace(json))
                return CreateGenericDefinition();
            var definition = DefinitionJson.Load(json);
            if (!string.Equals(definition.DefinitionId, Id, StringComparison.Ordinal))
                throw new InvalidOperationException("MOBA default character HFSM definition id does not match.");
            return MobaCharacterBindings.Load(definition, loader);
        }

        public static StateMachineDefinition CreateGenericDefinition() => CreateGenericDefinition(includeAttackSlots: false);

        /// <summary>
        /// Shared profile for non-hero combat actors. It adds the standard
        /// loadout attack slots while retaining the same life/action semantics.
        /// </summary>
        public static StateMachineDefinition CreateSummonDefinition() => CreateGenericDefinition(includeAttackSlots: true);

        private static StateMachineDefinition CreateGenericDefinition(bool includeAttackSlots)
        {
            var life = new MachineDefinition { Id = "life", InitialStateId = "alive" };
            life.States.Add(new StateDefinition { Id = "alive", ChildMachineId = "action" });
            life.States.Add(new StateDefinition { Id = "dead" });
            life.Transitions.Add(Edge("die", "alive", "dead", "dead", 100));
            life.Transitions.Add(Edge("respawn", "dead", "alive", "alive", 100));
            var action = new MachineDefinition { Id = "action", InitialStateId = "idle" };
            var actionStates = includeAttackSlots
                ? new[] { "idle", "moving", "casting", "controlled", "attack.slot.1", "attack.slot.2", "attack.slot.3", "attack.slot.4", "attack.slot.5" }
                : new[] { "idle", "moving", "casting", "controlled" };
            foreach (var id in actionStates)
                action.States.Add(new StateDefinition { Id = id });
            action.Transitions.Add(Edge("idle.move", "idle", "moving", "moving", 40));
            action.Transitions.Add(Edge("moving.idle", "moving", "idle", "idle", 10));
            action.Transitions.Add(Edge("idle.cast", "idle", "casting", "casting", 70));
            action.Transitions.Add(Edge("moving.cast", "moving", "casting", "casting", 70));
            action.Transitions.Add(Edge("casting.idle", "casting", "idle", "idle", 10));
            if (includeAttackSlots)
            {
                for (var slot = 1; slot <= 5; slot++)
                {
                    var attack = "attack.slot." + slot;
                    action.Transitions.Add(Edge("idle." + attack, "idle", attack,
                        "generic.attack.cast.slot." + slot, 80));
                    action.Transitions.Add(Edge("moving." + attack, "moving", attack,
                        "generic.attack.cast.slot." + slot, 80));
                    action.Transitions.Add(Edge(attack + ".idle", attack, "idle", "idle", 10));
                    action.Transitions.Add(Edge(attack + ".control", attack, "controlled", "controlled", 100));
                }
            }
            foreach (var source in new[] { "idle", "moving", "casting" })
                action.Transitions.Add(Edge(source + ".control", source, "controlled", "controlled", 100));
            action.Transitions.Add(Edge("controlled.idle", "controlled", "idle", "free.idle", 10));
            action.Transitions.Add(Edge("controlled.move", "controlled", "moving", "free.move", 40));
            var definition = new StateMachineDefinition
            {
                DefinitionId = Id,
                RootMachineId = life.Id,
                Machines = new List<MachineDefinition> { life, action },
                Parameters = CreateGenericParameters(),
            };
            MobaCharacterBindings.Attach(definition, new MobaCharacterBindings
            {
                DefinitionId = definition.DefinitionId,
                States = CreateGenericStateBindings(includeAttackSlots),
            });
            return definition;
        }

        private static List<MobaStateBinding> CreateGenericStateBindings(bool includeAttackSlots)
        {
            var states = new List<MobaStateBinding>
            {
                new MobaStateBinding { Path = "life/alive/action/casting", CastContext = "Current" },
            };
            if (includeAttackSlots)
                for (var slot = 1; slot <= 5; slot++)
                    states.Add(new MobaStateBinding
                    {
                        Path = "life/alive/action/attack.slot." + slot,
                        CastContext = "Current",
                        CastKind = "BasicAttack",
                    });
            return states;
        }

        private static StateMachineParameterDefinition NewWritableParameter(int id, string name) =>
            new StateMachineParameterDefinition
            {
                Id = id,
                Name = name,
                SupportedOperations = new List<int> { (int)ModifierOp.Override },
            };

        private static List<StateMachineParameterDefinition> CreateGenericParameters() =>
            new List<StateMachineParameterDefinition>
            {
                NewWritableParameter(MobaCharacterHfsmParameterService.Controlled, "Controlled"),
                NewWritableParameter(MobaCharacterHfsmParameterService.Casting, "Casting"),
                NewWritableParameter(MobaCharacterHfsmParameterService.Moving, "Moving"),
            };

        public static StateMachineDefinition ResolveDefinition(ITextAssetLoader loader, int heroId) =>
            LoadDefinition(loader, heroId);

        public static bool IsSupportedConditionKey(StateMachineDefinition definition, string key)
        {
            if (TryParseParameterCondition(key, out var id, out _, out _))
                return definition?.Parameters.Exists(x => x.Id == id) == true;
            return IsSupportedConditionKey(key) || MobaCharacterBindings.For(definition).Condition(key) != null;
        }

        public static RuntimeBindings<MobaCharacterHfsmFacts> CreateBindings(
            StateMachineDefinition definition, MobaCharacterHfsmParameterService parameters = null)
        {
            var result = new RuntimeBindings<MobaCharacterHfsmFacts>();
            var builtinConditions = new List<string> { "dead", "alive", "controlled", "casting", "active.skill.cast",
                "moving", "idle", "free.cast", "free.move", "free.idle" };
            for (var slot = 1; slot <= 5; slot++)
                builtinConditions.Add("generic.attack.cast.slot." + slot);
            foreach (var key in builtinConditions)
            {
                var factKey = key;
                result.RegisterCondition(key, () => new FactCondition(f => EvaluateFact(f, factKey)));
            }
            var conditions = new HashSet<string>(StringComparer.Ordinal);
            var actions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var machine in definition?.Machines ?? new List<MachineDefinition>())
            {
                foreach (var state in machine.States)
                {
                    RegisterStateTriggerActions(result, actions, state.LogicEntryActionKeys, true);
                    RegisterStateTriggerActions(result, actions, state.LogicExitActionKeys, false);
                }
                foreach (var edge in machine.Transitions)
                {
                    var key = edge.ConditionKey;
                    if (string.IsNullOrEmpty(key)) continue;
                    if (!IsSupportedConditionKey(definition, key))
                        throw new InvalidOperationException("Undeclared character condition: " + key);
                    if (!conditions.Add(key)) continue;
                    if (TryParseParameterCondition(key, out var id, out var comparison, out var value))
                        result.RegisterCondition(key, () => new ParameterCondition(id, comparison, value,
                            FindParameterDefault(definition, id)));
                    else if (MobaCharacterBindings.For(definition).Condition(key) is MobaConditionExpression expression)
                        result.RegisterCondition(key, () => new FactCondition(f =>
                            expression.Evaluate(f, definition, fact => EvaluateFact(f, fact))));
                }
            }
            return result;
        }

        internal static bool EvaluateFact(MobaCharacterHfsmFacts f, string key)
        {
            if (key != null && key.StartsWith("generic.attack.cast.slot.", StringComparison.Ordinal) &&
                int.TryParse(key.Substring("generic.attack.cast.slot.".Length), out var attackSlot))
                return IsCasting(f) && IsBasicAttackCast(f) && f.SkillSlot == attackSlot;
            switch (key)
            {
                case "dead": return !f.Alive;
                case "alive": return f.Alive;
                case "controlled": return IsControlled(f);
                case "casting": return IsCasting(f);
                case "basic.attack.cast": return IsCasting(f) && IsBasicAttackCast(f);
                case "active.skill.cast": return IsActiveSkillCast(f);
                case "moving": return IsMoving(f);
                case "idle": return IsIdle(f);
                case "free.cast": return !IsControlled(f) && IsCasting(f);
                case "free.move": return !IsControlled(f) && IsMoving(f);
                case "free.idle": return !IsControlled(f) && IsIdle(f);
                default: throw new InvalidOperationException("Unknown character fact: " + key);
            }
        }

        private static void RegisterStateTriggerActions(
            RuntimeBindings<MobaCharacterHfsmFacts> bindings,
            HashSet<string> registered,
            List<string> keys,
            bool entry)
        {
            if (keys == null) return;
            foreach (var key in keys)
            {
                if (!TryParseStateTriggerActionKey(key, out var triggerId, out var actionEntry) || actionEntry != entry)
                    throw new InvalidOperationException("Unsupported character HFSM logic action key: " + key);
                if (registered.Add(key))
                    bindings.RegisterStateAction(key, () => new TriggerStateLifecycleAction(triggerId, entry));
            }
        }

        private static bool TryParseStateTriggerActionKey(string key, out int triggerId, out bool entry)
        {
            triggerId = 0;
            entry = false;
            var parts = key?.Split('.');
            if (parts == null || parts.Length != 3 || parts[0] != "trigger" ||
                (parts[1] != "enter" && parts[1] != "exit") ||
                !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out triggerId) ||
                triggerId <= 0)
            {
                triggerId = 0;
                return false;
            }

            entry = parts[1] == "enter";
            return true;
        }

        public static bool IsSupportedStateActionKey(string key) =>
            TryParseStateTriggerActionKey(key, out _, out _);

        private sealed class TriggerStateLifecycleAction : IStateLifecycleAction<MobaCharacterHfsmFacts>
        {
            private readonly int _triggerId;
            private readonly bool _entry;

            public TriggerStateLifecycleAction(int triggerId, bool entry)
            { _triggerId = triggerId; _entry = entry; }

            public void OnEnter(MobaCharacterHfsmFacts owner, string stateId, in TickContext context)
            {
                if (_entry) owner.ExecuteLogicTrigger?.Invoke(owner.ActorId, _triggerId, stateId, context.Frame);
            }

            public void OnExit(MobaCharacterHfsmFacts owner, string stateId, in TickContext context)
            {
                if (!_entry) owner.ExecuteLogicTrigger?.Invoke(owner.ActorId, _triggerId, stateId, context.Frame);
            }
        }

        public static RuntimeBindings<MobaCharacterHfsmFacts> CreateBindings()
        {
            return CreateBindings(null, null);
        }

        private static bool TryParseParameterCondition(string key, out int parameterId,
            out string comparison, out float expected)
        {
            parameterId = 0;
            comparison = null;
            expected = 0f;
            if (string.IsNullOrEmpty(key)) return false;
            var parts = key.Split('.');
            return parts.Length == 4 && parts[0] == "parameter" &&
                   int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out parameterId) &&
                   MobaCharacterHfsmParameterService.IsValidParameterId(parameterId) && (comparison = parts[2]) != null &&
                   (comparison == "eq" || comparison == "ne" || comparison == "gt" ||
                    comparison == "ge" || comparison == "lt" || comparison == "le") &&
                   float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out expected) &&
                   !float.IsNaN(expected) && !float.IsInfinity(expected);
        }

        public static bool IsSupportedConditionKey(string key)
        {
            if (key != null && key.StartsWith("generic.attack.cast.slot.", StringComparison.Ordinal) &&
                int.TryParse(key.Substring("generic.attack.cast.slot.".Length), out var attackSlot))
                return attackSlot >= 1 && attackSlot <= 5;
            switch (key)
            {
                case "dead": case "alive": case "controlled": case "casting": case "moving": case "idle":
                case "active.skill.cast": case "free.cast": case "free.move": case "free.idle": return true;
                default: return TryParseParameterCondition(key, out _, out _, out _);
            }
        }

        private static float FindParameterDefault(StateMachineDefinition definition, int parameterId)
        {
            foreach (var parameter in definition?.Parameters ?? new List<StateMachineParameterDefinition>())
                if (parameter != null && parameter.Id == parameterId) return parameter.DefaultValue;
            return 0f;
        }

        private static bool IsControlled(MobaCharacterHfsmFacts f)
        {
            return f.Controlled || f.Parameters?.ResolveBool(f.ActorId,
                MobaCharacterHfsmParameterService.Controlled) == true;
        }

        private static bool IsCasting(MobaCharacterHfsmFacts f)
        {
            return !IsControlled(f) && (f.CastInstanceId > 0 ||
                f.Parameters?.ResolveBool(f.ActorId, MobaCharacterHfsmParameterService.Casting) == true);
        }

        private static bool IsActiveSkillCast(MobaCharacterHfsmFacts f)
        {
            return IsCasting(f) && !IsBasicAttackCast(f);
        }

        private static bool IsBasicAttackCast(MobaCharacterHfsmFacts f)
        {
            var kind = f.Bindings?.Cast(f.SkillId)?.CastKind ?? "LoadoutSlot";
            if (kind == "BasicAttack") return true;
            if (kind == "Active") return false;
            return f.SkillSlot > 0 && f.BasicAttackSlot > 0 && f.SkillSlot == f.BasicAttackSlot;
        }

        private static bool IsMoving(MobaCharacterHfsmFacts f)
        {
            return !IsControlled(f) && !IsCasting(f) && (f.Moving ||
                f.Parameters?.ResolveBool(f.ActorId, MobaCharacterHfsmParameterService.Moving) == true);
        }

        private static bool IsIdle(MobaCharacterHfsmFacts f)
        {
            return !IsControlled(f) && !IsCasting(f) && !IsMoving(f);
        }

        private sealed class ParameterCondition : ITransitionCondition<MobaCharacterHfsmFacts>
        {
            private readonly int _parameterId;
            private readonly string _comparison;
            private readonly float _expected;
            private readonly float _defaultValue;
            public ParameterCondition(int parameterId, string comparison, float expected)
                : this(parameterId, comparison, expected, 0f) { }
            public ParameterCondition(int parameterId, string comparison, float expected, float defaultValue)
            { _parameterId = parameterId; _comparison = comparison; _expected = expected; _defaultValue = defaultValue; }
            public bool Evaluate(MobaCharacterHfsmFacts owner, in TransitionContext context)
            {
                var value = owner.Parameters?.Resolve(owner.ActorId, _parameterId, _defaultValue) ?? _defaultValue;
                switch (_comparison)
                {
                    case "eq": return value == _expected;
                    case "ne": return value != _expected;
                    case "gt": return value > _expected;
                    case "ge": return value >= _expected;
                    case "lt": return value < _expected;
                    case "le": return value <= _expected;
                    default: return false;
                }
            }
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

    public sealed class MobaCharacterHfsmProfileCatalog
    {
        private readonly ITextAssetLoader _loader;
        private readonly Dictionary<int, StateMachineDefinition> _definitions =
            new Dictionary<int, StateMachineDefinition>();

        public MobaCharacterHfsmProfileCatalog(ITextAssetLoader loader)
        {
            _loader = loader;
        }

        public StateMachineDefinition Resolve(int heroId)
        {
            if (heroId > 0 && _definitions.TryGetValue(heroId, out var definition)) return definition;
            if (heroId > 0)
            {
                definition = MobaCharacterHfsmProfile.ResolveDefinition(_loader, heroId);
                _definitions[heroId] = definition;
                return definition;
            }
            return MobaCharacterHfsmProfile.CreateGenericDefinition();
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
        public readonly string ModeId;
        public readonly long ModeInstanceId;
        public readonly int ModeStartFrame;
        public readonly int ModeLocalFrame;

        public MobaCharacterActionState(string path, long instanceId, int startFrame, int localFrame,
            long castInstanceId, int skillId)
            : this(path, instanceId, startFrame, localFrame, castInstanceId, skillId,
                MobaCharacterModeState.NormalModeId, 0L, 0, 0) { }

        public MobaCharacterActionState(string path, long instanceId, int startFrame, int localFrame,
            long castInstanceId, int skillId, string modeId, long modeInstanceId,
            int modeStartFrame, int modeLocalFrame)
        {
            Path = path ?? string.Empty;
            InstanceId = instanceId;
            StartFrame = startFrame;
            LocalFrame = localFrame;
            CastInstanceId = castInstanceId;
            SkillId = skillId;
            ModeId = modeId ?? string.Empty;
            ModeInstanceId = modeInstanceId;
            ModeStartFrame = modeStartFrame;
            ModeLocalFrame = modeLocalFrame;
        }
    }

    public sealed class MobaCharacterFactSnapshot
    {
        public bool Alive, Controlled, Moving;
        public long CastInstanceId;
        public int SkillId, SkillSlot, BasicAttackSlot;
        public float AimX, AimY, AimZ, DirectionX, DirectionY, DirectionZ;
    }

    public sealed class MobaCharacterHfsmSnapshot
    {
        public RuntimeSnapshot Machine;
        public MobaCharacterFactSnapshot Facts;
        public MobaCharacterActionState Action;
        public MobaCharacterModeSnapshot Mode;
        public string BindingsFingerprint;
        public string AimScope;
        public long AimScopeInstanceId;
        public uint NextAimScopeSequence;
        public bool HasFrozenAim;
        public Vec3 FrozenAimPos;
        public Vec3 FrozenAimDir;
    }

    public sealed class MobaCharacterHfsmRuntime : IDisposable, IRuntimeInspectionName
    {
        private readonly int _actorId;
        private readonly StateMachineDefinition _definition;
        private readonly MobaCharacterHfsmFacts _facts = new MobaCharacterHfsmFacts();
        private readonly StateMachineRuntime<MobaCharacterHfsmFacts> _machine;
        private readonly MobaCharacterModeRuntime _mode;
        private readonly bool _registeredForInspection;
        private MobaCharacterActionState _action;
        private readonly MobaCharacterBindings _bindings;
        private string _aimScope;
        private long _aimScopeInstanceId;
        private uint _nextAimScopeSequence = 1;
        private readonly string _bindingsFingerprint;
        private bool _hasFrozenAim;
        private Vec3 _frozenAimPos;
        private Vec3 _frozenAimDir;
        private bool _disposed;
        private bool _reevaluatingCurrentFrame;
        private bool _reevaluateRequested;
        private bool _machineTicking;

        public MobaCharacterHfsmRuntime(int actorId, StateMachineDefinition definition, int frame, Fixed64 time,
            bool registerForInspection = true, MobaCharacterHfsmParameterService parameters = null,
            Action<int, int, string, int> executeLogicTrigger = null)
        {
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            _actorId = actorId;
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _bindings = MobaCharacterBindings.For(definition);
            _facts.Bindings = _bindings;
            _bindingsFingerprint = _bindings.Fingerprint;
            _facts.ActorId = actorId;
            _facts.Parameters = parameters;
            _facts.ExecuteLogicTrigger = executeLogicTrigger;
            _registeredForInspection = registerForInspection;
            _mode = new MobaCharacterModeRuntime(actorId, frame);
            _machine = new StateMachineRuntime<MobaCharacterHfsmFacts>(_facts, definition,
                MobaCharacterHfsmProfile.CreateBindings(definition, parameters));
            _facts.Alive = true;
            _facts.ModeId = _mode.Current.ModeId;
            _facts.ModeInstanceId = _mode.Current.InstanceId;
            _machine.Initialize(frame, time);
            if (_registeredForInspection) RuntimeInspectionHub.Register(InspectionName, _machine);
            UpdateAction(frame);
        }

        public long DefinitionHash => _machine.DefinitionHash;
        public MobaDeathCastBinding DeathCasting => _bindings.DeathCasting;
        public string InspectionName => "MobaHero/Actor_" + _actorId;
        public int Frame => _machine.CurrentFrame;
        public Fixed64 Time => _machine.CurrentTime;
        public MobaCharacterActionState Action => _action;
        public MobaCharacterModeState Mode => _mode.Current;
        public float ResolveParameter(int parameterId) => _facts.Parameters?.Resolve(_actorId, parameterId) ?? 0f;
        public long FactCastInstanceId => _facts.CastInstanceId;
        public int FactSkillId => _facts.SkillId;
        public bool FactIsCasting => _facts.CastInstanceId > 0 ||
            _facts.Parameters?.ResolveBool(_actorId, MobaCharacterHfsmParameterService.Casting) == true;

        /// <summary>
        /// Synchronizes cast ownership before a same-frame parameter
        /// reevaluation. Trigger actions can run during a pipeline commit,
        /// before the world system mirrors the running cast into ECS.
        /// </summary>
        public void SyncCastContext(long castInstanceId, int skillId)
        {
            SyncCastContext(castInstanceId, skillId, 0, 0);
        }

        public void SyncCastContext(long castInstanceId, int skillId, int skillSlot, int basicAttackSlot)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MobaCharacterHfsmRuntime));
            _facts.CastInstanceId = castInstanceId > 0 ? castInstanceId : 0L;
            _facts.SkillId = castInstanceId > 0 && skillId > 0 ? skillId : 0;
            _facts.SkillSlot = castInstanceId > 0 ? skillSlot : 0;
            _facts.BasicAttackSlot = basicAttackSlot > 0 ? basicAttackSlot : 0;
        }

        /// <summary>
        /// Direction captured when a sustained aimed mode entered its aiming state.
        /// Short child casts may update their own input, but the mode keeps this
        /// authoritative ray until the mode exits.
        /// </summary>
        public bool TryGetFrozenAim(out Vec3 aimPos, out Vec3 aimDir)
        {
            aimPos = _frozenAimPos;
            aimDir = _frozenAimDir;
            return _hasFrozenAim && !aimDir.Equals(Vec3.Zero);
        }

        public MobaCastBinding CastBinding(int skillId) => _bindings.Cast(skillId);
        public void SyncAim(in Vec3 position, in Vec3 direction)
        { _facts.AimPos = position; _facts.AimDir = direction; }

        public bool TryGetAimSnapshot(string scope, out Vec3 aimPos, out Vec3 aimDir)
        {
            var valid = TryGetFrozenAim(out aimPos, out aimDir);
            return valid && string.Equals(_aimScope, scope, StringComparison.Ordinal);
        }

        private void UpdateAimScope(int frame, in Vec3 aimPos, in Vec3 aimDir)
        {
            var state = _bindings.State(_action.Path);
            var scope = state?.AimScope;
            if (string.IsNullOrEmpty(scope) || !_facts.Alive || MobaCharacterHfsmProfile.EvaluateFact(_facts, "controlled"))
            { ClearFrozenAim(); return; }
            if (!string.Equals(scope, _aimScope, StringComparison.Ordinal))
            {
                ClearFrozenAim();
                _aimScope = scope;
                if (_nextAimScopeSequence == 0) throw new InvalidOperationException("Aim scope identity exhausted.");
                _aimScopeInstanceId = ((long)_actorId << 32) | _nextAimScopeSequence++;
            }
            if (!_hasFrozenAim && state.CaptureAim && !aimDir.Equals(Vec3.Zero))
            {
                _frozenAimPos = aimPos;
                _frozenAimDir = aimDir;
                _hasFrozenAim = true;
            }
        }

        public void ClearFrozenAim()
        {
            _hasFrozenAim = false;
            _aimScope = null;
            _aimScopeInstanceId = 0;
            _frozenAimPos = Vec3.Zero;
            _frozenAimDir = Vec3.Zero;
        }

        public bool RequestModeEnter(string modeId, int priority, long ownerInstanceId = 0L,
            int requestedFrame = -1) => _mode.RequestEnter(modeId, priority, ownerInstanceId, requestedFrame);

        public bool RequestModeExit(string modeId, long ownerInstanceId = 0L,
            int requestedFrame = -1,
            MobaCharacterModeTransitionReason reason = MobaCharacterModeTransitionReason.ExitRequested) =>
            _mode.RequestExit(modeId, ownerInstanceId, requestedFrame, reason);

        public bool RequestModeInterrupt(string modeId, long ownerInstanceId = 0L,
            int requestedFrame = -1,
            MobaCharacterModeTransitionReason reason = MobaCharacterModeTransitionReason.Interrupted) =>
            _mode.RequestInterrupt(modeId, ownerInstanceId, requestedFrame, reason);

        public bool RequestResetToNormal(int requestedFrame,
            MobaCharacterModeTransitionReason reason) =>
            _mode.RequestResetToNormal(requestedFrame, reason);

        public void Tick(int frame, Fixed64 time, bool alive, bool controlled, bool moving,
            long castInstanceId, int skillId)
        {
            Tick(frame, time, alive, controlled, moving, castInstanceId, skillId, 0, 0,
                Vec3.Zero, Vec3.Zero);
        }

        public void Tick(int frame, Fixed64 time, bool alive, bool controlled, bool moving,
            long castInstanceId, int skillId, int skillSlot, int basicAttackSlot)
        {
            Tick(frame, time, alive, controlled, moving, castInstanceId, skillId,
                skillSlot, basicAttackSlot, Vec3.Zero, Vec3.Zero);
        }

        public void Tick(int frame, Fixed64 time, bool alive, bool controlled, bool moving,
            long castInstanceId, int skillId, in Vec3 aimPos, in Vec3 aimDir)
        {
            Tick(frame, time, alive, controlled, moving, castInstanceId, skillId, 0, 0,
                in aimPos, in aimDir);
        }

        public void Tick(int frame, Fixed64 time, bool alive, bool controlled, bool moving,
            long castInstanceId, int skillId, int skillSlot, int basicAttackSlot,
            in Vec3 aimPos, in Vec3 aimDir)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MobaCharacterHfsmRuntime));
            if (frame <= Frame) return;
            _facts.Alive = alive;
            _facts.Controlled = controlled;
            _facts.Moving = moving;
            _facts.CastInstanceId = castInstanceId;
            _facts.SkillId = skillId;
            _facts.SkillSlot = castInstanceId > 0 ? skillSlot : 0;
            _facts.BasicAttackSlot = basicAttackSlot > 0 ? basicAttackSlot : 0;
            _facts.AimPos = aimPos;
            _facts.AimDir = aimDir;
            UpdateTemporalFacts(frame);
            if (!alive)
                _mode.RequestResetToNormal(frame, MobaCharacterModeTransitionReason.Death);
            else if (controlled)
                _mode.RequestResetToNormal(frame, MobaCharacterModeTransitionReason.Controlled);
            _mode.Advance(frame);
            _facts.ModeId = _mode.Current.ModeId;
            _facts.ModeInstanceId = _mode.Current.InstanceId;
            _machineTicking = true;
            try
            {
                _machine.Tick(frame, time);
            }
            finally
            {
                _machineTicking = false;
            }
            UpdateAction(frame);
            if (_reevaluateRequested)
                ReevaluateCurrentFrame();

            UpdateAimScope(frame, in aimPos, in aimDir);

        }

        /// <summary>
        /// Re-evaluates parameter-driven transitions at the current simulation frame.
        /// Gameplay effects may commit after the regular character HFSM tick; this
        /// keeps state-owned logic actions and slot projections observable immediately
        /// without advancing the deterministic clock.
        /// </summary>
        public void ReevaluateCurrentFrame()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MobaCharacterHfsmRuntime));
            // Logic entry/exit actions can synchronously execute plans that write another
            // HFSM parameter. Do not re-enter the same machine while its transition is
            // still dispatching lifecycle actions; a nested pass can observe the source
            // state before the outer transition has completed and immediately undo it.
            if (_reevaluatingCurrentFrame || _machineTicking)
            {
                // A state entry/exit trigger may write another HFSM parameter while
                // the current transition is dispatching. Preserve that request and
                // run one more same-frame pass after the outer transition completes;
                // otherwise the nested write can leave the machine in the previous
                // slot state until an unrelated future tick.
                _reevaluateRequested = true;
                return;
            }
            _reevaluatingCurrentFrame = true;
            try
            {
                UpdateTemporalFacts(_machine.CurrentFrame);
                var passes = 0;
                do
                {
                    _reevaluateRequested = false;
                    _machine.ReevaluateCurrentFrame();
                    UpdateAction(_machine.CurrentFrame);
                    UpdateAimScope(_machine.CurrentFrame, _facts.AimPos, _facts.AimDir);
                }
                while (_reevaluateRequested && ++passes < 8);
            }
            finally
            {
                _reevaluatingCurrentFrame = false;
                _reevaluateRequested = false;
            }
        }

        private void UpdateTemporalFacts(int frame)
        {
            _facts.CurrentFrame = frame;
            _facts.ActionLocalFrame = Math.Max(0, frame - _action.StartFrame);
        }

        public MobaCharacterHfsmSnapshot CaptureSnapshot() => new MobaCharacterHfsmSnapshot
        {
            Machine = _machine.CaptureSnapshot(), Action = _action,
            Facts = new MobaCharacterFactSnapshot {
                Alive = _facts.Alive, Controlled = _facts.Controlled, Moving = _facts.Moving,
                CastInstanceId = _facts.CastInstanceId, SkillId = _facts.SkillId,
                SkillSlot = _facts.SkillSlot, BasicAttackSlot = _facts.BasicAttackSlot,
                AimX = _facts.AimPos.X, AimY = _facts.AimPos.Y, AimZ = _facts.AimPos.Z,
                DirectionX = _facts.AimDir.X, DirectionY = _facts.AimDir.Y, DirectionZ = _facts.AimDir.Z },
            Mode = _mode.CaptureSnapshot(), HasFrozenAim = _hasFrozenAim,
            FrozenAimPos = _frozenAimPos, FrozenAimDir = _frozenAimDir,
            AimScope = _aimScope, AimScopeInstanceId = _aimScopeInstanceId,
            NextAimScopeSequence = _nextAimScopeSequence,
            BindingsFingerprint = _bindingsFingerprint
        };

        public void RestoreSnapshot(MobaCharacterHfsmSnapshot snapshot)
        {
            if (snapshot?.Machine == null) throw new ArgumentNullException(nameof(snapshot));
            var mode = snapshot.Mode ?? CreateLegacyNormalModeSnapshot(snapshot.Machine.Frame);
            var path = ActivePath(snapshot.Machine, _definition);
            var action = snapshot.Action;
            if (snapshot.Mode == null)
                action = new MobaCharacterActionState(action.Path, action.InstanceId, action.StartFrame,
                    action.LocalFrame, action.CastInstanceId, action.SkillId, mode.Current.ModeId,
                    mode.Current.InstanceId, mode.Current.StartFrame, mode.Current.LocalFrame);
            if (snapshot.Machine.DefinitionHash != DefinitionHash || !snapshot.Machine.Initialized ||
                !string.Equals(path, action.Path, StringComparison.Ordinal) ||
                action.StartFrame < 0 || action.StartFrame > snapshot.Machine.Frame ||
                action.LocalFrame != snapshot.Machine.Frame - action.StartFrame ||
                action.InstanceId != (((long)_actorId << 32) | (uint)action.StartFrame) ||
                ((action.CastInstanceId == 0) != (action.SkillId == 0)) ||
                (StateCastPolicy(path) == "None" &&
                    (action.CastInstanceId != 0 || action.SkillId != 0)) ||
                mode.Frame != snapshot.Machine.Frame ||
                !string.Equals(action.ModeId, mode.Current.ModeId, StringComparison.Ordinal) ||
                action.ModeInstanceId != mode.Current.InstanceId ||
                action.ModeStartFrame != mode.Current.StartFrame ||
                action.ModeLocalFrame != mode.Current.LocalFrame)
                throw new InvalidOperationException("Character HFSM action does not match the restored machine.");
            if (snapshot.BindingsFingerprint != _bindingsFingerprint)
                throw new InvalidOperationException("Character binding snapshot fingerprint mismatch.");
            var expectedScope = _bindings.State(path)?.AimScope;
            if (snapshot.NextAimScopeSequence == 0 ||
                (string.IsNullOrEmpty(expectedScope) ? !string.IsNullOrEmpty(snapshot.AimScope) || snapshot.AimScopeInstanceId != 0 || snapshot.HasFrozenAim :
                    snapshot.AimScope != expectedScope || (uint)snapshot.AimScopeInstanceId == 0 ||
                    (snapshot.AimScopeInstanceId >> 32) != _actorId || (uint)snapshot.AimScopeInstanceId >= snapshot.NextAimScopeSequence) ||
                (snapshot.HasFrozenAim && (snapshot.FrozenAimDir.Equals(Vec3.Zero) ||
                    !Finite(snapshot.FrozenAimPos) || !Finite(snapshot.FrozenAimDir))))
                throw new InvalidOperationException("Invalid restored aim scope.");
            var restoredFacts = snapshot.Facts;
            if (restoredFacts == null || restoredFacts.CastInstanceId < 0 ||
                ((restoredFacts.CastInstanceId == 0) != (restoredFacts.SkillId == 0)) ||
                restoredFacts.SkillSlot < 0 || restoredFacts.BasicAttackSlot < 0 ||
                !Finite(new Vec3(restoredFacts.AimX, restoredFacts.AimY, restoredFacts.AimZ)) ||
                !Finite(new Vec3(restoredFacts.DirectionX, restoredFacts.DirectionY, restoredFacts.DirectionZ)))
                throw new InvalidOperationException("Invalid restored character facts.");
            _facts.Alive = restoredFacts.Alive; _facts.Controlled = restoredFacts.Controlled; _facts.Moving = restoredFacts.Moving;
            _facts.CastInstanceId = restoredFacts.CastInstanceId; _facts.SkillId = restoredFacts.SkillId;
            _facts.SkillSlot = restoredFacts.SkillSlot; _facts.BasicAttackSlot = restoredFacts.BasicAttackSlot;
            _facts.AimPos = new Vec3(restoredFacts.AimX, restoredFacts.AimY, restoredFacts.AimZ);
            _facts.AimDir = new Vec3(restoredFacts.DirectionX, restoredFacts.DirectionY, restoredFacts.DirectionZ);
            _facts.CurrentFrame = snapshot.Machine.Frame;
            _facts.ActionLocalFrame = action.LocalFrame;
            _facts.ModeId = mode.Current.ModeId; _facts.ModeInstanceId = mode.Current.InstanceId;
            _nextAimScopeSequence = snapshot.NextAimScopeSequence;
            _mode.RestoreSnapshot(mode);
            _machine.RestoreSnapshot(snapshot.Machine);
            _action = action;
            _aimScope = snapshot.AimScope;
            _aimScopeInstanceId = snapshot.AimScopeInstanceId;
            _hasFrozenAim = snapshot.HasFrozenAim;
            _frozenAimPos = snapshot.FrozenAimPos;
            _frozenAimDir = snapshot.FrozenAimDir;
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
            var state = _bindings.State(path);
            var policy = state?.CastContext ?? "None";
            var kindMatches = state == null || state.CastKind == "Any" ||
                (state.CastKind == "BasicAttack" && MobaCharacterHfsmProfile.EvaluateFact(_facts, "basic.attack.cast")) ||
                (state.CastKind == "Active" && MobaCharacterHfsmProfile.EvaluateFact(_facts, "active.skill.cast"));
            var cast = policy != "None" && kindMatches ? _facts.CastInstanceId : 0L;
            var skill = cast > 0 ? _facts.SkillId : 0;
            if (policy == "RetainCommitted" && cast <= 0 &&
                string.Equals(path, _action.Path, StringComparison.Ordinal) && _action.CastInstanceId > 0)
            {
                cast = _action.CastInstanceId;
                skill = _action.SkillId;
            }

            var mode = _mode.Current;
            if (!string.Equals(path, _action.Path, StringComparison.Ordinal) || cast != _action.CastInstanceId)
            {
                // The actor/frame identity is stable across rollback and independent of allocation order.
                var instanceId = ((long)_actorId << 32) | (uint)frame;
                _action = new MobaCharacterActionState(path, instanceId, frame, 0, cast,
                    skill, mode.ModeId, mode.InstanceId,
                    mode.StartFrame, mode.LocalFrame);
            }
            else
            {
                _action = new MobaCharacterActionState(path, _action.InstanceId, _action.StartFrame,
                    frame - _action.StartFrame, cast, _action.SkillId, mode.ModeId,
                    mode.InstanceId, mode.StartFrame, mode.LocalFrame);
            }
        }

        private static bool Finite(Vec3 v) =>
            !float.IsNaN(v.X) && !float.IsInfinity(v.X) && !float.IsNaN(v.Y) && !float.IsInfinity(v.Y) && !float.IsNaN(v.Z) && !float.IsInfinity(v.Z);

        private string StateCastPolicy(string path) => _bindings.State(path)?.CastContext ?? "None";

        private MobaCharacterModeSnapshot CreateLegacyNormalModeSnapshot(int frame)
        {
            var instanceId = ((long)_actorId << 32) | 1u;
            return new MobaCharacterModeSnapshot
            {
                Frame = frame,
                NextInstanceSequence = 2,
                Current = new MobaCharacterModeState(
                    MobaCharacterModeState.NormalModeId, instanceId, 0L, 0, 0, frame,
                    MobaCharacterModeTransitionReason.Entered)
            };
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (_machine.IsInitialized && !_machine.IsFaulted) _machine.Shutdown();
            }
            finally
            {
                if (_registeredForInspection) RuntimeInspectionHub.Unregister(_machine);
            }
        }
    }
}
