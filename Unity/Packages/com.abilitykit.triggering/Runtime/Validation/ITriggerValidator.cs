using System;

namespace AbilityKit.Triggering.Validation
{
    /// <summary>
    /// 单个触发器校验器接口
    /// 校验器应该是无状态的，所有状态应该通过 ValidationContext 传入
    /// </summary>
    /// <typeparam name="TCtx">上下文类型参数</typeparam>
    public interface ITriggerValidator<TCtx>
    {
        /// <summary>
        /// 校验器名称（用于日志和调试）
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 执行优先级，数字越小越先执行
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 是否为关键校验器（关键校验器失败会短路后续校验）
        /// </summary>
        bool IsCritical { get; }

        /// <summary>
        /// 执行校验
        /// </summary>
        /// <param name="database">触发器计划数据库</param>
        /// <param name="context">验证上下文</param>
        /// <returns>校验结果</returns>
        ValidationResult Validate(in TriggerPlanDatabase<TCtx> database, in ValidationContext<TCtx> context);
    }

    /// <summary>
    /// 无操作校验器（用于禁用校验的场景）
    /// </summary>
    public sealed class NullValidator<TCtx> : ITriggerValidator<TCtx>
    {
        public static readonly NullValidator<TCtx> Instance = new NullValidator<TCtx>();

        public string Name => "空校验器";
        public int Priority => int.MaxValue;
        public bool IsCritical => false;

        public ValidationResult Validate(in TriggerPlanDatabase<TCtx> database, in ValidationContext<TCtx> context)
            => ValidationResult.Success;

        private NullValidator() { }
    }
}
