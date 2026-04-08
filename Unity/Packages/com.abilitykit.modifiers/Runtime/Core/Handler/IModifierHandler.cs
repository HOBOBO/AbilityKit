using System;
using System.Runtime.CompilerServices;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 修改器处理器接口
    // ============================================================================

    /// <summary>
    /// 修改器处理器接口。
    /// 定义如何将修改器应用到基础值上。
    ///
    /// 设计原则：
    /// - 泛型设计：支持任意类型的值（float、int、bool、string、struct 等）
    /// - 单一职责：Handler 只负责类型相关的操作，不负责具体操作实现
    /// - 可扩展：业务层可实现自定义 Handler
    /// - 使用 Operator：具体操作逻辑委托给 IModifierOperator
    ///
    /// 使用示例：
    /// ```csharp
    /// // 数值型
    /// var handler = new NumericModifierHandler();
    /// var result = handler.Apply(100f, modifier, context);
    ///
    /// // 布尔型
    /// var boolHandler = new BooleanModifierHandler();
    /// var boolResult = boolHandler.Apply(false, modifier, context);
    ///
    /// // 自定义
    /// public class MyHandler : IModifierHandler<MyType> { ... }
    /// ```
    /// </summary>
    /// <typeparam name="TValue">被修改的值的类型</typeparam>
    public interface IModifierHandler<TValue>
    {
        /// <summary>
        /// 应用单个修改器到当前值
        /// </summary>
        /// <param name="currentValue">当前累积的值</param>
        /// <param name="modifier">修改器数据</param>
        /// <param name="context">修改器上下文（用于获取属性、等级等）</param>
        /// <returns>应用后的值</returns>
        TValue Apply(TValue currentValue, in ModifierData modifier, IModifierContext context);

        /// <summary>
        /// 比较两个值，用于 Override 优先级判断。
        /// 返回值：&lt;0 表示 a 更优先，&gt;0 表示 b 更优先，=0 表示相同
        /// </summary>
        int Compare(TValue a, TValue b);

        /// <summary>
        /// 合并多个修改器的值（用于叠加层数等场景）。
        /// </summary>
        TValue Combine(in Span<TValue> values);
    }

    // ============================================================================
    // 修改器处理器基类
    // ============================================================================

    /// <summary>
    /// 修改���处理器基类。
    /// 提供通用的应用逻辑，子类只需实现类型特定的方法。
    /// </summary>
    /// <typeparam name="TValue">值的类型</typeparam>
    public abstract class ModifierHandlerBase<TValue> : IModifierHandler<TValue>
    {
        /// <summary>
        /// 应用单个修改器到当前值
        /// </summary>
        public virtual TValue Apply(TValue currentValue, in ModifierData modifier, IModifierContext context)
        {
            float value = GetModifierValue(modifier, context);
            var op = ModifierOperatorRegistry.Get(modifier.Op);

            if (op != null)
            {
                // 使用 IModifierOperator 进行计算
                return ApplyOperator(currentValue, op, value);
            }

            // 自定义操作
            return ApplyCustom(currentValue, modifier, context);
        }

        /// <summary>
        /// 使用操作器应用修改
        /// </summary>
        protected abstract TValue ApplyOperator(TValue currentValue, IModifierOperator op, float modifierValue);

        /// <summary>
        /// 从修改器获取数值
        /// </summary>
        protected virtual float GetModifierValue(in ModifierData modifier, IModifierContext context)
        {
            return modifier.GetMagnitude(context?.Level ?? 1f, context);
        }

        /// <summary>
        /// 自定义操作处理。子类可重写。
        /// </summary>
        protected virtual TValue ApplyCustom(TValue currentValue, in ModifierData modifier, IModifierContext context)
        {
            return currentValue;
        }

        /// <summary>
        /// 比较两个值
        /// </summary>
        public abstract int Compare(TValue a, TValue b);

        /// <summary>
        /// 合并多个值
        /// </summary>
        public abstract TValue Combine(in Span<TValue> values);
    }
}
