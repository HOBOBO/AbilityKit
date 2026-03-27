using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityHFSM.Actions
{
    /// <summary>
    /// 行为树构建器，从配置构建行为树
    /// </summary>
    public static class BehaviorTreeBuilder
    {
        /// <summary>
        /// 从行为项列表构建行为树
        /// </summary>
        public static IAction Build(List<BehaviorItemConfig> items, string rootId)
        {
            if (items == null || items.Count == 0)
                return null;

            // 1. 创建所有 IAction 实例
            var actionMap = new Dictionary<string, IAction>();
            foreach (var item in items)
            {
                var action = CreateAction(item);
                if (action != null)
                {
                    action.Name = item.name;
                    actionMap[item.id] = action;
                }
            }

            // 2. 建立父子关系
            foreach (var item in items)
            {
                if (!actionMap.TryGetValue(item.id, out var action))
                    continue;

                if (item is CompositeBehaviorItemConfig composite && composite.childIds != null)
                {
                    var compositeAction = action as CompositeActionBase;
                    if (compositeAction != null)
                    {
                        foreach (var childId in composite.childIds)
                        {
                            if (actionMap.TryGetValue(childId, out var childAction))
                            {
                                compositeAction.AddChild(childAction);
                            }
                        }
                    }
                }
                else if (item is DecoratorBehaviorItemConfig decorator && decorator.childId != null)
                {
                    var decoratorAction = action as DecoratorActionBase;
                    if (decoratorAction != null && actionMap.TryGetValue(decorator.childId, out var childAction))
                    {
                        decoratorAction.SetChild(childAction);
                    }
                }
            }

            // 3. 返回根节点
            if (string.IsNullOrEmpty(rootId) || !actionMap.TryGetValue(rootId, out var root))
                return null;

            return root;
        }

        /// <summary>
        /// 从 HfsmBehaviorItem 列表构建行为树
        /// </summary>
        public static IAction BuildFromEditorItems(List<UnityHFSM.HfsmBehaviorItem> items, string rootId)
        {
            if (items == null || items.Count == 0)
                return null;

            // 1. 创建所有 IAction 实例
            var actionMap = new Dictionary<string, IAction>();
            foreach (var item in items)
            {
                var action = CreateActionFromEditorItem(item);
                if (action != null)
                {
                    action.Name = item.displayName;
                    actionMap[item.id] = action;
                }
            }

            // 2. 建立父子关系
            foreach (var item in items)
            {
                if (!actionMap.TryGetValue(item.id, out var action))
                    continue;

                if (action is CompositeActionBase compositeAction)
                {
                    foreach (var childId in item.childIds)
                    {
                        if (actionMap.TryGetValue(childId, out var childAction))
                        {
                            compositeAction.AddChild(childAction);
                        }
                    }
                }
                else if (action is DecoratorActionBase decoratorAction)
                {
                    if (item.childIds.Count > 0 && actionMap.TryGetValue(item.childIds[0], out var childAction))
                    {
                        decoratorAction.SetChild(childAction);
                    }
                }
            }

            // 3. 返回根节点
            if (string.IsNullOrEmpty(rootId) || !actionMap.TryGetValue(rootId, out var root))
                return null;

            return root;
        }

        /// <summary>
        /// 从 HfsmBehaviorItem 构建行为树，自动找到根节点
        /// </summary>
        public static IAction BuildFromEditorItems(List<UnityHFSM.HfsmBehaviorItem> items)
        {
            if (items == null || items.Count == 0)
                return null;

            // 找到根节点（没有父节点的节点）
            string rootId = null;
            var allIds = new HashSet<string>();
            var childIds = new HashSet<string>();

            foreach (var item in items)
            {
                allIds.Add(item.id);
                foreach (var childId in item.childIds)
                {
                    childIds.Add(childId);
                }
            }

            foreach (var id in allIds)
            {
                if (!childIds.Contains(id))
                {
                    rootId = id;
                    break;
                }
            }

            if (string.IsNullOrEmpty(rootId))
                rootId = items[0].id;

            return BuildFromEditorItems(items, rootId);
        }

        private static IAction CreateActionFromEditorItem(UnityHFSM.HfsmBehaviorItem item)
        {
            if (item == null)
                return null;

            // 确保注册表已初始化
            if (!HfsmBehaviorTypeRegistry.IsInitialized)
            {
                HfsmBehaviorTypeRegistry.Initialize();
            }

            // 使用注册表创建并配置行为
            string typeName = item.TypeName;
            return HfsmBehaviorTypeRegistry.CreateAndConfigure(typeName, item);
        }

        private static IAction CreateAction(BehaviorItemConfig item)
        {
            switch (item.type)
            {
                // Primitive
                case BehaviorType.Wait:
                    return new WaitAction(item.GetFloat("duration", 1f));
                case BehaviorType.WaitUntil:
                    return new WaitUntilAction();
                case BehaviorType.Log:
                    return new LogAction(item.GetString("message", ""));
                case BehaviorType.SetFloat:
                    return new SetFloatAction(item.GetString("variableName", ""), item.GetFloat("value", 0f));
                case BehaviorType.SetBool:
                    return new SetBoolAction(item.GetString("variableName", ""), item.GetBool("value", false));
                case BehaviorType.SetInt:
                    return new SetIntAction(item.GetString("variableName", ""), item.GetInt("value", 0));
                case BehaviorType.PlayAnimation:
                    return new PlayAnimationAction(item.GetString("stateName", ""), item.GetFloat("crossFadeDuration", 0.1f));
                case BehaviorType.SetActive:
                    return new SetActiveAction();

                // Composite
                case BehaviorType.Sequence:
                    return new SequenceAction();
                case BehaviorType.Selector:
                    return new SelectorAction();
                case BehaviorType.Parallel:
                    return new ParallelAction(null, item.GetBool("failOnAnyFailure", false));
                case BehaviorType.RandomSelector:
                    return new RandomSelectorAction();

                // Decorator
                case BehaviorType.Repeat:
                    return new RepeatAction(null, item.GetInt("count", -1));
                case BehaviorType.Invert:
                    return new InvertAction();
                case BehaviorType.TimeLimit:
                    return new TimeLimitAction(null, item.GetFloat("timeLimit", 5f));
                case BehaviorType.UntilSuccess:
                    return new UntilSuccessAction();
                case BehaviorType.UntilFailure:
                    return new UntilFailureAction();
                case BehaviorType.Cooldown:
                    return new CooldownAction(null, item.GetFloat("cooldownDuration", 1f));
                case BehaviorType.If:
                    return new IfAction();

                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// 行为类型枚举
    /// </summary>
    public enum BehaviorType
    {
        // Primitive Actions
        Wait,
        WaitUntil,
        Log,
        SetFloat,
        SetBool,
        SetInt,
        PlayAnimation,
        SetActive,
        MoveTo,

        // Composite Actions
        Sequence,
        Selector,
        Parallel,
        RandomSelector,
        RandomSequence,

        // Decorator Actions
        Repeat,
        Invert,
        TimeLimit,
        UntilSuccess,
        UntilFailure,
        Cooldown,
        If
    }

    /// <summary>
    /// 行为配置基类
    /// </summary>
    [System.Serializable]
    public class BehaviorItemConfig
    {
        public string id;
        public string name;
        public BehaviorType type;

        [SerializeField]
        private List<ParameterEntry> parameters = new List<ParameterEntry>();

        public string GetString(string key, string defaultValue = "")
        {
            var param = parameters.Find(p => p.name == key);
            return param?.stringValue ?? defaultValue;
        }

        public float GetFloat(string key, float defaultValue = 0f)
        {
            var param = parameters.Find(p => p.name == key);
            return param?.floatValue ?? defaultValue;
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            var param = parameters.Find(p => p.name == key);
            return param?.intValue ?? defaultValue;
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            var param = parameters.Find(p => p.name == key);
            return param?.boolValue ?? defaultValue;
        }

        public T GetObject<T>(string key) where T : UnityEngine.Object
        {
            var param = parameters.Find(p => p.name == key);
            return param?.objectValue as T;
        }
    }

    [System.Serializable]
    public class ParameterEntry
    {
        public string name;
        public ParameterType type;
        public string stringValue;
        public float floatValue;
        public int intValue;
        public bool boolValue;
        public UnityEngine.Object objectValue;

        public enum ParameterType
        {
            Float,
            Int,
            Bool,
            String,
            Object
        }
    }

    /// <summary>
    /// 复合行为配置
    /// </summary>
    [System.Serializable]
    public class CompositeBehaviorItemConfig : BehaviorItemConfig
    {
        public List<string> childIds = new List<string>();
    }

    /// <summary>
    /// 修饰器行为配置
    /// </summary>
    [System.Serializable]
    public class DecoratorBehaviorItemConfig : BehaviorItemConfig
    {
        public string childId;
    }

    /// <summary>
    /// 复合行为基类（用于构建器）
    /// </summary>
    public abstract class CompositeActionBase : ActionBase
    {
        protected List<IAction> children = new List<IAction>();

        public void AddChild(IAction child)
        {
            if (child != null)
                children.Add(child);
        }

        public void RemoveChild(IAction child)
        {
            children.Remove(child);
        }

        public void ClearChildren()
        {
            children.Clear();
        }

        public int ChildCount => children.Count;
    }

    /// <summary>
    /// 修饰器行为基类（用于构建器）
    /// </summary>
    public abstract class DecoratorActionBase : ActionBase
    {
        protected IAction child;

        public void SetChild(IAction child)
        {
            this.child = child;
        }
    }
}

namespace UnityHFSM
{
    /// <summary>
    /// 行为项参数类型
    /// </summary>
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

    /// <summary>
    /// 行为项参数值（可序列化）
    /// </summary>
    [System.Serializable]
    public class HfsmBehaviorParameter
    {
        public string name;

        [UnityEngine.SerializeField]
        private int typeIndex;

        public HfsmBehaviorParameterType ValueType
        {
            get => (HfsmBehaviorParameterType)typeIndex;
            set => typeIndex = (int)value;
        }

        public float floatValue;
        public int intValue;
        public bool boolValue;
        public string stringValue;
        public UnityEngine.Object objectValue;
        public UnityEngine.Vector2 vector2Value;
        public UnityEngine.Vector3 vector3Value;
        public UnityEngine.Color colorValue;

        public HfsmBehaviorParameter() { }

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

        public HfsmBehaviorParameter(string name, UnityEngine.Vector3 value)
        {
            this.name = name;
            vector3Value = value;
            ValueType = HfsmBehaviorParameterType.Vector3;
        }

        public T GetValue<T>()
        {
            if (typeof(T) == typeof(float))
                return (T)(object)floatValue;
            if (typeof(T) == typeof(int))
                return (T)(object)intValue;
            if (typeof(T) == typeof(bool))
                return (T)(object)boolValue;
            if (typeof(T) == typeof(string))
                return (T)(object)stringValue;
            if (typeof(T) == typeof(UnityEngine.Object) || (typeof(T).IsSubclassOf(typeof(UnityEngine.Object))))
                return (T)(object)objectValue;
            return default(T);
        }

        /// <summary>
        /// 获取值作为 object 类型（用于导出）
        /// </summary>
        public object GetValueAsObject()
        {
            switch (ValueType)
            {
                case HfsmBehaviorParameterType.Float:
                    return floatValue;
                case HfsmBehaviorParameterType.Int:
                    return intValue;
                case HfsmBehaviorParameterType.Bool:
                    return boolValue;
                case HfsmBehaviorParameterType.String:
                    return stringValue;
                case HfsmBehaviorParameterType.Vector2:
                    return new { x = vector2Value.x, y = vector2Value.y };
                case HfsmBehaviorParameterType.Vector3:
                    return new { x = vector3Value.x, y = vector3Value.y, z = vector3Value.z };
                case HfsmBehaviorParameterType.Color:
                    return new { r = colorValue.r, g = colorValue.g, b = colorValue.b, a = colorValue.a };
                case HfsmBehaviorParameterType.Object:
                    return objectValue != null ? objectValue.name : null;
                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// 行为类型
    /// </summary>
    public enum HfsmBehaviorType
    {
        // ========== 原子行为 ==========
        Wait,
        WaitUntil,
        Log,
        SetFloat,
        SetBool,
        SetInt,
        PlayAnimation,
        SetActive,
        MoveTo,

        // ========== 复合行为 ==========
        Sequence,
        Selector,
        Parallel,
        RandomSelector,
        RandomSequence,

        // ========== 修饰器行为 ==========
        Repeat,
        Invert,
        TimeLimit,
        UntilSuccess,
        UntilFailure,
        Cooldown,
        If
    }

    /// <summary>
    /// 行为项 - 可序列化的行为配置
    /// </summary>
    [System.Serializable]
    public class HfsmBehaviorItem
    {
        public string id;
        public string displayName;

        [UnityEngine.SerializeField]
        private int typeIndex;

        /// <summary>
        /// 兼容旧版本的枚举类型（已弃用，推荐使用 TypeName）
        /// </summary>
        [Obsolete("Use TypeName instead for better extensibility.")]
        public HfsmBehaviorType Type
        {
            get => (HfsmBehaviorType)typeIndex;
            set => typeIndex = (int)value;
        }

        /// <summary>
        /// 行为类型名称（用于注册表查找）
        /// 支持包外扩展
        /// </summary>
        public string TypeName
        {
            get
            {
                // 如果注册表已初始化且有对应的类型名，直接返回
                if (HfsmBehaviorTypeRegistry.IsInitialized)
                {
                    // 从枚举值转换为类型名
                    return EnumToTypeName((HfsmBehaviorType)typeIndex);
                }
                // 否则使用旧的枚举名称
                return EnumToTypeName((HfsmBehaviorType)typeIndex);
            }
            set
            {
                // 将类型名转换回枚举索引
                typeIndex = TypeNameToIndex(value);
            }
        }

        public string parentId;
        public List<string> childIds = new List<string>();
        public List<HfsmBehaviorParameter> parameters = new List<HfsmBehaviorParameter>();
        public bool isExpanded = true;

        public HfsmBehaviorItem()
        {
            id = Guid.NewGuid().ToString();
            displayName = "New Behavior";
            typeIndex = 0;
        }

        public HfsmBehaviorItem(HfsmBehaviorType type, string displayName = null)
        {
            id = Guid.NewGuid().ToString();
            Type = type;
            this.displayName = displayName ?? GetDefaultDisplayName(type);
            SetupDefaultParameters();
        }

        /// <summary>
        /// 使用类型名称创建行为项
        /// </summary>
        public HfsmBehaviorItem(string typeName, string displayName = null)
        {
            id = Guid.NewGuid().ToString();
            TypeName = typeName;
            this.displayName = displayName ?? GetDefaultDisplayNameFromTypeName(typeName);
            SetupDefaultParametersFromTypeName(typeName);
        }

        private static string EnumToTypeName(HfsmBehaviorType type)
        {
            return type.ToString();
        }

        private static int TypeNameToIndex(string typeName)
        {
            // 如果注册表已初始化，从注册表获取枚举值
            if (HfsmBehaviorTypeRegistry.IsInitialized && HfsmBehaviorTypeRegistry.IsRegistered(typeName))
            {
                return (int)Enum.Parse(typeof(HfsmBehaviorType), typeName);
            }
            // 否则尝试直接解析
            if (Enum.TryParse<HfsmBehaviorType>(typeName, out var result))
            {
                return (int)result;
            }
            return 0;
        }

        private static string GetDefaultDisplayName(HfsmBehaviorType type)
        {
            // 优先从注册表获取
            if (HfsmBehaviorTypeRegistry.IsInitialized)
            {
                var def = HfsmBehaviorTypeRegistry.GetDefinition(type.ToString());
                if (def != null)
                    return def.displayName;
            }
            return type switch
            {
                HfsmBehaviorType.Wait => "Wait",
                HfsmBehaviorType.WaitUntil => "Wait Until",
                HfsmBehaviorType.Log => "Log",
                HfsmBehaviorType.SetFloat => "Set Float",
                HfsmBehaviorType.SetBool => "Set Bool",
                HfsmBehaviorType.SetInt => "Set Int",
                HfsmBehaviorType.PlayAnimation => "Play Animation",
                HfsmBehaviorType.SetActive => "Set Active",
                HfsmBehaviorType.MoveTo => "Move To",
                HfsmBehaviorType.Sequence => "Sequence",
                HfsmBehaviorType.Selector => "Selector",
                HfsmBehaviorType.Parallel => "Parallel",
                HfsmBehaviorType.RandomSelector => "Random Selector",
                HfsmBehaviorType.RandomSequence => "Random Sequence",
                HfsmBehaviorType.Repeat => "Repeat",
                HfsmBehaviorType.Invert => "Invert",
                HfsmBehaviorType.TimeLimit => "Time Limit",
                HfsmBehaviorType.UntilSuccess => "Until Success",
                HfsmBehaviorType.UntilFailure => "Until Failure",
                HfsmBehaviorType.Cooldown => "Cooldown",
                HfsmBehaviorType.If => "If",
                _ => type.ToString()
            };
        }

        /// <summary>
        /// 根据类型名称获取默认显示名称
        /// </summary>
        private static string GetDefaultDisplayNameFromTypeName(string typeName)
        {
            // 优先从注册表获取
            if (HfsmBehaviorTypeRegistry.IsInitialized)
            {
                var def = HfsmBehaviorTypeRegistry.GetDefinition(typeName);
                if (def != null)
                    return def.displayName;
            }
            // 回退到枚举
            if (Enum.TryParse<HfsmBehaviorType>(typeName, out var type))
            {
                return GetDefaultDisplayName(type);
            }
            return typeName;
        }

        /// <summary>
        /// 根据类型名称设置默认参数
        /// </summary>
        private void SetupDefaultParametersFromTypeName(string typeName)
        {
            parameters.Clear();

            // 优先从注册表获取参数定义
            if (HfsmBehaviorTypeRegistry.IsInitialized)
            {
                var def = HfsmBehaviorTypeRegistry.GetDefinition(typeName);
                if (def != null)
                {
                    foreach (var paramDef in def.parameters)
                    {
                        var param = new HfsmBehaviorParameter
                        {
                            name = paramDef.name,
                            ValueType = paramDef.valueType
                        };
                        // 从 JSON 恢复默认值
                        RestoreDefaultValue(param, paramDef.defaultValueJson);
                        parameters.Add(param);
                    }
                    return;
                }
            }

            // 回退到旧的 switch 逻辑
            if (Enum.TryParse<HfsmBehaviorType>(typeName, out var type))
            {
                SetupDefaultParametersForEnum(type);
            }
        }

        private static void RestoreDefaultValue(HfsmBehaviorParameter param, string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var jsonValue = UnityEngine.JsonUtility.FromJson<JsonValue>(json);
                if (jsonValue?.value == null) return;

                switch (param.ValueType)
                {
                    case HfsmBehaviorParameterType.Float:
                        param.floatValue = Convert.ToSingle(jsonValue.value);
                        break;
                    case HfsmBehaviorParameterType.Int:
                        param.intValue = Convert.ToInt32(jsonValue.value);
                        break;
                    case HfsmBehaviorParameterType.Bool:
                        param.boolValue = Convert.ToBoolean(jsonValue.value);
                        break;
                    case HfsmBehaviorParameterType.String:
                        param.stringValue = jsonValue.value?.ToString() ?? "";
                        break;
                }
            }
            catch { }
        }

        private void SetupDefaultParameters()
        {
            // 使用新的基于类型名称的逻辑
            SetupDefaultParametersFromTypeName(TypeName);
        }

        private void SetupDefaultParametersForEnum(HfsmBehaviorType type)
        {
            parameters.Clear();

            switch (Type)
            {
                case HfsmBehaviorType.Wait:
                    parameters.Add(new HfsmBehaviorParameter("duration", 1f));
                    break;

                case HfsmBehaviorType.Log:
                    parameters.Add(new HfsmBehaviorParameter("message", ""));
                    break;

                case HfsmBehaviorType.SetFloat:
                    parameters.Add(new HfsmBehaviorParameter("variableName", ""));
                    parameters.Add(new HfsmBehaviorParameter("value", 0f));
                    break;

                case HfsmBehaviorType.SetBool:
                    parameters.Add(new HfsmBehaviorParameter("variableName", ""));
                    parameters.Add(new HfsmBehaviorParameter("value", false));
                    break;

                case HfsmBehaviorType.SetInt:
                    parameters.Add(new HfsmBehaviorParameter("variableName", ""));
                    parameters.Add(new HfsmBehaviorParameter("value", 0));
                    break;

                case HfsmBehaviorType.PlayAnimation:
                    parameters.Add(new HfsmBehaviorParameter("stateName", ""));
                    parameters.Add(new HfsmBehaviorParameter("crossFadeDuration", 0.1f));
                    break;

                case HfsmBehaviorType.SetActive:
                    parameters.Add(new HfsmBehaviorParameter("target", (UnityEngine.Object)null));
                    parameters.Add(new HfsmBehaviorParameter("active", true));
                    break;

                case HfsmBehaviorType.MoveTo:
                    parameters.Add(new HfsmBehaviorParameter("target", (UnityEngine.Object)null));
                    parameters.Add(new HfsmBehaviorParameter("destination", UnityEngine.Vector3.zero));
                    parameters.Add(new HfsmBehaviorParameter("speed", 5f));
                    break;

                case HfsmBehaviorType.Repeat:
                    parameters.Add(new HfsmBehaviorParameter("count", -1));
                    break;

                case HfsmBehaviorType.TimeLimit:
                    parameters.Add(new HfsmBehaviorParameter("timeLimit", 5f));
                    break;

                case HfsmBehaviorType.Cooldown:
                    parameters.Add(new HfsmBehaviorParameter("cooldownDuration", 1f));
                    break;

                case HfsmBehaviorType.Parallel:
                    parameters.Add(new HfsmBehaviorParameter("failOnAnyFailure", false));
                    break;
            }
        }

        public HfsmBehaviorParameter GetParameter(string name)
        {
            return parameters.Find(p => p.name == name);
        }

        public void SetParameter(string name, float value)
        {
            var param = GetParameter(name);
            if (param != null)
            {
                param.floatValue = value;
                param.ValueType = HfsmBehaviorParameterType.Float;
            }
        }

        public void SetParameter(string name, int value)
        {
            var param = GetParameter(name);
            if (param != null)
            {
                param.intValue = value;
                param.ValueType = HfsmBehaviorParameterType.Int;
            }
        }

        public void SetParameter(string name, bool value)
        {
            var param = GetParameter(name);
            if (param != null)
            {
                param.boolValue = value;
                param.ValueType = HfsmBehaviorParameterType.Bool;
            }
        }

        public void SetParameter(string name, string value)
        {
            var param = GetParameter(name);
            if (param != null)
            {
                param.stringValue = value;
                param.ValueType = HfsmBehaviorParameterType.String;
            }
        }

        public bool IsComposite
        {
            get
            {
                if (HfsmBehaviorTypeRegistry.IsInitialized)
                {
                    return HfsmBehaviorTypeRegistry.GetCategory(TypeName) == BehaviorCategory.Composite;
                }
                return Type >= HfsmBehaviorType.Sequence && Type <= HfsmBehaviorType.RandomSequence;
            }
        }

        public bool IsDecorator
        {
            get
            {
                if (HfsmBehaviorTypeRegistry.IsInitialized)
                {
                    return HfsmBehaviorTypeRegistry.GetCategory(TypeName) == BehaviorCategory.Decorator;
                }
                return Type >= HfsmBehaviorType.Repeat;
            }
        }

        public string GetDescription()
        {
            switch (Type)
            {
                case HfsmBehaviorType.Wait:
                    return $"Wait {GetParamValue<float>("duration")}s";
                case HfsmBehaviorType.Log:
                    return $"Log: {GetParamValue<string>("message")}";
                case HfsmBehaviorType.SetFloat:
                    return $"{GetParamValue<string>("variableName")} = {GetParamValue<float>("value")}";
                case HfsmBehaviorType.SetBool:
                    return $"{GetParamValue<string>("variableName")} = {GetParamValue<bool>("value")}";
                case HfsmBehaviorType.SetInt:
                    return $"{GetParamValue<string>("variableName")} = {GetParamValue<int>("value")}";
                case HfsmBehaviorType.PlayAnimation:
                    return $"Play: {GetParamValue<string>("stateName")}";
                case HfsmBehaviorType.Repeat:
                    var count = GetParamValue<int>("count");
                    return count < 0 ? "Repeat (Infinite)" : $"Repeat x{count}";
                case HfsmBehaviorType.Sequence:
                    return $"Sequence [{childIds.Count}]";
                case HfsmBehaviorType.Selector:
                    return $"Selector [{childIds.Count}]";
                case HfsmBehaviorType.Parallel:
                    return $"Parallel [{childIds.Count}]";
                default:
                    return Type.ToString();
            }
        }

        public T GetParamValue<T>(string name)
        {
            var param = GetParameter(name);
            if (param != null)
            {
                return param.GetValue<T>();
            }
            return default;
        }

        public HfsmBehaviorItem Clone()
        {
            var clone = new HfsmBehaviorItem
            {
                id = Guid.NewGuid().ToString(),
                displayName = displayName,
                Type = Type,
                parentId = null,
                isExpanded = isExpanded
            };

            foreach (var param in parameters)
            {
                var newParam = new HfsmBehaviorParameter
                {
                    name = param.name,
                    floatValue = param.floatValue,
                    intValue = param.intValue,
                    boolValue = param.boolValue,
                    stringValue = param.stringValue,
                    objectValue = param.objectValue,
                    vector2Value = param.vector2Value,
                    vector3Value = param.vector3Value,
                    colorValue = param.colorValue
                };
                newParam.ValueType = param.ValueType;
                clone.parameters.Add(newParam);
            }

            return clone;
        }
    }
}
