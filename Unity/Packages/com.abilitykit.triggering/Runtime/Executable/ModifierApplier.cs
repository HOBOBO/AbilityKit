using System;
using AbilityKit.Core.Common.Marker;
using AbilityKit.Modifiers;

namespace AbilityKit.Triggering.Runtime.Executable
{
    /// <summary>
    /// 修改器应用器接口 - 框架扩展点
    /// 业务包实现此接口来将调度行为的修改器应用到属性系统
    ///
    /// 使用场景：
    /// - DOT（持续伤害）：周期性对目标造成伤害
    /// - HOT（持续治疗）：周期性为目标恢复生命
    /// - 属性增益：周期性增加/减少属性值
    /// </summary>
    public interface IModifierApplier
    {
        ModifierApplyResult ApplyModifiers(object target, ReadOnlySpan<ModifierData> modifiers, int sourceId);
    }

    /// <summary>
    /// 修改器应用结果
    /// </summary>
    public readonly struct ModifierApplyResult
    {
        public bool Success { get; }
        public string ErrorMessage { get; }
        public float AppliedValue { get; }

        public static ModifierApplyResult Succeeded(float appliedValue = 0f)
            => new(true, null, appliedValue);

        public static ModifierApplyResult Failed(string error)
            => new(false, error, 0f);

        private ModifierApplyResult(bool success, string errorMessage, float appliedValue)
        {
            Success = success;
            ErrorMessage = errorMessage;
            AppliedValue = appliedValue;
        }
    }

    /// <summary>
    /// 标记实现 IModifierApplier 的类型的 Attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ModifierApplierAttribute : MarkerAttribute
    {
        public int Priority { get; }

        public ModifierApplierAttribute(int priority = 0)
        {
            Priority = priority;
        }
    }

    /// <summary>
    /// 修改器应用器注册表
    /// </summary>
    public sealed class ModifierApplierRegistry
    {
        internal IModifierApplier _defaultApplier;
        private bool _hasDefault;

        public void SetDefault(IModifierApplier applier)
        {
            _defaultApplier = applier;
            _hasDefault = true;
        }

        public ModifierApplyResult Apply(object target, ReadOnlySpan<ModifierData> modifiers, int sourceId)
        {
            if (!_hasDefault || _defaultApplier == null)
                return ModifierApplyResult.Failed("No modifier applier registered");

            return _defaultApplier.ApplyModifiers(target, modifiers, sourceId);
        }

        public static ModifierApplierRegistry Default { get; } = new ModifierApplierRegistry();
    }

    /// <summary>
    /// 可应用修改器的周期行为
    /// 业务包可以使用此类来实现 DOT/HOT 等效果
    /// </summary>
    public class ModifierApplyingPeriodicExecutable : PeriodicExecutable
    {
        public ModifierData[] Modifiers { get; set; }
        public int SourceId { get; set; }

        public ModifierApplyingPeriodicExecutable()
        {
            OnPeriodExecuted += HandlePeriodExecuted;
        }

        private void HandlePeriodExecuted(object ctx)
        {
            if (Modifiers == null || Modifiers.Length == 0)
                return;

            var applier = ModifierApplierRegistry.Default._defaultApplier;
            if (applier == null)
                return;

            var target = GetModifierTarget(ctx);
            if (target == null)
                return;

            var result = applier.ApplyModifiers(target, Modifiers, SourceId);
            if (!result.Success)
            {
                OnModifierApplyFailed(ctx, result.ErrorMessage);
            }
            else
            {
                OnModifierApplied(ctx, result.AppliedValue);
            }
        }

        protected virtual object GetModifierTarget(object ctx)
        {
            return ctx;
        }

        protected virtual void OnModifierApplied(object ctx, float appliedValue)
        {
        }

        protected virtual void OnModifierApplyFailed(object ctx, string error)
        {
        }
    }
}
