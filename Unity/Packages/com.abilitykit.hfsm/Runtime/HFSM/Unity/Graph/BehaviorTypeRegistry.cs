// ============================================================================
// BehaviorTypeRegistry - 行为类型注册表
// 支持包外扩展行为类型，无需修改枚举
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using IAction = AbilityKit.HFSM.Actions.IAction;
using ICompositeAction = AbilityKit.HFSM.Actions.ICompositeAction;
using IDecoratorAction = AbilityKit.HFSM.Actions.IDecoratorAction;


namespace AbilityKit.HFSM
{

    /// <summary>
    /// 行为类型注册表 - 替代枚举，支持运行时扩展
    /// </summary>
    public static class BehaviorTypeRegistry
    {
        private static readonly Dictionary<string, BehaviorTypeDefinition> _types = new Dictionary<string, BehaviorTypeDefinition>();
        private static readonly Dictionary<string, Action<IAction, BehaviorItem>> _factories = new Dictionary<string, Action<IAction, BehaviorItem>>();
        private static bool _isInitialized = false;

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public static bool IsInitialized => _isInitialized;

        /// <summary>
        /// 获取所有已注册的行为类型
        /// </summary>
        public static IEnumerable<BehaviorTypeDefinition> AllTypes => _types.Values;

        /// <summary>
        /// 获取所有类型名称
        /// </summary>
        public static IEnumerable<string> AllTypeNames => _types.Keys;

        /// <summary>
        /// 获取指定分类的所有类型
        /// </summary>
        public static IEnumerable<BehaviorTypeDefinition> GetByCategory(BehaviorCategory category)
        {
            foreach (var type in _types.Values)
            {
                if (type.category == category)
                    yield return type;
            }
        }

        /// <summary>
        /// 根据类型名称获取定义
        /// </summary>
        public static BehaviorTypeDefinition GetDefinition(string typeName)
        {
            return _types.TryGetValue(typeName, out var def) ? def : null;
        }

        /// <summary>
        /// 检查类型是否已注册
        /// </summary>
        public static bool IsRegistered(string typeName)
        {
            return _types.ContainsKey(typeName);
        }

        /// <summary>
        /// 获取行为分类
        /// </summary>
        public static BehaviorCategory GetCategory(string typeName)
        {
            if (_types.TryGetValue(typeName, out var def))
                return def.category;
            throw new InvalidOperationException($"Unknown behavior type '{typeName}'.");
        }

        /// <summary>
        /// 注册行为类型
        /// </summary>
        /// <typeparam name="TAction">行为类类型</typeparam>
        /// <param name="typeName">类型唯一标识</param>
        /// <param name="displayName">显示名称</param>
        /// <param name="category">分类</param>
        /// <param name="categoryName">分类显示名</param>
        /// <param name="description">描述</param>
        /// <param name="parameters">参数定义</param>
        /// <param name="factory">工厂方法，用于从配置创建实例</param>
        public static void Register<TAction>(
            string typeName,
            string displayName,
            BehaviorCategory category,
            string categoryName = null,
            string description = null,
            IEnumerable<BehaviorParameterDefinition> parameters = null,
            Action<IAction, BehaviorItem> factory = null,
            int minChildren = -1,
            int maxChildren = -1)
            where TAction : AbilityKit.HFSM.Actions.IAction, new()
        {
            RegisterInternal(
                typeName,
                displayName,
                category,
                categoryName,
                description,
                parameters,
                typeof(TAction),
                factory,
                minChildren,
                maxChildren
            );
        }

        /// <summary>
        /// 注册行为类型（使用现有类型）
        /// </summary>
        public static void Register(
            string typeName,
            string displayName,
            BehaviorCategory category,
            Type actionType,
            string categoryName = null,
            string description = null,
            IEnumerable<BehaviorParameterDefinition> parameters = null,
            Action<IAction, BehaviorItem> factory = null,
            int minChildren = -1,
            int maxChildren = -1)
        {
            RegisterInternal(
                typeName,
                displayName,
                category,
                categoryName,
                description,
                parameters,
                actionType,
                factory,
                minChildren,
                maxChildren
            );
        }

