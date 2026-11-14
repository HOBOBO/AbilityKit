using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using AbilityKit.Ability.Config;
using AbilityKit.HFSM.Definition;
using Newtonsoft.Json;

namespace AbilityKit.Demo.Moba.Services.StateMachine
{
    /// <summary>Project-side declarations; the HFSM package only sees registered conditions.</summary>
    public sealed class MobaCharacterBindings
    {
        public const string ResourcePath = "moba/character_bindings";
        private static readonly ConditionalWeakTable<StateMachineDefinition, MobaCharacterBindings> ByDefinition =
            new ConditionalWeakTable<StateMachineDefinition, MobaCharacterBindings>();
        public string DefinitionId { get; set; }
        public List<MobaConditionBinding> Conditions { get; set; } = new List<MobaConditionBinding>();
        public List<MobaStateBinding> States { get; set; } = new List<MobaStateBinding>();
        public List<MobaCastBinding> Casts { get; set; } = new List<MobaCastBinding>();
        public MobaDeathCastBinding DeathCasting { get; set; }
        [JsonIgnore]
        public string Fingerprint
        {
            get
            {
                using (var hash = SHA256.Create())
                    return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this))));
            }
        }

        public static MobaCharacterBindings For(StateMachineDefinition definition) =>
            definition != null && ByDefinition.TryGetValue(definition, out var bindings)
                ? bindings : new MobaCharacterBindings();

        public static void Attach(StateMachineDefinition definition, MobaCharacterBindings bindings)
        {
            if (definition == null || bindings == null) throw new ArgumentNullException();
            bindings.Validate(definition);
            ByDefinition.Remove(definition);
            ByDefinition.Add(definition, JsonConvert.DeserializeObject<MobaCharacterBindings>(JsonConvert.SerializeObject(bindings)));
        }

        public static StateMachineDefinition Load(StateMachineDefinition definition, ITextAssetLoader loader)
        {
            if (loader != null && loader.TryLoadText(ResourcePath, out var json) && !string.IsNullOrWhiteSpace(json))
            {
                var all = JsonConvert.DeserializeObject<MobaCharacterBindings[]>(json) ?? Array.Empty<MobaCharacterBindings>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in all)
                    if (item == null || string.IsNullOrWhiteSpace(item.DefinitionId) || !seen.Add(item.DefinitionId))
                        throw new InvalidOperationException("Character binding definition ids must be unique.");
                var binding = all.SingleOrDefault(x => x.DefinitionId == definition.DefinitionId);
                if (binding != null) Attach(definition, binding);
            }
            return definition;
        }

        public MobaStateBinding State(string path) => States.Find(x => x.Path == path);
        public MobaCastBinding Cast(int skillId) => Casts.Find(x => x.SkillId == skillId);
        public MobaConditionExpression Condition(string key) => Conditions.Find(x => x.Key == key)?.Expression;

        public void Validate(StateMachineDefinition definition)
        {
            if (definition == null || Conditions == null || States == null || Casts == null ||
                DefinitionId != definition.DefinitionId)
                throw new InvalidOperationException("Character binding definition mismatch.");
            var ids = new HashSet<int>(definition.Parameters.Select(x => x.Id));
            DeathCasting?.Validate();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var condition in Conditions)
            {
                if (condition == null || string.IsNullOrWhiteSpace(condition.Key) || !keys.Add(condition.Key) ||
                    MobaCharacterHfsmProfile.IsSupportedConditionKey(condition.Key))
                    throw new InvalidOperationException("Invalid or duplicate character condition binding.");
                var count = 0;
                condition.Expression?.Validate(ids, 0, ref count);
                if (condition.Expression == null) throw new InvalidOperationException("Missing character condition expression.");
            }
            var paths = new HashSet<string>(StringComparer.Ordinal);
            CollectPaths(definition, definition.RootMachineId, string.Empty, paths, new HashSet<string>());
            keys.Clear();
            foreach (var state in States)
                if (state == null || !paths.Contains(state.Path) || !keys.Add(state.Path) ||
                    (state.CastContext != "None" && state.CastContext != "Current" && state.CastContext != "RetainCommitted") ||
                    (state.CastKind != "Any" && state.CastKind != "BasicAttack" && state.CastKind != "Active") ||
                    (state.CaptureAim && string.IsNullOrWhiteSpace(state.AimScope)))
                    throw new InvalidOperationException("Invalid character state binding.");
            keys.Clear();
            foreach (var cast in Casts)
            {
                if (cast == null || cast.ParameterSnapshots == null || cast.SkillId <= 0 || !keys.Add(cast.SkillId.ToString()) ||
                    (cast.CastKind != "LoadoutSlot" && cast.CastKind != "BasicAttack" && cast.CastKind != "Active") ||
                    (cast.AimSource != "Input" && cast.AimSource != "StateAimSnapshot") ||
                    (cast.AimSource == "StateAimSnapshot" && !States.Exists(x => x.AimScope == cast.AimScope && x.CaptureAim)))
                    throw new InvalidOperationException("Invalid character cast binding.");
                var targets = new HashSet<int>();
                foreach (var sample in cast.ParameterSnapshots)
                    if (sample == null || (sample.SourceParameterId != 0 && !ids.Contains(sample.SourceParameterId)) ||
                        float.IsNaN(sample.Value) || float.IsInfinity(sample.Value) || !ids.Contains(sample.TargetParameterId) ||
                        !targets.Add(sample.TargetParameterId) ||
                        !definition.Parameters.Exists(x => x.Id == sample.TargetParameterId && x.SupportedOperations.Contains((int)AbilityKit.Modifiers.ModifierOp.Override)))
                        throw new InvalidOperationException("Invalid cast parameter snapshot binding.");
            }
        }

        private static void CollectPaths(StateMachineDefinition definition, string machineId, string prefix,
            HashSet<string> paths, HashSet<string> visited)
        {
            if (!visited.Add(machineId)) throw new InvalidOperationException("Cyclic character state topology.");
            var machine = definition.Machines.Find(x => x.Id == machineId);
            if (machine == null) throw new InvalidOperationException("Missing character state machine.");
            foreach (var state in machine.States)
            {
                var path = prefix + machineId + "/" + state.Id;
                paths.Add(path);
                if (!string.IsNullOrEmpty(state.ChildMachineId))
                    CollectPaths(definition, state.ChildMachineId, path + "/", paths, visited);
            }
            visited.Remove(machineId);
        }
    }

    public sealed class MobaConditionBinding
    {
        public string Key { get; set; }
        public MobaConditionExpression Expression { get; set; }
    }

    public sealed class MobaConditionExpression
    {
        public string Fact { get; set; }
        public int ParameterId { get; set; }
        public string Comparison { get; set; } = "eq";
        public float Value { get; set; }
        public List<MobaConditionExpression> All { get; set; }
        public List<MobaConditionExpression> Any { get; set; }
        public MobaConditionExpression Not { get; set; }

        internal void Validate(HashSet<int> parameters, int depth, ref int count)
        {
            if (depth > 8 || ++count > 64) throw new InvalidOperationException("Character condition exceeds bounds.");
            var kinds = (Fact != null ? 1 : 0) + (ParameterId != 0 ? 1 : 0) + (All != null ? 1 : 0) +
                (Any != null ? 1 : 0) + (Not != null ? 1 : 0);
            if (kinds != 1 || float.IsNaN(Value) || float.IsInfinity(Value))
                throw new InvalidOperationException("Invalid character condition expression.");
            if (Fact != null && Fact != "alive" && Fact != "dead" && Fact != "controlled" && Fact != "casting" &&
                Fact != "basic.attack.cast" && Fact != "active.skill.cast" && Fact != "moving" && Fact != "idle")
                throw new InvalidOperationException("Unknown character fact: " + Fact);
            if (ParameterId != 0 && (!parameters.Contains(ParameterId) ||
                (Comparison != "eq" && Comparison != "ne" && Comparison != "lt" && Comparison != "le" && Comparison != "gt" && Comparison != "ge")))
                throw new InvalidOperationException("Invalid character parameter condition.");
            if (All != null || Any != null)
            {
                var children = All ?? Any;
                if (children.Count == 0) throw new InvalidOperationException("Empty character condition group.");
                foreach (var child in children)
                {
                    if (child == null) throw new InvalidOperationException("Null character condition.");
                    child.Validate(parameters, depth + 1, ref count);
                }
            }
            Not?.Validate(parameters, depth + 1, ref count);
        }

        internal bool Evaluate(MobaCharacterHfsmFacts facts, StateMachineDefinition definition, Func<string, bool> fact)
        {
            if (Fact != null) return fact(Fact);
            if (Not != null) return !Not.Evaluate(facts, definition, fact);
            if (All != null) return All.TrueForAll(x => x.Evaluate(facts, definition, fact));
            if (Any != null) return Any.Exists(x => x.Evaluate(facts, definition, fact));
            var fallback = definition.Parameters.Find(x => x.Id == ParameterId)?.DefaultValue ?? 0f;
            var value = facts.Parameters?.Resolve(facts.ActorId, ParameterId, fallback) ?? fallback;
            switch (Comparison)
            {
                case "eq": return value == Value;
                case "ne": return value != Value;
                case "lt": return value < Value;
                case "le": return value <= Value;
                case "gt": return value > Value;
                case "ge": return value >= Value;
                default: throw new InvalidOperationException("Invalid character parameter comparison.");
            }
        }
    }

    public sealed class MobaStateBinding
    {
        public string Path { get; set; }
        public string CastContext { get; set; } = "None";
        public string CastKind { get; set; } = "Any";
        public string AimScope { get; set; }
        public bool CaptureAim { get; set; }
    }

    public sealed class MobaCastBinding
    {
        public int SkillId { get; set; }
        public string CastKind { get; set; } = "LoadoutSlot";
        public string AimSource { get; set; } = "Input";
        public string AimScope { get; set; }
        public List<MobaParameterSnapshotBinding> ParameterSnapshots { get; set; } = new List<MobaParameterSnapshotBinding>();
    }

    public sealed class MobaParameterSnapshotBinding
    {
        public int SourceParameterId { get; set; }
        public int TargetParameterId { get; set; }
        /// <summary>Explicit selected-candidate value when no source parameter is used.</summary>
        public float Value { get; set; }
    }

    public readonly struct MobaPreparedParameterSnapshot
    {
        public readonly int TargetParameterId;
        public readonly float Value;
        public MobaPreparedParameterSnapshot(int targetParameterId, float value)
        { TargetParameterId = targetParameterId; Value = value; }
    }
}
