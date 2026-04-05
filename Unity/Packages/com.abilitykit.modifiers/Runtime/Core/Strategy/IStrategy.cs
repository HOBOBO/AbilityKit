using System;
using System.Collections.Generic;
using System.Reflection;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 策略核心接口 — 框架层定义契约，业务层实现
    //
    // 设计原则：
    //  - 框架只定义"如何执行策略"的接口
    //  - 业务定义"策略是什么"的实现
    //  - 策略ID由字符串指定，框架无需知道具体类型
    //  - 支持按 OwnerKey 统一还原
    // ============================================================================

    /// <summary>
    /// 策略唯一标识符
    /// </summary>
    public readonly struct StrategyId : IEquatable<StrategyId>
    {
        public readonly string Id;

        public StrategyId(string id)
        {
            Id = id ?? string.Empty;
        }

        public static StrategyId None => default;
        public bool IsValid => !string.IsNullOrEmpty(Id);

        public static bool operator ==(StrategyId a, StrategyId b) => a.Id == b.Id;
        public static bool operator !=(StrategyId a, StrategyId b) => a.Id != b.Id;
        public bool Equals(StrategyId other) => Id == other.Id;
        public override bool Equals(object obj) => obj is StrategyId other && Equals(other);
        public override int GetHashCode() => Id?.GetHashCode() ?? 0;
        public override string ToString() => Id ?? "None";
    }

    /// <summary>
    /// 策略应用结果
    /// </summary>
    public readonly struct StrategyApplyResult
    {
        public readonly bool Success;
        public readonly string Error;
        public readonly object ResultValue;

        public StrategyApplyResult(bool success, string error, object resultValue)
        {
            Success = success;
            Error = error;
            ResultValue = resultValue;
        }

        public static StrategyApplyResult Succeeded(object result = null)
            => new StrategyApplyResult(true, null, result);

        public static StrategyApplyResult Failed(string error)
            => new StrategyApplyResult(false, error, null);
    }

    /// <summary>
    /// 策略还原结果
    /// </summary>
    public readonly struct StrategyRevertResult
    {
        public readonly bool Success;
        public readonly string Error;

        public StrategyRevertResult(bool success, string error)
        {
            Success = success;
            Error = error;
        }

        public static StrategyRevertResult Succeeded()
            => new StrategyRevertResult(true, null);

        public static StrategyRevertResult Failed(string error)
            => new StrategyRevertResult(false, error);
    }

    /// <summary>
    /// 策略接口 - 核心扩展点
    ///
    /// 业务层实现此接口来定义具体的修改逻辑：
    /// - 数值修改（加法、乘法、覆盖）
    /// - 状态修改（保存原始值、设置新值）
    /// - 标签管理（添加、移除）
    /// - 列表操作（增、删、改）
    /// - 任何其他自定义逻辑
    ///
    /// 框架只负责调用策略，不关心策略的具体实现。
    /// </summary>
    public interface IStrategy
    {
        /// <summary>
        /// 策略唯一标识
        /// 业务层自定义字符串，如 "numeric.add"、"state.set"、"tag.add"
        /// </summary>
        StrategyId StrategyId { get; }

        /// <summary>
        /// 策略描述（用于调试）
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 应用策略到目标
        /// </summary>
        /// <param name="target">目标对象（由业务层解释）</param>
        /// <param name="context">策略上下文（包含值、拥有者等信息）</param>
        /// <returns>应用结果</returns>
        StrategyApplyResult Apply(object target, in StrategyContext context);

        /// <summary>
        /// 还原策略对目标的影响
        /// </summary>
        /// <param name="target">目标对象</param>
        /// <param name="context">策略上下文</param>
        /// <returns>还原结果</returns>
        StrategyRevertResult Revert(object target, in StrategyContext context);

        /// <summary>
        /// 计算修改后的值（用于数值类策略）
        /// 非数值类策略可直接返回默认值
        /// </summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="baseValue">基础值</param>
        /// <param name="context">策略上下文</param>
        /// <returns>计算后的值</returns>
        T Calculate<T>(T baseValue, in StrategyContext context);
    }

    /// <summary>
    /// 数值类策略基类
    /// 提供默认的数值计算逻辑，子类可重写特定操作
    /// </summary>
    public abstract class NumericStrategyBase : IStrategy
    {
        public abstract StrategyId StrategyId { get; }
        public virtual string Description => StrategyId.Id;

        public abstract StrategyApplyResult Apply(object target, in StrategyContext context);
        public abstract StrategyRevertResult Revert(object target, in StrategyContext context);

        public virtual T Calculate<T>(T baseValue, in StrategyContext context)
        {
            if (baseValue is float f && context.TryGetFloatValue(out var modValue))
            {
                return (T)(object)CalculateNumeric(f, context);
            }
            if (baseValue is int i && context.TryGetIntValue(out var intValue))
            {
                return (T)(object)CalculateInt(i, context);
            }
            return baseValue;
        }

        protected virtual float CalculateNumeric(float baseValue, in StrategyContext context)
        {
            var op = context.OperationKind;
            var value = context.GetFloatValue();

            return op switch
            {
                StrategyOperationKind.Add => baseValue + value,
                StrategyOperationKind.Mult => baseValue * value,
                StrategyOperationKind.Override => value,
                StrategyOperationKind.PercentAdd => baseValue * (1f + value),
                _ => baseValue
            };
        }

        protected virtual int CalculateInt(int baseValue, in StrategyContext context)
        {
            var op = context.OperationKind;
            var value = context.GetIntValue();

            return op switch
            {
                StrategyOperationKind.Add => baseValue + value,
                StrategyOperationKind.Mult => (int)(baseValue * value),
                StrategyOperationKind.Override => value,
                _ => baseValue
            };
        }
    }

    /// <summary>
    /// 状态修改策略基类
    /// 提供状态保存、修改、还原的基础实现
    /// </summary>
    public abstract class StateStrategyBase : IStrategy
    {
        public abstract StrategyId StrategyId { get; }
        public virtual string Description => StrategyId.Id;

        /// <summary>
        /// 获取目标当前的状态值
        /// </summary>
        protected abstract object GetState(object target, string stateKey);

        /// <summary>
        /// 设置目标的状态值
        /// </summary>
        protected abstract void SetState(object target, string stateKey, object value);

        /// <summary>
        /// 保存原始状态（用于还原）
        /// </summary>
        protected abstract void SaveOriginalState(object target, long ownerKey, string stateKey, object originalValue);

        /// <summary>
        /// 还原原始状态
        /// </summary>
        protected abstract object RestoreOriginalState(object target, long ownerKey, string stateKey);

        public virtual StrategyApplyResult Apply(object target, in StrategyContext context)
        {
            var stateKey = context.TargetKey;
            if (string.IsNullOrEmpty(stateKey))
            {
                return StrategyApplyResult.Failed("State key is required for state strategy");
            }

            // 保存原始值
            var originalValue = GetState(target, stateKey);
            SaveOriginalState(target, context.OwnerKey, stateKey, originalValue);

            // 设置新值
            SetState(target, stateKey, context.Value);

            return StrategyApplyResult.Succeeded(originalValue);
        }

        public virtual StrategyRevertResult Revert(object target, in StrategyContext context)
        {
            var stateKey = context.TargetKey;
            if (string.IsNullOrEmpty(stateKey))
            {
                return StrategyRevertResult.Failed("State key is required for state strategy");
            }

            RestoreOriginalState(target, context.OwnerKey, stateKey);
            return StrategyRevertResult.Succeeded();
        }

        public virtual T Calculate<T>(T baseValue, in StrategyContext context) => baseValue;
    }

    /// <summary>
    /// 标签操作策略基类
    /// </summary>
    public abstract class TagStrategyBase : IStrategy
    {
        public abstract StrategyId StrategyId { get; }
        public virtual string Description => StrategyId.Id;

        protected abstract void AddTag(object target, string tag, long ownerKey);
        protected abstract void RemoveTag(object target, string tag, long ownerKey);
        protected abstract bool HasTag(object target, string tag);

        public virtual StrategyApplyResult Apply(object target, in StrategyContext context)
        {
            var tag = context.GetStringValue();
            if (string.IsNullOrEmpty(tag))
            {
                return StrategyApplyResult.Failed("Tag value is required for tag strategy");
            }

            if (StrategyId.Id == "tag.add")
            {
                AddTag(target, tag, context.OwnerKey);
            }
            else if (StrategyId.Id == "tag.remove")
            {
                RemoveTag(target, tag, context.OwnerKey);
            }

            return StrategyApplyResult.Succeeded(tag);
        }

        public virtual StrategyRevertResult Revert(object target, in StrategyContext context)
        {
            var tag = context.GetStringValue();
            if (string.IsNullOrEmpty(tag))
            {
                return StrategyRevertResult.Failed("Tag value is required for tag strategy");
            }

            if (StrategyId.Id == "tag.add")
            {
                RemoveTag(target, tag, context.OwnerKey);
            }
            else if (StrategyId.Id == "tag.remove")
            {
                AddTag(target, tag, context.OwnerKey);
            }

            return StrategyRevertResult.Succeeded();
        }

        public virtual T Calculate<T>(T baseValue, in StrategyContext context) => baseValue;
    }

    // ============================================================================
    // 策略注册表 — 业务层注册策略实例
    // ============================================================================

    /// <summary>
    /// 策略注册表接口
    /// 业务层通过此接口注册策略，框架通过此接口获取策略
    /// </summary>
    public interface IStrategyRegistry
    {
        /// <summary>
        /// 注册策略
        /// </summary>
        /// <param name="strategy">策略实例</param>
        void Register(IStrategy strategy);

        /// <summary>
        /// 批量注册策略
        /// </summary>
        void RegisterRange(IEnumerable<IStrategy> strategies);

        /// <summary>
        /// 获取策略
        /// </summary>
        /// <param name="strategyId">策略ID</param>
        /// <param name="strategy">输出策略实例</param>
        /// <returns>是否找到</returns>
        bool TryGet(StrategyId strategyId, out IStrategy strategy);

        /// <summary>
        /// 获取策略（通过字符串）
        /// </summary>
        bool TryGet(string strategyId, out IStrategy strategy);

        /// <summary>
        /// 获取所有已注册的策略
        /// </summary>
        IReadOnlyList<IStrategy> GetAll();

        /// <summary>
        /// 检查是否已注册
        /// </summary>
        bool IsRegistered(StrategyId strategyId);

        /// <summary>
        /// 移除策略
        /// </summary>
        bool Unregister(StrategyId strategyId);
    }

    /// <summary>
    /// 策略注册表默认实现
    /// </summary>
    public sealed class StrategyRegistry : IStrategyRegistry
    {
        private readonly Dictionary<string, IStrategy> _strategies = new();
        private readonly Dictionary<StrategyId, IStrategy> _strategiesById = new();
        private readonly object _lock = new();

        public void Register(IStrategy strategy)
        {
            if (strategy == null) return;

            lock (_lock)
            {
                _strategies[strategy.StrategyId.Id] = strategy;
                _strategiesById[strategy.StrategyId] = strategy;
            }
        }

        public void RegisterRange(IEnumerable<IStrategy> strategies)
        {
            if (strategies == null) return;

            lock (_lock)
            {
                foreach (var strategy in strategies)
                {
                    if (strategy != null)
                    {
                        _strategies[strategy.StrategyId.Id] = strategy;
                        _strategiesById[strategy.StrategyId] = strategy;
                    }
                }
            }
        }

        public bool TryGet(StrategyId strategyId, out IStrategy strategy)
        {
            lock (_lock)
            {
                return _strategiesById.TryGetValue(strategyId, out strategy);
            }
        }

        public bool TryGet(string strategyId, out IStrategy strategy)
        {
            if (string.IsNullOrEmpty(strategyId))
            {
                strategy = null;
                return false;
            }

            lock (_lock)
            {
                return _strategies.TryGetValue(strategyId, out strategy);
            }
        }

        public IReadOnlyList<IStrategy> GetAll()
        {
            lock (_lock)
            {
                return new List<IStrategy>(_strategies.Values);
            }
        }

        public bool IsRegistered(StrategyId strategyId)
        {
            lock (_lock)
            {
                return _strategiesById.ContainsKey(strategyId);
            }
        }

        public bool Unregister(StrategyId strategyId)
        {
            lock (_lock)
            {
                if (_strategiesById.TryGetValue(strategyId, out var strategy))
                {
                    _strategiesById.Remove(strategyId);
                    _strategies.Remove(strategyId.Id);
                    return true;
                }
                return false;
            }
        }
    }

    // ============================================================================
    // 策略标记 Attribute — 用于声明式注册
    // ============================================================================

    /// <summary>
    /// 标记策略实现的 Attribute
    /// 框架可扫描程序集自动发现并注册
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class StrategyImplAttribute : Attribute
    {
        /// <summary>
        /// 策略ID
        /// </summary>
        public string StrategyId { get; }

        /// <summary>
        /// 注册优先级（数值越大优先级越高）
        /// </summary>
        public int Priority { get; set; }

        public StrategyImplAttribute(string strategyId)
        {
            StrategyId = strategyId ?? throw new ArgumentNullException(nameof(strategyId));
        }
    }

    /// <summary>
    /// 策略扫描器 - 自动扫描程序集发现策略实现
    /// </summary>
    public static class StrategyScanner
    {
        /// <summary>
        /// 扫描程序集并注册所有策略
        /// </summary>
        public static void ScanAndRegister(Assembly assembly, IStrategyRegistry registry)
        {
            if (assembly == null || registry == null) return;

            foreach (var type in assembly.GetTypes())
            {
                var attr = type.GetCustomAttribute<StrategyImplAttribute>();
                if (attr != null)
                {
                    var ctor = type.GetConstructor(Type.EmptyTypes);
                    if (ctor != null)
                    {
                        var strategy = ctor.Invoke(null) as IStrategy;
                        if (strategy != null)
                        {
                            registry.Register(strategy);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 扫描调用程序集并注册所有策略
        /// </summary>
        public static void ScanAndRegister(IStrategyRegistry registry)
        {
            ScanAndRegister(Assembly.GetCallingAssembly(), registry);
        }
    }
}
