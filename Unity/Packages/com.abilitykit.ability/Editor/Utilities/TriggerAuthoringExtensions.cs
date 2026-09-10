#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    /// <summary>
    /// Implement in a business Editor assembly to contribute project-scoped trigger authoring types.
    /// The project must explicitly enable <see cref="Id"/> before the contribution is applied.
    /// </summary>
    public interface ITriggerAuthoringExtension
    {
        string Id { get; }
        void Register(TriggerAuthoringExtensionContext context);
    }

    public interface ITriggerAuthoringConditionCompiler
    {
        void Compile(TriggerAuthoringConditionCompilerContext context);
    }

    public sealed class TriggerAuthoringConditionCompilerContext
    {
        private readonly Func<bool, string[], TriggerAuthoringRuntimeValueRefDto> _compileArgument;
        private readonly Action<string, TriggerAuthoringRuntimeValueRefDto, TriggerAuthoringRuntimeValueRefDto> _emitFunction;
        private readonly Action<bool> _emitConstant;
        private readonly Action<string, string, double, string> _emitPayloadScaledComparison;
        private readonly Action<string, string, string> _reportError;

        internal TriggerAuthoringConditionCompilerContext(
            TriggerNodeData node,
            string path,
            Func<bool, string[], TriggerAuthoringRuntimeValueRefDto> compileArgument,
            Action<string, TriggerAuthoringRuntimeValueRefDto, TriggerAuthoringRuntimeValueRefDto> emitFunction,
            Action<bool> emitConstant,
            Action<string, string, double, string> emitPayloadScaledComparison,
            Action<string, string, string> reportError)
        {
            Node = node;
            Path = path ?? string.Empty;
            _compileArgument = compileArgument;
            _emitFunction = emitFunction;
            _emitConstant = emitConstant;
            _emitPayloadScaledComparison = emitPayloadScaledComparison;
            _reportError = reportError;
        }

        public TriggerNodeData Node { get; }
        public string Path { get; }

        public TriggerAuthoringRuntimeValueRefDto CompileArgument(
            bool required,
            params string[] aliases)
        {
            return _compileArgument(required, aliases ?? Array.Empty<string>());
        }

        public bool HasArgument(params string[] aliases)
        {
            return FindValue(aliases) != null;
        }

        public bool TryGetConstantNumber(out double value, params string[] aliases)
        {
            value = 0d;
            var source = FindValue(aliases);
            if (source == null || source.Source != TriggerValueSource.Constant) return false;
            if (source.Type == TriggerValueType.Integer)
            {
                value = source.IntegerValue;
                return true;
            }
            if (source.Type != TriggerValueType.Number) return false;
            value = source.NumberValue;
            return true;
        }

        public void EmitFunction(
            string functionKey,
            TriggerAuthoringRuntimeValueRefDto left = null,
            TriggerAuthoringRuntimeValueRefDto right = null)
        {
            _emitFunction(functionKey, left, right);
        }

        public void EmitConstant(bool value)
        {
            _emitConstant(value);
        }

        public void EmitPayloadScaledComparison(
            string leftPayloadField,
            string rightPayloadField,
            double rightScale,
            string compareOp)
        {
            _emitPayloadScaledComparison(leftPayloadField, rightPayloadField, rightScale, compareOp);
        }

        public void ReportError(string code, string pathSuffix, string message)
        {
            _reportError(code, pathSuffix, message);
        }

        private TriggerValueRefData FindValue(IReadOnlyList<string> aliases)
        {
            if (Node?.Arguments == null || aliases == null) return null;
            for (var i = 0; i < aliases.Count; i++)
            {
                var parts = (aliases[i] ?? string.Empty).Split('.');
                IReadOnlyList<TriggerArgumentData> fields = Node.Arguments;
                TriggerValueRefData value = null;
                for (var part = 0; part < parts.Length; part++)
                {
                    value = FindField(fields, parts[part])?.Value;
                    if (value == null) break;
                    fields = value.Fields;
                }
                if (value != null) return value;
            }
            return null;
        }

        private static TriggerArgumentData FindField(
            IReadOnlyList<TriggerArgumentData> fields,
            string name)
        {
            if (fields == null) return null;
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (field != null && string.Equals(field.Name, name, StringComparison.Ordinal)) return field;
            }
            return null;
        }
    }

    public sealed class TriggerAuthoringExtensionContext
    {
        private readonly TriggerTypeDescriptorCatalog _types;
        private readonly List<TriggerEventDefinitionData> _events;

        internal TriggerAuthoringExtensionContext(
            TriggerAuthoringProjectAsset project,
            TriggerTypeDescriptorCatalog types,
            List<TriggerEventDefinitionData> events)
        {
            Project = project;
            _types = types;
            _events = events;
        }

        public TriggerAuthoringProjectAsset Project { get; }
        public bool AcceptsNodes => _types != null;
        public bool AcceptsEvents => _events != null;

        public void RegisterCondition(TriggerTypeDescriptor descriptor)
        {
            RegisterNode(descriptor, TriggerNodeKind.Condition);
        }

        public void RegisterCondition(
            TriggerTypeDescriptor descriptor,
            ITriggerAuthoringConditionCompiler compiler)
        {
            RegisterNode(descriptor, TriggerNodeKind.Condition);
            if (_types != null && compiler != null)
                _types.RegisterConditionCompiler(descriptor.Type, compiler);
        }

        public void RegisterAction(TriggerTypeDescriptor descriptor)
        {
            RegisterNode(descriptor, TriggerNodeKind.Action);
        }

        public void RegisterEvent(TriggerEventDefinitionData definition)
        {
            if (_events == null) return;
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                throw new ArgumentException("事件扩展必须提供有效的事件定义。", nameof(definition));
            _events.Add(definition);
        }

        private void RegisterNode(TriggerTypeDescriptor descriptor, TriggerNodeKind expectedKind)
        {
            if (_types == null) return;
            if (descriptor == null || descriptor.Kind != expectedKind)
                throw new ArgumentException($"节点扩展必须注册 {expectedKind} 描述符。", nameof(descriptor));
            _types.Register(descriptor);
        }
    }

    internal static class TriggerAuthoringExtensionRegistry
    {
        public static void ApplyTypes(
            TriggerAuthoringProjectAsset project,
            TriggerTypeDescriptorCatalog catalog)
        {
            if (project == null || catalog == null) return;
            Apply(project, catalog, null);
        }

        public static List<TriggerEventDefinitionData> GetEvents(TriggerAuthoringProjectAsset project)
        {
            var result = new List<TriggerEventDefinitionData>();
            if (project != null) Apply(project, null, result);
            return result;
        }

        public static List<string> GetAvailableExtensionIds()
        {
            var result = new List<string>();
            var extensions = CreateExtensions();
            for (var i = 0; i < extensions.Count; i++)
            {
                var id = extensions[i].Id?.Trim();
                if (!string.IsNullOrEmpty(id) && !result.Contains(id)) result.Add(id);
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static void Apply(
            TriggerAuthoringProjectAsset project,
            TriggerTypeDescriptorCatalog types,
            List<TriggerEventDefinitionData> events)
        {
            var enabled = new HashSet<string>(project.ExtensionIds, StringComparer.Ordinal);
            if (enabled.Count == 0) return;
            var extensions = CreateExtensions();
            var applied = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < extensions.Count; i++)
            {
                var extension = extensions[i];
                var id = extension.Id?.Trim() ?? string.Empty;
                if (!enabled.Contains(id)) continue;
                if (!applied.Add(id))
                {
                    Debug.LogError($"[TriggerAuthoring] 扩展 ID '{id}' 存在重复实现，已忽略后续实现。", project);
                    continue;
                }
                try
                {
                    extension.Register(new TriggerAuthoringExtensionContext(project, types, events));
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        $"[TriggerAuthoring] 扩展 '{id}' 注册失败：{ex}",
                        project);
                }
            }
        }

        private static List<ITriggerAuthoringExtension> CreateExtensions()
        {
            var types = TypeCache.GetTypesDerivedFrom<ITriggerAuthoringExtension>();
            var result = new List<ITriggerAuthoringExtension>();
            foreach (var type in types)
            {
                if (type == null || type.IsAbstract || type.IsInterface || type.ContainsGenericParameters) continue;
                try
                {
                    if (Activator.CreateInstance(type) is ITriggerAuthoringExtension extension)
                        result.Add(extension);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[TriggerAuthoring] 无法创建扩展 '{type.FullName}'：{ex}");
                }
            }
            result.Sort((left, right) => string.Compare(
                left.GetType().FullName,
                right.GetType().FullName,
                StringComparison.Ordinal));
            return result;
        }
    }
}
#endif
