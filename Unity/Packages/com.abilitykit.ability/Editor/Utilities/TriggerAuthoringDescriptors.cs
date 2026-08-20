using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;

namespace AbilityKit.Ability.Editor.Utilities
{
    [Flags]
    internal enum TriggerValueSourceMask
    {
        None = 0,
        Constant = 1 << 0,
        Payload = 1 << 1,
        Context = 1 << 2,
        LocalBlackboard = 1 << 3,
        GlobalBlackboard = 1 << 4,
        TemplateParameter = 1 << 5,
        Expression = 1 << 6,
        All = Constant | Payload | Context | LocalBlackboard | GlobalBlackboard | TemplateParameter | Expression
    }

    internal sealed class TriggerParameterDescriptor
    {
        public TriggerParameterDescriptor(
            string name,
            TriggerValueType type,
            bool required = true,
            TriggerValueSourceMask allowedSources = TriggerValueSourceMask.All)
        {
            Name = name ?? string.Empty;
            Type = type;
            Required = required;
            AllowedSources = allowedSources;
        }

        public string Name { get; }
        public TriggerValueType Type { get; }
        public bool Required { get; }
        public TriggerValueSourceMask AllowedSources { get; }
    }

    internal sealed class TriggerTypeDescriptor
    {
        public TriggerTypeDescriptor(
            TriggerNodeKind kind,
            string type,
            string displayName,
            string category,
            int minChildren = 0,
            int maxChildren = 0,
            params TriggerParameterDescriptor[] parameters)
        {
            Kind = kind;
            Type = type ?? string.Empty;
            DisplayName = displayName ?? Type;
            Category = category ?? string.Empty;
            MinChildren = minChildren;
            MaxChildren = maxChildren;
            Parameters = parameters ?? Array.Empty<TriggerParameterDescriptor>();
        }

        public TriggerNodeKind Kind { get; }
        public string Type { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public int MinChildren { get; }
        public int MaxChildren { get; }
        public IReadOnlyList<TriggerParameterDescriptor> Parameters { get; }
    }

    internal sealed class TriggerTypeDescriptorCatalog
    {
        private readonly Dictionary<string, TriggerTypeDescriptor> _entries =
            new Dictionary<string, TriggerTypeDescriptor>(StringComparer.Ordinal);

        public void Register(TriggerTypeDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (string.IsNullOrWhiteSpace(descriptor.Type))
                throw new ArgumentException("Descriptor type is required.", nameof(descriptor));

            _entries[BuildKey(descriptor.Kind, descriptor.Type)] = descriptor;
        }

        public bool TryGet(TriggerNodeKind kind, string type, out TriggerTypeDescriptor descriptor)
        {
            return _entries.TryGetValue(BuildKey(kind, type), out descriptor);
        }

        public static TriggerTypeDescriptorCatalog CreateProjectDefaults()
        {
            var catalog = new TriggerTypeDescriptorCatalog();
            catalog.Register(new TriggerTypeDescriptor(TriggerNodeKind.Condition, "all", "全部满足", "条件/复合", 1, -1));
            catalog.Register(new TriggerTypeDescriptor(TriggerNodeKind.Condition, "any", "任意满足", "条件/复合", 1, -1));
            catalog.Register(new TriggerTypeDescriptor(TriggerNodeKind.Condition, "not", "取反", "条件/复合", 1, 1));
            catalog.Register(new TriggerTypeDescriptor(
                TriggerNodeKind.Condition,
                "arg_eq",
                "参数等于",
                "条件/参数",
                0,
                0,
                new TriggerParameterDescriptor("left", TriggerValueType.None),
                new TriggerParameterDescriptor("right", TriggerValueType.None)));
            catalog.Register(new TriggerTypeDescriptor(
                TriggerNodeKind.Condition,
                "arg_gt",
                "参数大于",
                "条件/参数",
                0,
                0,
                new TriggerParameterDescriptor("left", TriggerValueType.Number),
                new TriggerParameterDescriptor("right", TriggerValueType.Number)));

            catalog.Register(new TriggerTypeDescriptor(TriggerNodeKind.Action, "seq", "顺序组", "行为/流程", 1, -1));
            catalog.Register(new TriggerTypeDescriptor(
                TriggerNodeKind.Action,
                "debug_log",
                "输出日志",
                "行为/调试",
                0,
                0,
                new TriggerParameterDescriptor("message", TriggerValueType.String),
                new TriggerParameterDescriptor("dump_args", TriggerValueType.Boolean, false)));

            RegisterOpenProjectTypes(catalog, TriggerNodeKind.Condition, new[]
            {
                "arg_lt", "arg_leq", "arg_geq", "num_var_gt", "num_var_lt", "num_var_eq",
                "has_buff", "has_tag", "is_alive", "is_grounded"
            });
            RegisterOpenProjectTypes(catalog, TriggerNodeKind.Action, new[]
            {
                "set_var", "set_num_var", "log_attacker", "effect_execute", "add_buff", "remove_buff",
                "shoot_projectile", "give_damage", "take_damage", "heal", "modify_resource", "spawn_summon",
                "play_presentation", "attr_effect_duration", "emit", "aoe_burst", "knock"
            });
            return catalog;
        }