        private static void RegisterInternal(
            string typeName,
            string displayName,
            BehaviorCategory category,
            string categoryName,
            string description,
            IEnumerable<BehaviorParameterDefinition> parameters,
            Type actionType,
            Action<IAction, BehaviorItem> factory,
            int minChildren,
            int maxChildren)
        {
            if (string.IsNullOrEmpty(typeName))
                throw new ArgumentException("typeName cannot be null or empty", nameof(typeName));
            if (actionType == null || !typeof(IAction).IsAssignableFrom(actionType))
                throw new ArgumentException($"Behavior type '{typeName}' must implement {nameof(IAction)}.", nameof(actionType));
            if (category == BehaviorCategory.Composite && !typeof(ICompositeAction).IsAssignableFrom(actionType))
                throw new ArgumentException($"Composite behavior type '{typeName}' must implement {nameof(ICompositeAction)}.", nameof(actionType));
            if (category == BehaviorCategory.Decorator && !typeof(IDecoratorAction).IsAssignableFrom(actionType))
                throw new ArgumentException($"Decorator behavior type '{typeName}' must implement {nameof(IDecoratorAction)}.", nameof(actionType));

            if (_types.ContainsKey(typeName))
                throw new InvalidOperationException($"Behavior type '{typeName}' is already registered.");

            var definition = new BehaviorTypeDefinition(typeName, displayName, category, categoryName, description)
            {
                actionType = actionType,
                minChildren = minChildren < 0 ? (category == BehaviorCategory.Primitive ? 0 : 1) : minChildren,
                maxChildren = maxChildren < 0 ? (category == BehaviorCategory.Composite ? -1 : 1) : maxChildren
            };

            if (parameters != null)
            {
                definition.parameters.AddRange(parameters);
            }

            _types[typeName] = definition;

            if (factory != null)
            {
                _factories[typeName] = factory;
            }
            else
            {
                // 使用默认工厂
                _factories[typeName] = CreateDefaultFactory(actionType);
            }
        }

        /// <summary>
        /// 注册包外扩展行为类型
        /// 在包外调用此方法注册自定义行为
        /// </summary>
        public static void RegisterExternal<TAction>(
            string typeName,
            string displayName,
            BehaviorCategory category,
            string categoryName = null,
            string description = null,
            IEnumerable<BehaviorParameterDefinition> parameters = null,
            Action<IAction, BehaviorItem> factory = null,
            int minChildren = -1,
            int maxChildren = -1)
            where TAction : AbilityKit.HFSM.Actions.IAction, new()
        {
            Register<TAction>(typeName, displayName, category, categoryName, description, parameters, factory, minChildren, maxChildren);
        }

        /// <summary>
        /// 创建行为实例
        /// </summary>
        public static AbilityKit.HFSM.Actions.IAction CreateInstance(string typeName)
        {
            if (!_types.TryGetValue(typeName, out var definition))
                throw new InvalidOperationException($"Unknown behavior type '{typeName}'.");

            if (definition.actionType == null)
                throw new InvalidOperationException($"Behavior type '{typeName}' has no associated action type.");

            try
            {
                return Activator.CreateInstance(definition.actionType) as AbilityKit.HFSM.Actions.IAction;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Failed to create behavior type '{typeName}'.", e);
            }
        }

        /// <summary>
        /// 从行为项创建并配置行为实例
        /// </summary>
        public static AbilityKit.HFSM.Actions.IAction CreateAndConfigure(string typeName, BehaviorItem item)
        {
            var action = CreateInstance(typeName);

            // 使用工厂方法配置参数
            if (_factories.TryGetValue(typeName, out var factory))
            {
                try
                {
                    factory(action, item);
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException($"Failed to configure behavior type '{typeName}'.", e);
                }
            }

            return action;
        }

