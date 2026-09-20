using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Config.Authoring
{
    public static class TriggerAuthoringSchema
    {
        public const string Id = "abilitykit-trigger-authoring";
        public const string Version = "2.2";
    }

    public enum TriggerModuleKind
    {
        Ability = 0,
        Buff = 1,
        Passive = 2,
        Projectile = 3,
        Summon = 4,
        Custom = 100
    }

    public enum TriggerNodeKind
    {
        Condition = 0,
        Action = 1
    }

    public enum TriggerEntryMode
    {
        Event = 0,
        Callable = 1
    }

    public enum TriggerCallableParameterDirection
    {
        Input = 0,
        Output = 1
    }

    public enum TriggerValueType
    {
        None = 0,
        Integer = 1,
        Number = 2,
        Boolean = 3,
        String = 4,
        Entity = 5,
        ObjectId = 6,
        IntegerList = 7,
        Vector3 = 8,
        Object = 9
    }

    public enum TriggerValueSource
    {
        Constant = 0,
        Payload = 1,
        Context = 2,
        LocalBlackboard = 3,
        GlobalBlackboard = 4,
        TemplateParameter = 5,
        Expression = 6
    }

    [Flags]
    public enum TriggerTemplateValueSourceMask
    {
        None = 0,
        Constant = 1 << (int)TriggerValueSource.Constant,
        Payload = 1 << (int)TriggerValueSource.Payload,
        Context = 1 << (int)TriggerValueSource.Context,
        LocalBlackboard = 1 << (int)TriggerValueSource.LocalBlackboard,
        GlobalBlackboard = 1 << (int)TriggerValueSource.GlobalBlackboard,
        TemplateParameter = 1 << (int)TriggerValueSource.TemplateParameter,
        Expression = 1 << (int)TriggerValueSource.Expression,
        InstanceBinding = Constant | Payload | Context | LocalBlackboard | GlobalBlackboard | Expression,
        All = InstanceBinding | TemplateParameter
    }

    public enum TriggerEventMatchMode
    {
        Exact = 0,
        Prefix = 1
    }

    [Serializable]
    public sealed class TriggerAuthoringSourceDocument
    {
        public string Schema = TriggerAuthoringSchema.Id;
        public string Version = TriggerAuthoringSchema.Version;
        public TriggerAuthoringSourceMetadata Metadata = new TriggerAuthoringSourceMetadata();
        public TriggerAuthoringModuleData Module = new TriggerAuthoringModuleData();
    }

    [Serializable]
    public sealed class TriggerAuthoringTemplateSourceDocument
    {
        public string Schema = TriggerAuthoringSchema.Id;
        public string Version = TriggerAuthoringSchema.Version;
        public TriggerAuthoringSourceMetadata Metadata = new TriggerAuthoringSourceMetadata();
        public TriggerAuthoringTemplateData Template = new TriggerAuthoringTemplateData();
    }

    [Serializable]
    public sealed class TriggerAuthoringSourceMetadata
    {
        public string Author = "team";
        public string Description;
    }

    [Serializable]
    public sealed class TriggerAuthoringModuleData
    {
        public string ModuleId;
        public string DisplayName;
        public TriggerModuleKind Kind;
        public List<TriggerBlackboardVariableData> Blackboard = new List<TriggerBlackboardVariableData>();
        public List<TriggerNodeGroupData> ConditionGroups = new List<TriggerNodeGroupData>();
        public List<TriggerNodeGroupData> ActionGroups = new List<TriggerNodeGroupData>();
        public List<TriggerDefinitionData> Triggers = new List<TriggerDefinitionData>();
    }

    [Serializable]
    public sealed class TriggerNodeGroupData
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public TriggerNodeData Root;
    }

    [Serializable]
    public sealed class TriggerDefinitionData
    {
        public int Id;
        public string Name;
        public string GroupPath;
        public List<string> Tags = new List<string>();
        public bool Enabled = true;
        public TriggerEntryMode EntryMode;
        public string Event;
        public string Phase = "immediate";
        public int Priority;
        public int InterruptPriority;
        public string Scope = "owner";
        public bool AllowExternal;
        public TriggerScheduleData Schedule = new TriggerScheduleData();
        public TriggerCueData Cue = new TriggerCueData();
        public TriggerExecutionControlData ExecutionControl = new TriggerExecutionControlData();
        public TriggerTemplateReferenceData Template;
        public TriggerNodeData Condition;
        public TriggerNodeData Actions;
        public List<TriggerBlackboardVariableData> Blackboard = new List<TriggerBlackboardVariableData>();
        // Null keeps existing source documents stable until a callable contract is declared.
        public List<TriggerCallableParameterData> CallableParameters;
        public string Note;
    }

    [Serializable]
    public sealed class TriggerNodeData
    {
        public string NodeId;
        public bool Enabled = true;
        public TriggerNodeKind Kind;
        public string GroupReference;
        public string Type;
        public string Note;
        public List<TriggerArgumentData> Arguments = new List<TriggerArgumentData>();
        // Action flow nodes use an explicit predicate so it cannot be confused with child actions.
        public TriggerNodeData Condition;
        public List<TriggerNodeData> Children = new List<TriggerNodeData>();
        public List<TriggerNodeData> ElseChildren = new List<TriggerNodeData>();
    }

    public static class TriggerAuthoringNodeIdentity
    {
        private const string Prefix = "node_";

        public static string Create()
        {
            return Prefix + Guid.NewGuid().ToString("N");
        }

        public static bool IsValid(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || !nodeId.StartsWith(Prefix, StringComparison.Ordinal))
                return false;

            var suffix = nodeId.Substring(Prefix.Length);
            var offset = suffix.Length == 17 && suffix[0] == 'l' ? 1 : 0;
            if (suffix.Length != 32 && offset == 0) return false;
            for (var i = offset; i < suffix.Length; i++)
            {
                var value = suffix[i];
                if (value < '0' || value > '9' && (value < 'a' || value > 'f')) return false;
            }
            return true;
        }

        public static int EnsureModule(TriggerAuthoringModuleData module)
        {
            if (module == null) return 0;
            var occupied = CollectIds(module);
            var visited = new HashSet<TriggerNodeData>();
            var assigned = 0;
            assigned += EnsureGroups(
                module.ConditionGroups,
                "module:" + (module.ModuleId ?? string.Empty) + ":condition-groups",
                occupied,
                visited);
            assigned += EnsureGroups(
                module.ActionGroups,
                "module:" + (module.ModuleId ?? string.Empty) + ":action-groups",
                occupied,
                visited);

            var triggers = module.Triggers;
            if (triggers == null) return assigned;
            for (var i = 0; i < triggers.Count; i++)
            {
                var trigger = triggers[i];
                if (trigger == null) continue;
                var seed = "module:" + (module.ModuleId ?? string.Empty) + ":trigger:" + trigger.Id + ":" + i;
                assigned += EnsureTreeInternal(trigger.Condition, seed + ":condition", occupied, visited);
                assigned += EnsureTreeInternal(trigger.Actions, seed + ":actions", occupied, visited);
            }
            return assigned;
        }

        public static int EnsureTemplate(TriggerAuthoringTemplateData template)
        {
            if (template == null) return 0;
            var occupied = new HashSet<string>(StringComparer.Ordinal);
            var definition = template.Definition;
            CollectIds(definition?.Condition, occupied, new HashSet<TriggerNodeData>());
            CollectIds(definition?.Actions, occupied, new HashSet<TriggerNodeData>());
            var visited = new HashSet<TriggerNodeData>();
            var seed = "template:" + (template.TemplateId ?? string.Empty) + ":" +
                       (template.TemplateVersion ?? string.Empty);
            return EnsureTreeInternal(definition?.Condition, seed + ":condition", occupied, visited) +
                   EnsureTreeInternal(definition?.Actions, seed + ":actions", occupied, visited);
        }

        public static int EnsureTree(TriggerNodeData root, string seed)
        {
            var occupied = new HashSet<string>(StringComparer.Ordinal);
            CollectIds(root, occupied, new HashSet<TriggerNodeData>());
            return EnsureTreeInternal(
                root,
                string.IsNullOrEmpty(seed) ? "tree" : seed,
                occupied,
                new HashSet<TriggerNodeData>());
        }

        public static void RegenerateTree(TriggerNodeData root)
        {
            RegenerateTree(root, new HashSet<string>(StringComparer.Ordinal), new HashSet<TriggerNodeData>());
        }

        private static int EnsureGroups(
            IReadOnlyList<TriggerNodeGroupData> groups,
            string seed,
            ISet<string> occupied,
            ISet<TriggerNodeData> visited)
        {
            if (groups == null) return 0;
            var assigned = 0;
            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                if (group == null) continue;
                assigned += EnsureTreeInternal(
                    group.Root,
                    seed + ":" + (group.Id ?? string.Empty) + ":" + i,
                    occupied,
                    visited);
            }
            return assigned;
        }

        private static int EnsureTreeInternal(
            TriggerNodeData node,
            string path,
            ISet<string> occupied,
            ISet<TriggerNodeData> visited)
        {
            if (node == null || !visited.Add(node)) return 0;
            var assigned = 0;
            if (string.IsNullOrWhiteSpace(node.NodeId))
            {
                node.NodeId = CreateLegacyId(path, occupied);
                occupied.Add(node.NodeId);
                assigned++;
            }

            assigned += EnsureTreeInternal(node.Condition, path + ":condition", occupied, visited);
            assigned += EnsureChildren(node.Children, path + ":children", occupied, visited);
            assigned += EnsureChildren(node.ElseChildren, path + ":else-children", occupied, visited);
            return assigned;
        }

        private static int EnsureChildren(
            IReadOnlyList<TriggerNodeData> nodes,
            string path,
            ISet<string> occupied,
            ISet<TriggerNodeData> visited)
        {
            if (nodes == null) return 0;
            var assigned = 0;
            for (var i = 0; i < nodes.Count; i++)
                assigned += EnsureTreeInternal(nodes[i], path + ":" + i, occupied, visited);
            return assigned;
        }

        private static HashSet<string> CollectIds(TriggerAuthoringModuleData module)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<TriggerNodeData>();
            CollectGroupIds(module.ConditionGroups, ids, visited);
            CollectGroupIds(module.ActionGroups, ids, visited);
            var triggers = module.Triggers;
            if (triggers != null)
                for (var i = 0; i < triggers.Count; i++)
                {
                    CollectIds(triggers[i]?.Condition, ids, visited);
                    CollectIds(triggers[i]?.Actions, ids, visited);
                }
            return ids;
        }

        private static void CollectGroupIds(
            IReadOnlyList<TriggerNodeGroupData> groups,
            ISet<string> ids,
            ISet<TriggerNodeData> visited)
        {
            if (groups == null) return;
            for (var i = 0; i < groups.Count; i++) CollectIds(groups[i]?.Root, ids, visited);
        }

        private static void CollectIds(
            TriggerNodeData node,
            ISet<string> ids,
            ISet<TriggerNodeData> visited)
        {
            if (node == null || !visited.Add(node)) return;
            if (!string.IsNullOrWhiteSpace(node.NodeId)) ids.Add(node.NodeId);
            CollectIds(node.Condition, ids, visited);
            CollectChildIds(node.Children, ids, visited);
            CollectChildIds(node.ElseChildren, ids, visited);
        }

        private static void CollectChildIds(
            IReadOnlyList<TriggerNodeData> nodes,
            ISet<string> ids,
            ISet<TriggerNodeData> visited)
        {
            if (nodes == null) return;
            for (var i = 0; i < nodes.Count; i++) CollectIds(nodes[i], ids, visited);
        }

        private static void RegenerateTree(
            TriggerNodeData node,
            ISet<string> occupied,
            ISet<TriggerNodeData> visited)
        {
            if (node == null || !visited.Add(node)) return;
            do node.NodeId = Create(); while (!occupied.Add(node.NodeId));
            RegenerateTree(node.Condition, occupied, visited);
            RegenerateChildren(node.Children, occupied, visited);
            RegenerateChildren(node.ElseChildren, occupied, visited);
        }

        private static void RegenerateChildren(
            IReadOnlyList<TriggerNodeData> nodes,
            ISet<string> occupied,
            ISet<TriggerNodeData> visited)
        {
            if (nodes == null) return;
            for (var i = 0; i < nodes.Count; i++) RegenerateTree(nodes[i], occupied, visited);
        }

        private static string CreateLegacyId(string seed, ISet<string> occupied)
        {
            var attempt = 0;
            while (true)
            {
                var value = attempt == 0 ? seed : seed + ":collision:" + attempt;
                var candidate = Prefix + "l" + ComputeFnv1A64(value).ToString("x16");
                if (!occupied.Contains(candidate)) return candidate;
                attempt++;
            }
        }

        private static ulong ComputeFnv1A64(string value)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offset;
            unchecked
            {
                for (var i = 0; i < value.Length; i++)
                {
                    hash ^= (byte)value[i];
                    hash *= prime;
                    hash ^= (byte)(value[i] >> 8);
                    hash *= prime;
                }
            }
            return hash;
        }
    }

    [Serializable]
    public sealed class TriggerArgumentData
    {
        public string Name;
        public TriggerValueRefData Value = new TriggerValueRefData();
    }

    [Serializable]
    public sealed class TriggerValueRefData
    {
        public TriggerValueSource Source;
        public TriggerValueType Type;
        public long IntegerValue;
        public double NumberValue;
        public bool BooleanValue;
        public string StringValue;
        public List<long> IntegerListValue = new List<long>();
        public TriggerVector3Data Vector3Value = new TriggerVector3Data();
        public List<TriggerArgumentData> Fields = new List<TriggerArgumentData>();
        public string Path;
        public string Expression;
    }

    [Serializable]
    public sealed class TriggerVector3Data
    {
        public double X;
        public double Y;
        public double Z;
    }

    [Serializable]
    public sealed class TriggerBlackboardVariableData
    {
        public string Key;
        public TriggerValueType Type;
        public bool ReadOnly;
        public string Description;
        public TriggerValueRefData DefaultValue = new TriggerValueRefData();
    }

    [Serializable]
    public sealed class TriggerCallableParameterData
    {
        public string Name;
        public string LocalVariableKey;
        public TriggerValueType Type;
        public TriggerCallableParameterDirection Direction;
        public bool Required = true;
        public bool HasDefault;
        public TriggerValueRefData DefaultValue = new TriggerValueRefData();
        public string Description;
    }

    [Serializable]
    public sealed class TriggerPayloadFieldData
    {
        public string Path;
        public string DisplayName;
        public TriggerValueType Type;
        public string Description;
    }

    [Serializable]
    public sealed class TriggerEventDefinitionData
    {
        public string Id;
        public TriggerEventMatchMode MatchMode;
        public string DisplayName;
        public string Category;
        public string PayloadType;
        public List<TriggerPayloadFieldData> PayloadFields = new List<TriggerPayloadFieldData>();
        public bool AllowExternal;
        public bool Deterministic = true;
        public string Description;
    }

    [Serializable]
    public sealed class TriggerGlobalBlackboardKeyData
    {
        public string Key;
        public string DisplayName;
        public TriggerValueType Type;
        public TriggerValueRefData DefaultValue = new TriggerValueRefData();
        public bool CanRead = true;
        public bool CanWrite = true;
        public string Domain = "global";
        public string Description;
    }

    [Serializable]
    public sealed class TriggerTemplateReferenceData
    {
        public string TemplateId;
        public string Version;
        public List<TriggerArgumentData> Bindings = new List<TriggerArgumentData>();
    }

    [Serializable]
    public sealed class TriggerAuthoringTemplateData
    {
        public string TemplateId;
        public string TemplateVersion = "1.0.0";
        public string DisplayName;
        public string Description;
        public List<TriggerAuthoringTemplateParameterData> Parameters =
            new List<TriggerAuthoringTemplateParameterData>();

        // 模板与普通触发器共用同一份定义结构；默认作为可调用函数，不订阅 EventBus。
        public TriggerDefinitionData Definition = CreateDefaultDefinition();

        public static TriggerDefinitionData CreateDefaultDefinition()
        {
            return new TriggerDefinitionData
            {
                EntryMode = TriggerEntryMode.Callable,
                Event = string.Empty,
                Actions = new TriggerNodeData
                {
                    Kind = TriggerNodeKind.Action,
                    Type = "seq"
                }
            };
        }
    }

    [Serializable]
    public sealed class TriggerAuthoringTemplateParameterData
    {
        public string Name;
        public string LocalVariableKey;
        public TriggerValueType Type;
        public bool Required = true;
        public TriggerTemplateValueSourceMask AllowedSources = TriggerTemplateValueSourceMask.InstanceBinding;
        public bool HasDefault;
        public TriggerValueRefData DefaultValue = new TriggerValueRefData();
        public string Description;
    }

    [Serializable]
    public sealed class TriggerScheduleData
    {
        public string Mode = "transient";
        public int DelayMilliseconds;
        public int IntervalMilliseconds;
        public int RepeatCount;
    }

    [Serializable]
    public sealed class TriggerCueData
    {
        public string CueId;
    }

    [Serializable]
    public sealed class TriggerExecutionControlData
    {
        public string Mode;
        public int MaxExecutions;
        public double CooldownMilliseconds;
        public string InterruptPolicy = "none";
        public bool StopPropagationOnSuccess;
        public bool StopPropagationOnFailure;
    }
}