        private static void RegisterOpenProjectTypes(
            TriggerTypeDescriptorCatalog catalog,
            TriggerNodeKind kind,
            IEnumerable<string> types)
        {
            foreach (var type in types)
            {
                catalog.Register(new TriggerTypeDescriptor(kind, type, type, "项目", 0, 0));
            }
        }

        private static string BuildKey(TriggerNodeKind kind, string type)
        {
            return ((int)kind).ToString() + ":" + (type ?? string.Empty);
        }
    }

    internal enum TriggerAuthoringDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    internal sealed class TriggerAuthoringDiagnostic
    {
        public TriggerAuthoringDiagnostic(
            string code,
            TriggerAuthoringDiagnosticSeverity severity,
            string path,
            string message)
        {
            Code = code;
            Severity = severity;
            Path = path;
            Message = message;
        }

        public string Code { get; }
        public TriggerAuthoringDiagnosticSeverity Severity { get; }
        public string Path { get; }
        public string Message { get; }
    }

    internal static class TriggerAuthoringValidator
    {
        public static List<TriggerAuthoringDiagnostic> Validate(
            TriggerAuthoringModuleData module,
            TriggerTypeDescriptorCatalog catalog = null)
        {
            catalog = catalog ?? TriggerTypeDescriptorCatalog.CreateProjectDefaults();
            var diagnostics = new List<TriggerAuthoringDiagnostic>();
            if (module == null)
            {
                AddError(diagnostics, "TRG1000", "module", "Module is null.");
                return diagnostics;
            }

            if (string.IsNullOrWhiteSpace(module.ModuleId))
                AddError(diagnostics, "TRG1001", "module.moduleId", "ModuleId is required.");

            var moduleKeys = ValidateBlackboard(diagnostics, module.Blackboard, "module.blackboard");
            var triggerIds = new HashSet<int>();
            var triggers = module.Triggers ?? new List<TriggerDefinitionData>();
            for (var i = 0; i < triggers.Count; i++)
            {
                var trigger = triggers[i];
                var path = $"module.triggers[{i}]";
                if (trigger == null)
                {
                    AddError(diagnostics, "TRG1002", path, "Trigger is null.");
                    continue;
                }

                if (trigger.Id <= 0)
                    AddError(diagnostics, "TRG1003", path + ".id", "Trigger Id must be greater than zero.");
                else if (!triggerIds.Add(trigger.Id))
                    AddError(diagnostics, "TRG1004", path + ".id", $"Duplicate Trigger Id: {trigger.Id}.");

                if (string.IsNullOrWhiteSpace(trigger.Event))
                    AddError(diagnostics, "TRG1005", path + ".event", "Event is required.");

                var triggerKeys = new HashSet<string>(moduleKeys, StringComparer.Ordinal);
                triggerKeys.UnionWith(ValidateBlackboard(diagnostics, trigger.Blackboard, path + ".blackboard"));
                ValidateNode(diagnostics, trigger.Condition, TriggerNodeKind.Condition, path + ".condition", catalog, triggerKeys);
                ValidateNode(diagnostics, trigger.Actions, TriggerNodeKind.Action, path + ".actions", catalog, triggerKeys);
            }

            return diagnostics;
        }

        public static bool HasErrors(IReadOnlyList<TriggerAuthoringDiagnostic> diagnostics)
        {
            if (diagnostics == null) return false;
            for (var i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == TriggerAuthoringDiagnosticSeverity.Error) return true;
            }
            return false;
        }

        private static HashSet<string> ValidateBlackboard(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            IReadOnlyList<TriggerBlackboardVariableData> variables,
            string path)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (variables == null) return keys;
            for (var i = 0; i < variables.Count; i++)
            {
                var variable = variables[i];
                var itemPath = $"{path}[{i}]";
                if (variable == null || string.IsNullOrWhiteSpace(variable.Key))
                {
                    AddError(diagnostics, "TRG1100", itemPath + ".key", "Blackboard key is required.");
                    continue;
                }
                if (!keys.Add(variable.Key))
                    AddError(diagnostics, "TRG1101", itemPath + ".key", $"Duplicate Blackboard key: {variable.Key}.");
                if (variable.Type == TriggerValueType.None)
                    AddError(diagnostics, "TRG1102", itemPath + ".type", "Blackboard value type is required.");
            }
            return keys;
        }

