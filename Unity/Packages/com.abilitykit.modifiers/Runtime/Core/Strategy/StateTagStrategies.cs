using System;
using System.Collections.Generic;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 状态策略实现
    // ============================================================================

    /// <summary>
    /// 状态保存并设置策略
    /// 保存原始值并设置新值
    /// </summary>
    [StrategyImpl("state.set")]
    public sealed class StateSetStrategy : IStrategy
    {
        public StrategyId StrategyId => new StrategyId("state.set");
        public string Description => "State Set: Save original and set new value";

        public StrategyApplyResult Apply(object target, in StrategyContext context)
        {
            var stateKey = context.TargetKey;
            if (string.IsNullOrEmpty(stateKey))
            {
                return StrategyApplyResult.Failed("State key is required for state strategy");
            }

            if (target is IStateModifierTarget stateTarget)
            {
                var originalValue = stateTarget.GetState(stateKey);
                stateTarget.SaveOriginal(stateKey, context.OwnerKey, originalValue);
                stateTarget.SetState(stateKey, context.Value);
                return StrategyApplyResult.Succeeded(originalValue);
            }

            if (target is IStateModifierHandler handler)
            {
                var result = handler.ApplyState(stateKey, context.Value, context.OwnerKey);
                return result.Success ? StrategyApplyResult.Succeeded(result.OriginalValue) : StrategyApplyResult.Failed(result.Error);
            }

            return StrategyApplyResult.Failed($"Target does not implement IStateModifierTarget or IStateModifierHandler");
        }

        public StrategyRevertResult Revert(object target, in StrategyContext context)
        {
            var stateKey = context.TargetKey;
            if (string.IsNullOrEmpty(stateKey))
            {
                return StrategyRevertResult.Failed("State key is required for state strategy");
            }

            if (target is IStateModifierTarget stateTarget)
            {
                stateTarget.RestoreOriginal(stateKey, context.OwnerKey);
                return StrategyRevertResult.Succeeded();
            }

            if (target is IStateModifierHandler handler)
            {
                var result = handler.RevertState(stateKey, context.OwnerKey);
                return result.Success ? StrategyRevertResult.Succeeded() : StrategyRevertResult.Failed(result.Error);
            }

            return StrategyRevertResult.Failed($"Target does not implement IStateModifierTarget or IStateModifierHandler");
        }

        public T Calculate<T>(T baseValue, in StrategyContext context) => baseValue;
    }

    /// <summary>
    /// 状态还原策略
    /// 只还原，不设置新值
    /// </summary>
    [StrategyImpl("state.restore")]
    public sealed class StateRestoreStrategy : IStrategy
    {
        public StrategyId StrategyId => new StrategyId("state.restore");
        public string Description => "State Restore: Restore original state";

        public StrategyApplyResult Apply(object target, in StrategyContext context)
        {
            return StrategyRevertResult.Succeeded().Success
                ? StrategyApplyResult.Succeeded()
                : StrategyApplyResult.Failed("Cannot apply restore strategy");
        }

        public StrategyRevertResult Revert(object target, in StrategyContext context)
        {
            var stateKey = context.TargetKey;
            if (string.IsNullOrEmpty(stateKey))
            {
                return StrategyRevertResult.Failed("State key is required for state strategy");
            }

            if (target is IStateModifierTarget stateTarget)
            {
                stateTarget.RestoreOriginal(stateKey, context.OwnerKey);
                return StrategyRevertResult.Succeeded();
            }

            if (target is IStateModifierHandler handler)
            {
                return handler.RevertState(stateKey, context.OwnerKey).ToStrategyRevertResult();
            }

            return StrategyRevertResult.Failed($"Target does not implement IStateModifierTarget or IStateModifierHandler");
        }

        public T Calculate<T>(T baseValue, in StrategyContext context) => baseValue;
    }

    // ============================================================================
    // 状态修改器目标接口
    // ============================================================================

    /// <summary>
    /// 状态修改器目标接口
    /// 实现此接口的目标可以被状态策略修改
    /// </summary>
    public interface IStateModifierTarget
    {
        /// <summary>
        /// 获取状态值
        /// </summary>
        object GetState(string stateKey);

        /// <summary>
        /// 设置状态值
        /// </summary>
        void SetState(string stateKey, object value);

        /// <summary>
        /// 保存原始状态（用于还原）
        /// </summary>
        void SaveOriginal(string stateKey, long ownerKey, object originalValue);

        /// <summary>
        /// 还原原始状态
        /// </summary>
        void RestoreOriginal(string stateKey, long ownerKey);
    }

    /// <summary>
    /// 状态修改器处理接口（替代方案）
    /// 提供更灵活的内部状态管理
    /// </summary>
    public interface IStateModifierHandler
    {
        /// <summary>
        /// 应用状态修改
        /// </summary>
        StateModifyResult ApplyState(string stateKey, object newValue, long ownerKey);

        /// <summary>
        /// 还原状态
        /// </summary>
        StateModifyResult RevertState(string stateKey, long ownerKey);
    }

    /// <summary>
    /// 状态修改结果
    /// </summary>
    public readonly struct StateModifyResult
    {
        public readonly bool Success;
        public readonly string Error;
        public readonly object OriginalValue;

        public StateModifyResult(bool success, string error, object originalValue)
        {
            Success = success;
            Error = error;
            OriginalValue = originalValue;
        }

        public static StateModifyResult Succeeded(object originalValue = null)
            => new StateModifyResult(true, null, originalValue);

        public static StateModifyResult Failed(string error)
            => new StateModifyResult(false, error, null);

        public StrategyRevertResult ToStrategyRevertResult()
            => Success ? StrategyRevertResult.Succeeded() : StrategyRevertResult.Failed(Error);
    }

    // ============================================================================
    // 标签策略实现
    // ============================================================================

    /// <summary>
    /// 标签添加策略
    /// </summary>
    [StrategyImpl("tag.add")]
    public sealed class TagAddStrategy : IStrategy
    {
        public StrategyId StrategyId => new StrategyId("tag.add");
        public string Description => "Tag Add: Add a tag to target";

        public StrategyApplyResult Apply(object target, in StrategyContext context)
        {
            var tag = context.TargetKey;
            if (string.IsNullOrEmpty(tag))
            {
                return StrategyApplyResult.Failed("Tag value is required for tag strategy");
            }

            if (target is ITagModifierTarget tagTarget)
            {
                tagTarget.AddTag(tag, context.OwnerKey);
                return StrategyApplyResult.Succeeded(tag);
            }

            return StrategyApplyResult.Failed($"Target does not implement ITagModifierTarget");
        }

        public StrategyRevertResult Revert(object target, in StrategyContext context)
        {
            var tag = context.TargetKey;
            if (string.IsNullOrEmpty(tag))
            {
                return StrategyRevertResult.Failed("Tag value is required for tag strategy");
            }

            if (target is ITagModifierTarget tagTarget)
            {
                tagTarget.RemoveTag(tag, context.OwnerKey);
                return StrategyRevertResult.Succeeded();
            }

            return StrategyRevertResult.Failed($"Target does not implement ITagModifierTarget");
        }

        public T Calculate<T>(T baseValue, in StrategyContext context) => baseValue;
    }

    /// <summary>
    /// 标签移除策略
    /// </summary>
    [StrategyImpl("tag.remove")]
    public sealed class TagRemoveStrategy : IStrategy
    {
        public StrategyId StrategyId => new StrategyId("tag.remove");
        public string Description => "Tag Remove: Remove a tag from target";

        public StrategyApplyResult Apply(object target, in StrategyContext context)
        {
            var tag = context.TargetKey;
            if (string.IsNullOrEmpty(tag))
            {
                return StrategyApplyResult.Failed("Tag value is required for tag strategy");
            }

            if (target is ITagModifierTarget tagTarget)
            {
                tagTarget.RemoveTag(tag, context.OwnerKey);
                return StrategyApplyResult.Succeeded(tag);
            }

            return StrategyApplyResult.Failed($"Target does not implement ITagModifierTarget");
        }

        public StrategyRevertResult Revert(object target, in StrategyContext context)
        {
            var tag = context.TargetKey;
            if (string.IsNullOrEmpty(tag))
            {
                return StrategyRevertResult.Failed("Tag value is required for tag strategy");
            }

            if (target is ITagModifierTarget tagTarget)
            {
                tagTarget.AddTag(tag, context.OwnerKey);
                return StrategyRevertResult.Succeeded();
            }

            return StrategyRevertResult.Failed($"Target does not implement ITagModifierTarget");
        }

        public T Calculate<T>(T baseValue, in StrategyContext context) => baseValue;
    }

    /// <summary>
    /// 标签修改器目标接口
    /// </summary>
    public interface ITagModifierTarget
    {
        /// <summary>
        /// 添加标签
        /// </summary>
        void AddTag(string tag, long ownerKey);

        /// <summary>
        /// 移除标签
        /// </summary>
        void RemoveTag(string tag, long ownerKey);

        /// <summary>
        /// 是否拥有标签
        /// </summary>
        bool HasTag(string tag);

        /// <summary>
        /// 获取所有标签
        /// </summary>
        IReadOnlyCollection<string> GetTags();
    }
}
