using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.HFSM.Actions
{
    public static class BehaviorTreeBuilder
    {
        public static IAction BuildFromEditorItems(
            IReadOnlyList<AbilityKit.HFSM.BehaviorItem> items,
            string rootId)
        {
            return BuildFromEditorItems(items, rootId, null);
        }

        /// <summary>
        /// Builds a behavior tree and wraps every node with a runtime state source.
        /// The default builder remains unwrapped for compatibility with existing callers.
        /// </summary>
        public static IAction BuildInstrumentedFromEditorItems(
            IReadOnlyList<AbilityKit.HFSM.BehaviorItem> items,
            string rootId,
            IDictionary<string, IActionRuntimeStateSource> runtimeStates)
        {
            return BuildFromEditorItems(items, rootId, runtimeStates);
        }

        private static IAction BuildFromEditorItems(
            IReadOnlyList<AbilityKit.HFSM.BehaviorItem> items,
            string rootId,
            IDictionary<string, IActionRuntimeStateSource> runtimeStates)
        {
            if (items == null || items.Count == 0)
                throw new ArgumentException("A behavior tree must contain at least one node.", nameof(items));
            if (string.IsNullOrWhiteSpace(rootId))
                throw new ArgumentException("A behavior tree root ID is required.", nameof(rootId));

            EnsureRegistryInitialized();

            var itemMap = new Dictionary<string, AbilityKit.HFSM.BehaviorItem>(StringComparer.Ordinal);
            var actionMap = new Dictionary<string, IAction>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (item == null)
                    throw new InvalidOperationException("A behavior tree cannot contain a null node.");
                if (string.IsNullOrWhiteSpace(item.id))
                    throw new InvalidOperationException("Every behavior node must have an ID.");
                if (!itemMap.TryAdd(item.id, item))
                    throw new InvalidOperationException($"Duplicate behavior node ID '{item.id}'.");

                var action = BehaviorTypeRegistry.CreateAndConfigure(item.TypeName, item);
                action.Name = item.displayName;
                actionMap.Add(item.id, action);
            }

            if (runtimeStates != null)
            {
                var instrumentedMap = new Dictionary<string, IAction>(StringComparer.Ordinal);
                foreach (var item in items)
                {
                    var observed = new ObservedAction(
                        item.id,
                        item.parentId,
                        item.TypeName,
                        actionMap[item.id]);
                    instrumentedMap.Add(item.id, observed);
                    runtimeStates[item.id] = observed;
                }
                actionMap = instrumentedMap;
            }

            if (!itemMap.TryGetValue(rootId, out var rootItem))
                throw new InvalidOperationException($"Behavior tree root '{rootId}' does not exist.");
            if (!string.IsNullOrEmpty(rootItem.parentId))
                throw new InvalidOperationException($"Behavior tree root '{rootId}' cannot have a parent.");

            foreach (var item in items)
            {
                var action = actionMap[item.id];
                var category = BehaviorTypeRegistry.GetCategory(item.TypeName);
                ValidateChildCount(item, category);

                foreach (var childId in item.childIds)
                {
                    if (!itemMap.TryGetValue(childId, out var childItem))
                        throw new InvalidOperationException($"Behavior '{item.id}' references missing child '{childId}'.");
                    if (!string.Equals(childItem.parentId, item.id, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Behavior '{childId}' does not reference '{item.id}' as its parent.");
                    }

                    var childAction = actionMap[childId];
                    if (category == BehaviorCategory.Composite)
                        ((ICompositeAction)action).AddChild(childAction);
                    else if (category == BehaviorCategory.Decorator)
                        ((IDecoratorAction)action).SetChild(childAction);
                }
            }

            var visited = new HashSet<string>(StringComparer.Ordinal);
            Visit(rootId, itemMap, visited, new HashSet<string>(StringComparer.Ordinal));
            if (visited.Count != items.Count)
                throw new InvalidOperationException("Every behavior node must be reachable from the single root.");

            return actionMap[rootId];
        }

        public static IAction BuildFromEditorItems(IReadOnlyList<AbilityKit.HFSM.BehaviorItem> items)
        {
            if (items == null || items.Count == 0)
                throw new ArgumentException("A behavior tree must contain at least one node.", nameof(items));

            string rootId = null;
            var rootCount = 0;
            foreach (var item in items)
            {
                if (item != null && string.IsNullOrEmpty(item.parentId))
                {
                    rootId = item.id;
                    rootCount++;
                }
            }

            if (rootCount != 1)
                throw new InvalidOperationException($"A behavior tree must have exactly one root, but found {rootCount}.");

            return BuildFromEditorItems(items, rootId);
        }

        private static void ValidateChildCount(AbilityKit.HFSM.BehaviorItem item, BehaviorCategory category)
        {
            var definition = BehaviorTypeRegistry.GetDefinition(item.TypeName);
            var count = item.childIds.Count;
            var min = definition?.minChildren ?? (category == BehaviorCategory.Primitive ? 0 : 1);
            var max = definition?.maxChildren ?? (category == BehaviorCategory.Composite ? -1 : 1);
            if (count < min)
                throw new InvalidOperationException($"Behavior '{item.id}' requires at least {min} child node(s).");
            if (max >= 0 && count > max)
                throw new InvalidOperationException($"Behavior '{item.id}' supports at most {max} child node(s).");
        }

        private static void Visit(
            string id,
            IReadOnlyDictionary<string, AbilityKit.HFSM.BehaviorItem> items,
            ISet<string> visited,
            ISet<string> activePath)
        {
            if (!activePath.Add(id))
                throw new InvalidOperationException($"Behavior tree contains a cycle at '{id}'.");
            if (!visited.Add(id))
                throw new InvalidOperationException($"Behavior '{id}' is referenced by more than one parent.");

            foreach (var childId in items[id].childIds)
                Visit(childId, items, visited, activePath);
            activePath.Remove(id);
        }

        private static void EnsureRegistryInitialized()
        {
            if (!BehaviorTypeRegistry.IsInitialized)
                BehaviorTypeRegistry.Initialize();
        }

        private sealed class ObservedAction : IAction, ICompositeAction, IDecoratorAction, IActionRuntimeStateSource
        {
            private readonly IAction _inner;

            public ObservedAction(string runtimeId, string parentRuntimeId, string typeName, IAction inner)
            {
                RuntimeId = runtimeId ?? string.Empty;
                ParentRuntimeId = parentRuntimeId ?? string.Empty;
                TypeName = typeName ?? string.Empty;
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public string RuntimeId { get; }
            public string ParentRuntimeId { get; }
            public string TypeName { get; }
            public ActionRuntimeStatus RuntimeStatus { get; private set; }
            public bool IsActive { get; private set; }
            public int ExecutionCount { get; private set; }
            public float ElapsedTime { get; private set; }

            public string Name
            {
                get => _inner.Name;
                set => _inner.Name = value;
            }

            public BehaviorStatus Execute(BehaviorContext context)
            {
                ExecutionCount++;
                IsActive = true;
                RuntimeStatus = ActionRuntimeStatus.Running;
                if (context != null && context.deltaTime > 0f)
                    ElapsedTime += context.deltaTime;

                var status = _inner.Execute(context);
                IsActive = status == BehaviorStatus.Running;
                RuntimeStatus = status == BehaviorStatus.Running
                    ? ActionRuntimeStatus.Running
                    : status == BehaviorStatus.Success
                        ? ActionRuntimeStatus.Success
                        : ActionRuntimeStatus.Failure;
                return status;
            }

            public void Reset()
            {
                _inner.Reset();
                RuntimeStatus = ActionRuntimeStatus.Inactive;
                IsActive = false;
                ExecutionCount = 0;
                ElapsedTime = 0f;
            }

            public void ForceEnd()
            {
                _inner.ForceEnd();
                RuntimeStatus = ActionRuntimeStatus.Cancelled;
                IsActive = false;
            }

            public void AddChild(IAction child)
            {
                if (!(_inner is ICompositeAction composite))
                    throw new InvalidOperationException($"Action '{TypeName}' is not composite.");
                composite.AddChild(child);
            }

            public void SetChild(IAction child)
            {
                if (!(_inner is IDecoratorAction decorator))
                    throw new InvalidOperationException($"Action '{TypeName}' is not a decorator.");
                decorator.SetChild(child);
            }
        }
    }
}

namespace AbilityKit.HFSM
{
    public enum BehaviorParameterType
    {
        Float,
        Int,
        Bool,
        String,
        Object,
        Vector2,
        Vector3,
        Color
    }

    [Serializable]
    public sealed class BehaviorParameter
    {
        public string name;

        [SerializeField]
        private BehaviorParameterType valueType;

        public BehaviorParameterType ValueType
        {
            get => valueType;
            set => valueType = value;
        }

        public float floatValue;
        public int intValue;
        public bool boolValue;
        public string stringValue;
        public UnityEngine.Object objectValue;
        public Vector2 vector2Value;
        public Vector3 vector3Value;
        public Color colorValue;

        public BehaviorParameter()
        {
        }

        public BehaviorParameter(string name, float value)
        {
            this.name = name;
            floatValue = value;
            ValueType = BehaviorParameterType.Float;
        }

        public BehaviorParameter(string name, int value)
        {
            this.name = name;
            intValue = value;
            ValueType = BehaviorParameterType.Int;
        }

        public BehaviorParameter(string name, bool value)
        {
            this.name = name;
            boolValue = value;
            ValueType = BehaviorParameterType.Bool;
        }

        public BehaviorParameter(string name, string value)
        {
            this.name = name;
            stringValue = value;
            ValueType = BehaviorParameterType.String;
        }

        public BehaviorParameter(string name, UnityEngine.Object value)
        {
            this.name = name;
            objectValue = value;
            ValueType = BehaviorParameterType.Object;
        }

        public BehaviorParameter(string name, Vector3 value)
        {
            this.name = name;
            vector3Value = value;
            ValueType = BehaviorParameterType.Vector3;
        }

        public T GetValue<T>()
        {
            object value = ValueType switch
            {
                BehaviorParameterType.Float => floatValue,
                BehaviorParameterType.Int => intValue,
                BehaviorParameterType.Bool => boolValue,
                BehaviorParameterType.String => stringValue,
                BehaviorParameterType.Object => objectValue,
                BehaviorParameterType.Vector2 => vector2Value,
                BehaviorParameterType.Vector3 => vector3Value,
                BehaviorParameterType.Color => colorValue,
                _ => throw new ArgumentOutOfRangeException()
            };

            if (value == null)
                return default;
            if (value is T typedValue)
                return typedValue;
            if (value is UnityEngine.Object unityObject && typeof(UnityEngine.Object).IsAssignableFrom(typeof(T)))
                return (T)(object)unityObject;

            throw new InvalidCastException(
                $"Behavior parameter '{name}' contains {ValueType}, not {typeof(T).FullName}.");
        }

        public object GetValueAsObject()
        {
            return ValueType switch
            {
                BehaviorParameterType.Float => floatValue,
                BehaviorParameterType.Int => intValue,
                BehaviorParameterType.Bool => boolValue,
                BehaviorParameterType.String => stringValue,
                BehaviorParameterType.Object => objectValue,
                BehaviorParameterType.Vector2 => vector2Value,
                BehaviorParameterType.Vector3 => vector3Value,
                BehaviorParameterType.Color => colorValue,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    [Serializable]
    public sealed class BehaviorItem
    {
        public string id;
        public string displayName;

        [SerializeField]
        private string typeName;

        public string TypeName
        {
            get => string.IsNullOrWhiteSpace(typeName)
                ? throw new InvalidOperationException($"Behavior '{id}' has no type name.")
                : typeName;
            set => typeName = string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("A behavior type name is required.", nameof(value))
                : value;
        }

        public string parentId;
        public List<string> childIds = new List<string>();
        public List<BehaviorParameter> parameters = new List<BehaviorParameter>();
        public bool isExpanded = true;

        public BehaviorItem()
        {
            id = Guid.NewGuid().ToString();
            displayName = "New Behavior";
        }

        public BehaviorItem(string typeName, string displayName = null)
        {
            id = Guid.NewGuid().ToString();
            TypeName = typeName;
            this.displayName = displayName ?? GetDefaultDisplayName(typeName);
            SetupDefaultParameters(typeName);
        }

        public BehaviorParameter GetParameter(string parameterName)
        {
            return parameters.Find(parameter => parameter.name == parameterName);
        }

        public void SetParameter(string parameterName, float value) =>
            SetParameter(parameterName, BehaviorParameterType.Float, parameter => parameter.floatValue = value);

        public void SetParameter(string parameterName, int value) =>
            SetParameter(parameterName, BehaviorParameterType.Int, parameter => parameter.intValue = value);

        public void SetParameter(string parameterName, bool value) =>
            SetParameter(parameterName, BehaviorParameterType.Bool, parameter => parameter.boolValue = value);

        public void SetParameter(string parameterName, string value) =>
            SetParameter(parameterName, BehaviorParameterType.String, parameter => parameter.stringValue = value);

        public bool IsComposite => GetCategory() == BehaviorCategory.Composite;

        public bool IsDecorator => GetCategory() == BehaviorCategory.Decorator;

        public T GetParamValue<T>(string parameterName)
        {
            var parameter = GetParameter(parameterName);
            return parameter != null ? parameter.GetValue<T>() : default;
        }

        public string GetDescription()
        {
            return TypeName switch
            {
                "Wait" => $"Wait {GetParamValue<float>("duration")}s",
                "Log" => $"Log: {GetParamValue<string>("message")}",
                "SetFloat" => $"{GetParamValue<string>("variableName")} = {GetParamValue<float>("value")}",
                "SetBool" => $"{GetParamValue<string>("variableName")} = {GetParamValue<bool>("value")}",
                "SetInt" => $"{GetParamValue<string>("variableName")} = {GetParamValue<int>("value")}",
                "PlayAnimation" => $"Play: {GetParamValue<string>("stateName")}",
                "Repeat" => GetParamValue<int>("count") < 0
                    ? "Repeat (Infinite)"
                    : $"Repeat x{GetParamValue<int>("count")}",
                "Sequence" => $"Sequence [{childIds.Count}]",
                "Selector" => $"Selector [{childIds.Count}]",
                "Parallel" => $"Parallel [{childIds.Count}]",
                _ => TypeName
            };
        }

        public BehaviorItem Clone()
        {
            var clone = new BehaviorItem
            {
                id = Guid.NewGuid().ToString(),
                displayName = displayName,
                TypeName = TypeName,
                isExpanded = isExpanded
            };

            foreach (var parameter in parameters)
            {
                clone.parameters.Add(new BehaviorParameter
                {
                    name = parameter.name,
                    ValueType = parameter.ValueType,
                    floatValue = parameter.floatValue,
                    intValue = parameter.intValue,
                    boolValue = parameter.boolValue,
                    stringValue = parameter.stringValue,
                    objectValue = parameter.objectValue,
                    vector2Value = parameter.vector2Value,
                    vector3Value = parameter.vector3Value,
                    colorValue = parameter.colorValue
                });
            }

            return clone;
        }

        private void SetParameter(
            string parameterName,
            BehaviorParameterType parameterType,
            Action<BehaviorParameter> setValue)
        {
            var parameter = GetParameter(parameterName);
            if (parameter == null)
                throw new InvalidOperationException($"Behavior '{TypeName}' has no parameter '{parameterName}'.");

            parameter.ValueType = parameterType;
            setValue(parameter);
        }

        private static string GetDefaultDisplayName(string behaviorTypeName)
        {
            EnsureRegistryInitialized();
            return BehaviorTypeRegistry.GetDefinition(behaviorTypeName)?.displayName ?? behaviorTypeName;
        }

        private void SetupDefaultParameters(string behaviorTypeName)
        {
            EnsureRegistryInitialized();
            var definition = BehaviorTypeRegistry.GetDefinition(behaviorTypeName);
            if (definition == null)
                return;

            foreach (var parameterDefinition in definition.parameters)
            {
                var parameter = new BehaviorParameter
                {
                    name = parameterDefinition.name,
                    ValueType = parameterDefinition.valueType
                };
                parameterDefinition.ApplyDefaultValue(parameter);
                parameters.Add(parameter);
            }
        }

        private BehaviorCategory GetCategory()
        {
            EnsureRegistryInitialized();
            return BehaviorTypeRegistry.GetCategory(TypeName);
        }

        private static void EnsureRegistryInitialized()
        {
            if (!BehaviorTypeRegistry.IsInitialized)
                BehaviorTypeRegistry.Initialize();
        }
    }
}