        private static void ValidateNode(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerNodeData node,
            TriggerNodeKind expectedKind,
            string path,
            TriggerTypeDescriptorCatalog catalog,
            ISet<string> localKeys)
        {
            if (node == null)
            {
                if (expectedKind == TriggerNodeKind.Action)
                    AddError(diagnostics, "TRG1200", path, "Action root is required.");
                return;
            }
            if (node.Kind != expectedKind)
                AddError(diagnostics, "TRG1201", path + ".kind", $"Expected {expectedKind}, got {node.Kind}.");
            if (string.IsNullOrWhiteSpace(node.Type))
            {
                AddError(diagnostics, "TRG1202", path + ".type", "Node type is required.");
                return;
            }
            if (!catalog.TryGet(expectedKind, node.Type, out var descriptor))
            {
                AddError(diagnostics, "TRG1203", path + ".type", $"Unknown {expectedKind} type: {node.Type}.");
                return;
            }

            var children = node.Children ?? new List<TriggerNodeData>();
            if (children.Count < descriptor.MinChildren)
                AddError(diagnostics, "TRG1204", path + ".children", $"Node requires at least {descriptor.MinChildren} child nodes.");
            if (descriptor.MaxChildren >= 0 && children.Count > descriptor.MaxChildren)
                AddError(diagnostics, "TRG1205", path + ".children", $"Node allows at most {descriptor.MaxChildren} child nodes.");

            var arguments = new Dictionary<string, TriggerArgumentData>(StringComparer.Ordinal);
            var nodeArguments = node.Arguments ?? new List<TriggerArgumentData>();
            for (var i = 0; i < nodeArguments.Count; i++)
            {
                var argument = nodeArguments[i];
                var argumentPath = $"{path}.arguments[{i}]";
                if (argument == null || string.IsNullOrWhiteSpace(argument.Name))
                {
                    AddError(diagnostics, "TRG1210", argumentPath + ".name", "Argument name is required.");
                    continue;
                }
                if (arguments.ContainsKey(argument.Name))
                    AddError(diagnostics, "TRG1211", argumentPath + ".name", $"Duplicate argument: {argument.Name}.");
                else
                    arguments.Add(argument.Name, argument);
            }

            for (var i = 0; i < descriptor.Parameters.Count; i++)
            {
                var parameter = descriptor.Parameters[i];
                if (!arguments.TryGetValue(parameter.Name, out var argument))
                {
                    if (parameter.Required)
                        AddError(diagnostics, "TRG1212", path + ".arguments", $"Required argument is missing: {parameter.Name}.");
                    continue;
                }
                ValidateValue(diagnostics, argument.Value, parameter, path + ".arguments." + parameter.Name, localKeys);
            }

            for (var i = 0; i < children.Count; i++)
                ValidateNode(diagnostics, children[i], expectedKind, $"{path}.children[{i}]", catalog, localKeys);
        }

        private static void ValidateValue(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerValueRefData value,
            TriggerParameterDescriptor parameter,
            string path,
            ISet<string> localKeys)
        {
            if (value == null)
            {
                AddError(diagnostics, "TRG1300", path, "Value is required.");
                return;
            }
            if (parameter.Type != TriggerValueType.None && value.Type != parameter.Type)
                AddError(diagnostics, "TRG1301", path + ".type", $"Expected {parameter.Type}, got {value.Type}.");
            if ((parameter.AllowedSources & ToMask(value.Source)) == 0)
                AddError(diagnostics, "TRG1302", path + ".source", $"Source {value.Source} is not allowed.");

            switch (value.Source)
            {
                case TriggerValueSource.Payload:
                case TriggerValueSource.Context:
                case TriggerValueSource.TemplateParameter:
                    if (string.IsNullOrWhiteSpace(value.Path))
                        AddError(diagnostics, "TRG1303", path + ".path", "Reference path is required.");
                    break;
                case TriggerValueSource.LocalBlackboard:
                    if (string.IsNullOrWhiteSpace(value.Path) || !localKeys.Contains(value.Path))
                        AddError(diagnostics, "TRG1304", path + ".path", $"Unknown local Blackboard key: {value.Path ?? string.Empty}.");
                    break;
                case TriggerValueSource.GlobalBlackboard:
                    if (string.IsNullOrWhiteSpace(value.Path))
                        AddError(diagnostics, "TRG1305", path + ".path", "Global Blackboard key is required.");
                    break;
                case TriggerValueSource.Expression:
                    if (string.IsNullOrWhiteSpace(value.Expression))
                        AddError(diagnostics, "TRG1306", path + ".expression", "Expression is required.");
                    break;
            }
        }

        private static TriggerValueSourceMask ToMask(TriggerValueSource source)
        {
            return (TriggerValueSourceMask)(1 << (int)source);
        }

        private static void AddError(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            string code,
            string path,
            string message)
        {
            diagnostics.Add(new TriggerAuthoringDiagnostic(code, TriggerAuthoringDiagnosticSeverity.Error, path, message));
        }
    }
}