        /// <summary>
        /// 获取所有分类名称
        /// </summary>
        public static IEnumerable<string> GetAllCategories()
        {
            var categories = new HashSet<string>();
            foreach (var type in _types.Values)
            {
                if (!string.IsNullOrEmpty(type.categoryName))
                    categories.Add(type.categoryName);
            }
            return categories;
        }

        /// <summary>
        /// 获取指定分类的所有类型名称
        /// </summary>
        public static IEnumerable<string> GetTypeNamesByCategory(string categoryName)
        {
            foreach (var type in _types.Values)
            {
                if (type.categoryName == categoryName)
                    yield return type.typeName;
            }
        }

        /// <summary>
        /// 初始化内置行为类型
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized)
                return;

            RegisterBuiltInTypes();
            _isInitialized = true;
        }

        /// <summary>
        /// 重置注册表（主要用于测试）
        /// </summary>
        public static void Reset()
        {
            _types.Clear();
            _factories.Clear();
            _isInitialized = false;
        }

        /// <summary>
        /// 注册内置行为类型
        /// </summary>
        private static void RegisterBuiltInTypes()
        {
            // ========== 原子行为 ==========
            Register<AbilityKit.HFSM.Actions.WaitAction>(
                "Wait",
                "等待",
                BehaviorCategory.Primitive,
                "基础行为",
                "等待指定时间",
                new[]
                {
                    new BehaviorParameterDefinition("duration", BehaviorParameterType.Float, "持续时间", "等待的秒数", 1f)
                },
                (action, item) =>
                {
                    var waitAction = (AbilityKit.HFSM.Actions.WaitAction)action;
                    waitAction.duration = item.GetParamValue<float>("duration");
                }
            );

            Register<AbilityKit.HFSM.Actions.LogAction>(
                "Log",
                "日志",
                BehaviorCategory.Primitive,
                "基础行为",
                "输出日志信息",
                new[]
                {
                    new BehaviorParameterDefinition("message", BehaviorParameterType.String, "消息", "日志内容", "")
                },
                (action, item) =>
                {
                    var logAction = (AbilityKit.HFSM.Actions.LogAction)action;
                    logAction.message = item.GetParamValue<string>("message");
                }
            );

            Register<AbilityKit.HFSM.Actions.SetFloatAction>(
                "SetFloat",
                "设置浮点数",
                BehaviorCategory.Primitive,
                "基础行为",
                "设置浮点参数值",
                new[]
                {
                    new BehaviorParameterDefinition("variableName", BehaviorParameterType.String, "变量名", "目标变量名称", ""),
                    new BehaviorParameterDefinition("value", BehaviorParameterType.Float, "值", "要设置的值", 0f)
                },
                (action, item) =>
                {
                    var setAction = (AbilityKit.HFSM.Actions.SetFloatAction)action;
                    setAction.variableName = item.GetParamValue<string>("variableName");
                    setAction.value = item.GetParamValue<float>("value");
                }
            );

            Register<AbilityKit.HFSM.Actions.SetBoolAction>(
                "SetBool",
                "设置布尔值",
                BehaviorCategory.Primitive,
                "基础行为",
                "设置布尔参数值",
                new[]
                {
                    new BehaviorParameterDefinition("variableName", BehaviorParameterType.String, "变量名", "目标变量名称", ""),
                    new BehaviorParameterDefinition("value", BehaviorParameterType.Bool, "值", "要设置的值", false)
                },
                (action, item) =>
                {
                    var setAction = (AbilityKit.HFSM.Actions.SetBoolAction)action;
                    setAction.variableName = item.GetParamValue<string>("variableName");
                    setAction.value = item.GetParamValue<bool>("value");
                }
            );

            Register<AbilityKit.HFSM.Actions.SetIntAction>(
                "SetInt",
                "设置整数值",
                BehaviorCategory.Primitive,
                "基础行为",
                "设置整型参数值",
                new[]
                {
                    new BehaviorParameterDefinition("variableName", BehaviorParameterType.String, "变量名", "目标变量名称", ""),
                    new BehaviorParameterDefinition("value", BehaviorParameterType.Int, "值", "要设置的值", 0)
                },
                (action, item) =>
                {
                    var setAction = (AbilityKit.HFSM.Actions.SetIntAction)action;
                    setAction.variableName = item.GetParamValue<string>("variableName");
                    setAction.value = item.GetParamValue<int>("value");
                }
            );

            Register<AbilityKit.HFSM.Actions.PlayAnimationAction>(
                "PlayAnimation",
                "播放动画",
                BehaviorCategory.Primitive,
                "基础行为",
                "播放 Animator 动画",
                new[]
                {
                    new BehaviorParameterDefinition("stateName", BehaviorParameterType.String, "状态名", "Animator 状态名称", ""),
                    new BehaviorParameterDefinition("crossFadeDuration", BehaviorParameterType.Float, "渐变时间", "动画渐变时间", 0.1f)
                },
                (action, item) =>
                {
                    var playAction = (AbilityKit.HFSM.Actions.PlayAnimationAction)action;
                    playAction.stateName = item.GetParamValue<string>("stateName");
                    playAction.crossFadeDuration = item.GetParamValue<float>("crossFadeDuration");
                }
            );

            Register<AbilityKit.HFSM.Actions.SetActiveAction>(
                "SetActive",
                "设置激活状态",
                BehaviorCategory.Primitive,
                "基础行为",
                "设置 GameObject 激活状态",
                new[]
                {
                    new BehaviorParameterDefinition("target", BehaviorParameterType.Object, "目标对象", "目标 GameObject", null),
                    new BehaviorParameterDefinition("active", BehaviorParameterType.Bool, "激活", "是否激活", true)
                },
                (action, item) =>
                {
                    var setAction = (AbilityKit.HFSM.Actions.SetActiveAction)action;
                    setAction.targetObject = item.GetParamValue<UnityEngine.Object>("target");
                    setAction.active = item.GetParamValue<bool>("active");
                }
            );

            Register<AbilityKit.HFSM.Actions.MoveToAction>(
                "MoveTo",
                "移动到目标",
                BehaviorCategory.Primitive,
                "基础行为",
                "移动 Transform 到目标位置",
                new[]
                {
                    new BehaviorParameterDefinition("target", BehaviorParameterType.Object, "变换组件", "目标 Transform", null),
                    new BehaviorParameterDefinition("destination", BehaviorParameterType.Vector3, "目标位置", "目标世界坐标", UnityEngine.Vector3.zero),
                    new BehaviorParameterDefinition("speed", BehaviorParameterType.Float, "速度", "移动速度", 5f)
                },
                (action, item) =>
                {
                    var moveAction = (AbilityKit.HFSM.Actions.MoveToAction)action;
                    moveAction.target = item.GetParamValue<UnityEngine.Transform>("target");
                    moveAction.destination = item.GetParamValue<UnityEngine.Vector3>("destination");
                    moveAction.speed = item.GetParamValue<float>("speed");
                }
            );

            // ========== 复合行为 ==========
            Register<AbilityKit.HFSM.Actions.SequenceAction>(
                "Sequence",
                "顺序执行",
                BehaviorCategory.Composite,
                "复合行为",
                "依次执行子节点，直到某个失败",
                null,
                null
            );

            Register<AbilityKit.HFSM.Actions.SelectorAction>(
                "Selector",
                "选择执行",
                BehaviorCategory.Composite,
                "复合行为",
                "依次尝试子节点，直到某个成功",
                null,
                null
            );

            Register<AbilityKit.HFSM.Actions.ParallelAction>(
                "Parallel",
                "并行执行",
                BehaviorCategory.Composite,
                "复合行为",
                "同时执行所有子节点",
                new[]
                {
                    new BehaviorParameterDefinition("failOnAnyFailure", BehaviorParameterType.Bool, "任一失败则失败", "任意子节点失败时整体失败", false)
                },
                (action, item) =>
                {
                    var parallelAction = (AbilityKit.HFSM.Actions.ParallelAction)action;
                    parallelAction.failOnAnyFailure = item.GetParamValue<bool>("failOnAnyFailure");
                }
            );

            Register<AbilityKit.HFSM.Actions.RandomSelectorAction>(
                "RandomSelector",
                "随机选择",
                BehaviorCategory.Composite,
                "复合行为",
                "随机选择一个子节点执行",
                null,
                null
            );

            Register<AbilityKit.HFSM.Actions.RandomSequenceAction>(
                "RandomSequence",
                "随机顺序",
                BehaviorCategory.Composite,
                "复合行为",
                "随机顺序执行所有子节点",
                null,
                null
            );

            // ========== 修饰器行为 ==========
            Register<AbilityKit.HFSM.Actions.RepeatAction>(
                "Repeat",
                "重复",
                BehaviorCategory.Decorator,
                "修饰器",
                "重复执行子节点指定次数",
                new[]
                {
                    new BehaviorParameterDefinition("count", BehaviorParameterType.Int, "次数", "重复次数，-1 表示无限", -1)
                },
                (action, item) =>
                {
                    var repeatAction = (AbilityKit.HFSM.Actions.RepeatAction)action;
                    repeatAction.count = item.GetParamValue<int>("count");
                }
            );

            Register<AbilityKit.HFSM.Actions.InvertAction>(
                "Invert",
                "反转",
                BehaviorCategory.Decorator,
                "修饰器",
                "反转子节点的执行结果",
                null,
                null
            );

            Register<AbilityKit.HFSM.Actions.TimeLimitAction>(
                "TimeLimit",
                "时间限制",
                BehaviorCategory.Decorator,
                "修饰器",
                "限制子节点执行时间",
                new[]
                {
                    new BehaviorParameterDefinition("timeLimit", BehaviorParameterType.Float, "时间限制", "最大执行时间（秒）", 5f)
                },
                (action, item) =>
                {
                    var timeLimitAction = (AbilityKit.HFSM.Actions.TimeLimitAction)action;
                    timeLimitAction.timeLimit = item.GetParamValue<float>("timeLimit");
                }
            );

            Register<AbilityKit.HFSM.Actions.UntilSuccessAction>(
                "UntilSuccess",
                "直到成功",
                BehaviorCategory.Decorator,
                "修饰器",
                "重复执行子节点直到成功",
                null,
                null
            );

            Register<AbilityKit.HFSM.Actions.UntilFailureAction>(
                "UntilFailure",
                "直到失败",
                BehaviorCategory.Decorator,
                "修饰器",
                "重复执行子节点直到失败",
                null,
                null
            );

            Register<AbilityKit.HFSM.Actions.CooldownAction>(
                "Cooldown",
                "冷却",
                BehaviorCategory.Decorator,
                "修饰器",
                "执行后等待冷却时间",
                new[]
                {
                    new BehaviorParameterDefinition("cooldownDuration", BehaviorParameterType.Float, "冷却时间", "冷却持续时间（秒）", 1f)
                },
                (action, item) =>
                {
                    var cooldownAction = (AbilityKit.HFSM.Actions.CooldownAction)action;
                    cooldownAction.cooldownDuration = item.GetParamValue<float>("cooldownDuration");
                }
            );

        }

        /// <summary>
        /// 创建默认工厂方法
        /// </summary>
        private static Action<IAction, BehaviorItem> CreateDefaultFactory(Type actionType)
        {
            // 默认工厂直接创建实例，不做参数绑定
            // 具体参数绑定需要在注册时提供自定义工厂
            return (action, item) => { };
        }
    }
}
