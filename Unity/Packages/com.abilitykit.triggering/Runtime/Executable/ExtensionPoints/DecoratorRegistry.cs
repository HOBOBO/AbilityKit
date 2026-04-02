using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Modifiers;

namespace AbilityKit.Triggering.Runtime.Executable
{
    // ========================================================================
    // 修饰器注册表 — 通过 [DecoratorImpl] 自动发现实现
    //
    //  核心包预置默认实现 (DefaultDecorators.cs)，业务包注册后自动替换
    //  注册基于 Type 而非枚举，新增修饰器类型无需修改核心包
    // ========================================================================

    /// <summary>
    /// 修饰器注册表
    /// 框架级别的扩展点，业务包通过 [DecoratorImpl] 特性注册实现
    /// </summary>
    public static class DecoratorRegistry
    {
        // Key: 修饰器接口 Type (e.g. typeof(IDurationDecorator))
        // Value: 具体实现类 Type (e.g. typeof(DefaultDurationDecorator))
        private static readonly Dictionary<Type, Type> _registry = new();
        private static bool _isInitialized;

        /// <summary>
        /// 业务包是否已注册
        /// </summary>
        public static bool IsBusinessPackageRegistered => _registry.Count > 0;

        static DecoratorRegistry()
        {
            // 延迟初始化
        }

        /// <summary>
        /// 从指定程序集自动发现并注册所有标记了 [DecoratorImpl] 的修饰器实现
        /// </summary>
        public static void RegisterFromAssembly(Assembly asm)
        {
            if (asm == null) return;

            var types = asm.GetTypes();
            foreach (var type in types)
            {
                var attr = type.GetCustomAttribute<DecoratorImplAttribute>(false);
                if (attr != null)
                {
                    var decoratorType = attr.DecoratorType;
                    if (!_registry.ContainsKey(decoratorType) ||
                        attr.Priority > GetPriority(_registry[decoratorType]))
                    {
                        _registry[decoratorType] = type;
                    }
                }
            }
        }

        /// <summary>
        /// 手动注册一个修饰器实现
        /// </summary>
        /// <param name="decoratorInterfaceType">修饰器接口类型</param>
        /// <param name="implType">实现类类型</param>
        public static void Register(Type decoratorInterfaceType, Type implType)
        {
            _registry[decoratorInterfaceType] = implType;
        }

        /// <summary>
        /// 创建持续时间修饰器
        /// </summary>
        public static IDurationDecorator CreateDuration(float durationMs)
        {
            var deco = Create<IDurationDecorator>(typeof(IDurationDecorator), () => new DefaultDurationDecorator(durationMs));
            deco.Inner = null;
            return deco;
        }

        /// <summary>
        /// 创建标签修饰器
        /// </summary>
        public static ITagDecorator CreateTag(params string[] tagNames)
        {
            var deco = Create<ITagDecorator>(typeof(ITagDecorator), () => new DefaultTagDecorator(tagNames));
            deco.Inner = null;
            return deco;
        }

        /// <summary>
        /// 创建修改器修饰器
        /// </summary>
        public static IModifierDecorator CreateModifier(params ModifierData[] modifiers)
        {
            var deco = Create<IModifierDecorator>(typeof(IModifierDecorator), () => new DefaultModifierDecorator(modifiers));
            deco.Inner = null;
            return deco;
        }

        /// <summary>
        /// 创建修改器修饰器（带自定义应用器）
        /// </summary>
        /// <param name="applier">自定义修改器应用器</param>
        /// <param name="modifiers">初始修改器列表</param>
        public static IModifierDecorator CreateModifier(IModifierApplier applier, params ModifierData[] modifiers)
        {
            var deco = Create<IModifierDecorator>(typeof(IModifierDecorator), () => new DefaultModifierDecorator(applier, modifiers));
            deco.Inner = null;
            return deco;
        }

        /// <summary>
        /// 创建层数修饰器
        /// </summary>
        public static IStackDecorator CreateStack(int initialStack = 1, float stackMultiplier = 1f)
        {
            var deco = Create<IStackDecorator>(typeof(IStackDecorator), () => new DefaultStackDecorator(initialStack, stackMultiplier));
            deco.Inner = null;
            return deco;
        }

        /// <summary>
        /// 创建层级修饰器
        /// </summary>
        public static IHierarchyDecorator CreateHierarchy(int? parentId = null)
        {
            var deco = Create<IHierarchyDecorator>(typeof(IHierarchyDecorator), () => new DefaultHierarchyDecorator(parentId));
            deco.Inner = null;
            return deco;
        }

        private static T Create<T>(Type decoratorInterfaceType, Func<T> defaultFactory) where T : class
        {
            // 如果有业务包注册的类型，优先使用
            if (_registry.TryGetValue(decoratorInterfaceType, out var implType))
            {
                return Activator.CreateInstance(implType) as T;
            }

            // 否则使用默认实现
            return defaultFactory();
        }

        private static int GetPriority(Type type)
        {
            var attr = type.GetCustomAttribute<DecoratorImplAttribute>(false);
            return attr?.Priority ?? 0;
        }

        /// <summary>
        /// 清除所有注册
        /// </summary>
        public static void Clear()
        {
            _registry.Clear();
            _isInitialized = false;
        }
    }
}
