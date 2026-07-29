using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityHFSM.Actions
{
    public static class BehaviorTreeBuilder
    {
        public static IAction BuildFromEditorItems(
            IReadOnlyList<UnityHFSM.HfsmBehaviorItem> items,
            string rootId)
        {
            if (items == null || items.Count == 0)
                throw new ArgumentException("A behavior tree must contain at least one node.", nameof(items));
            if (string.IsNullOrWhiteSpace(rootId))
                throw new ArgumentException("A behavior tree root ID is required.", nameof(rootId));

            EnsureRegistryInitialized();

            var itemMap = new Dictionary<string, UnityHFSM.HfsmBehaviorItem>(StringComparer.Ordinal);
            var actionMap = new Dictionary<string, IAction>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (item == null)
                    throw new InvalidOperationException("A behavior tree cannot contain a null node.");
                if (string.IsNullOrWhiteSpace(item.id))
                    throw new InvalidOperationException("Every behavior node must have an ID.");
                if (!itemMap.TryAdd(item.id, item))
                    throw new InvalidOperationException($"Duplicate behavior node ID '{item.id}'.");

                var action = HfsmBehaviorTypeRegistry.CreateAndConfigure(item.TypeName, item);
                action.Name = item.displayName;
                actionMap.Add(item.id, action);
            }

            if (!itemMap.TryGetValue(rootId, out var rootItem))
                throw new InvalidOperationException($"Behavior tree root '{rootId}' does not exist.");
            if (!string.IsNullOrEmpty(rootItem.parentId))
                throw new InvalidOperationException($"Behavior tree root '{rootId}' cannot have a parent.");

            foreach (var item in items)
            {
                var action = actionMap[item.id];
                var category = HfsmBehaviorTypeRegistry.GetCategory(item.TypeName);
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

        public static IAction BuildFromEditorItems(IReadOnlyList<UnityHFSM.HfsmBehaviorItem> items)
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

        private static void ValidateChildCount(UnityHFSM.HfsmBehaviorItem item, BehaviorCategory category)
        {
            switch (category)
            {
                case BehaviorCategory.Primitive when item.childIds.Count != 0:
                    throw new InvalidOperationException($"Primitive behavior '{item.id}' cannot have children.");
                case BehaviorCategory.Composite when item.childIds.Count == 0:
                    throw new InvalidOperationException($"Composite behavior '{item.id}' must have at least one child.");
                case BehaviorCategory.Decorator when item.childIds.Count != 1:
                    throw new InvalidOperationException($"Decorator behavior '{item.id}' must have exactly one child.");
            }
        }

        private static void Visit(
            string id,
            IReadOnlyDictionary<string, UnityHFSM.HfsmBehaviorItem> items,
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
            if (!HfsmBehaviorTypeRegistry.IsInitialized)
                HfsmBehaviorTypeRegistry.Initialize();
        }
    }
}

namespace UnityHFSM
{
    public enum HfsmBehaviorParameterType
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
    public sealed class HfsmBehaviorParameter
    {
        public string name;

        [SerializeField]
        private HfsmBehaviorParameterType valueType;

        public HfsmBehaviorParameterType ValueType
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

        public HfsmBehaviorParameter()
        {
        }

        public HfsmBehaviorParameter(string name, float value)
        {
            this.name = name;
            floatValue = value;
            ValueType = HfsmBehaviorParameterType.Float;
        }

        public HfsmBehaviorParameter(string name, int value)
        {
            this.name = name;
            intValue = value;
            ValueType = HfsmBehaviorParameterType.Int;
        }

        public HfsmBehaviorParameter(string name, bool value)
        {
            this.name = name;
            boolValue = value;
            ValueType = HfsmBehaviorParameterType.Bool;
        }

        public HfsmBehaviorParameter(string name, string value)
        {
            this.name = name;
            stringValue = value;
            ValueType = HfsmBehaviorParameterType.String;
        }

        public HfsmBehaviorParameter(string name, UnityEngine.Object value)
        {
            this.name = name;
            objectValue = value;
            ValueType = HfsmBehaviorParameterType.Object;
        }

        public HfsmBehaviorParameter(string name, Vector3 value)
        {
            this.name = name;
            vector3Value = value;
            ValueType = HfsmBehaviorParameterType.Vector3;
        }

        public T GetValue<T>()
        {
            object value = ValueType switch
            {
                HfsmBehaviorParameterType.Float => floatValue,
                HfsmBehaviorParameterType.Int => intValue,
                HfsmBehaviorParameterType.Bool => boolValue,
                HfsmBehaviorParameterType.String => stringValue,
                HfsmBehaviorParameterType.Object => objectValue,
                HfsmBehaviorParameterType.Vector2 => vector2Value,
                HfsmBehaviorParameterType.Vector3 => vector3Value,
                HfsmBehaviorParameterType.Color => colorValue,
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
                HfsmBehaviorParameterType.Float => floatValue,
                HfsmBehaviorParameterType.Int => intValue,
                HfsmBehaviorParameterType.Bool => boolValue,
                HfsmBehaviorParameterType.String => stringValue,
                HfsmBehaviorParameterType.Object => objectValue,
                HfsmBehaviorParameterType.Vector2 => vector2Value,
                HfsmBehaviorParameterType.Vector3 => vector3Value,
                HfsmBehaviorParameterType.Color => colorValue,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    [Serializable]
    public sealed class HfsmBehaviorItem
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
        public List<HfsmBehaviorParameter> parameters = new List<HfsmBehaviorParameter>();
        public bool isExpanded = true;

        public HfsmBehaviorItem()
        {
            id = Guid.NewGuid().ToString();
            displayName = "New Behavior";
        }

        public HfsmBehaviorItem(string typeName, string displayName = null)
        {
            id = Guid.NewGuid().ToString();
            TypeName = typeName;
            this.displayName = displayName ?? GetDefaultDisplayName(typeName);
            SetupDefaultParameters(typeName);
        }

        public HfsmBehaviorParameter GetParameter(string parameterName)
        {
            return parameters.Find(parameter => parameter.name == parameterName);
        }

        public void SetParameter(string parameterName, float value) =>
            SetParameter(parameterName, HfsmBehaviorParameterType.Float, parameter => parameter.floatValue = value);

        public void SetParameter(string parameterName, int value) =>
            SetParameter(parameterName, HfsmBehaviorParameterType.Int, parameter => parameter.intValue = value);

        public void SetParameter(string parameterName, bool value) =>
            SetParameter(parameterName, HfsmBehaviorParameterType.Bool, parameter => parameter.boolValue = value);

        public void SetParameter(string parameterName, string value) =>
            SetParameter(parameterName, HfsmBehaviorParameterType.String, parameter => parameter.stringValue = value);

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

        public HfsmBehaviorItem Clone()
        {
            var clone = new HfsmBehaviorItem
            {
                id = Guid.NewGuid().ToString(),
                displayName = displayName,
                TypeName = TypeName,
                isExpanded = isExpanded
            };

            foreach (var parameter in parameters)
            {
                clone.parameters.Add(new HfsmBehaviorParameter
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
            HfsmBehaviorParameterType parameterType,
            Action<HfsmBehaviorParameter> setValue)
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
            return HfsmBehaviorTypeRegistry.GetDefinition(behaviorTypeName)?.displayName ?? behaviorTypeName;
        }

        private void SetupDefaultParameters(string behaviorTypeName)
        {
            EnsureRegistryInitialized();
            var definition = HfsmBehaviorTypeRegistry.GetDefinition(behaviorTypeName);
            if (definition == null)
                return;

            foreach (var parameterDefinition in definition.parameters)
            {
                var parameter = new HfsmBehaviorParameter
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
            return HfsmBehaviorTypeRegistry.GetCategory(TypeName);
        }

        private static void EnsureRegistryInitialized()
        {
            if (!HfsmBehaviorTypeRegistry.IsInitialized)
                HfsmBehaviorTypeRegistry.Initialize();
        }
    }
}
