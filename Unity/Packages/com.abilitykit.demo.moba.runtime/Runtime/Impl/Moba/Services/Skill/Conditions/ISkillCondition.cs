using System;
using System.Linq;
using System.Reflection;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    /// <summary>
    /// 技能释放条件检查结果
    /// </summary>
    public readonly struct SkillConditionResult
    {
        /// <summary>
        /// 是否通过
        /// </summary>
        public bool Passed { get; }

        /// <summary>
        /// 失败原因（用于显示给玩家）
        /// </summary>
        public string FailureReason { get; }

        /// <summary>
        /// 失败原因的关键字（用于UI显示）
        /// </summary>
        public string FailureKey { get; }

        /// <summary>
        /// 失败原因的参数（用于格式化）
        /// </summary>
        public object[] FailureParams { get; }

        public static SkillConditionResult Pass => new(true, null, null, null);

        public static SkillConditionResult Fail(string reason, string failureKey = null, params object[] @params)
            => new(false, reason, failureKey, @params);

        private SkillConditionResult(bool passed, string reason, string failureKey, object[] @params)
        {
            Passed = passed;
            FailureReason = reason;
            FailureKey = failureKey;
            FailureParams = @params;
        }

        public SkillConditionResult And(SkillConditionResult other)
        {
            if (!Passed) return this;
            return other;
        }

        public SkillConditionResult Or(SkillConditionResult other)
        {
            if (Passed) return this;
            return other;
        }
    }

    /// <summary>
    /// 技能条件接口
    /// 定义技能释放前置条件的检查逻辑
    /// </summary>
    public interface ISkillCondition
    {
        /// <summary>
        /// 条件唯一标识
        /// </summary>
        string Id { get; }

        /// <summary>
        /// 条件显示名称
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// 条件描述（用于调试/日志）
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 检查条件是否满足
        /// </summary>
        /// <param name="context">技能管线上下文</param>
        /// <returns>检查结果</returns>
        SkillConditionResult Check(SkillPipelineContext context);

        /// <summary>
        /// 检查是否可以在持续检测模式下工作
        /// 某些条件（如冷却）可以持续检查，有些（如资源）只检查一次
        /// </summary>
        bool SupportsContinuousCheck { get; }
    }

    /// <summary>
    /// 技能条件基类
    /// 提供通用的条件检查能力
    /// 自动从 SkillConditionAttribute 读取 Id 和 DisplayName
    /// </summary>
    public abstract class SkillConditionBase : ISkillCondition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public virtual string Description => DisplayName;
        public virtual bool SupportsContinuousCheck => false;

        protected SkillConditionBase()
        {
            var attr = GetType().GetCustomAttributes(typeof(SkillConditionAttribute), false)
                .FirstOrDefault() as SkillConditionAttribute;
            Id = attr?.Id ?? GetType().Name;
            DisplayName = attr?.DisplayName ?? Id;
        }

        public abstract SkillConditionResult Check(SkillPipelineContext context);

        protected static SkillConditionResult Fail(string reason, string failureKey = null, params object[] @params)
            => SkillConditionResult.Fail(reason, failureKey, @params);

        protected static SkillConditionResult Pass => SkillConditionResult.Pass;
    }

    /// <summary>
    /// 技能条件特性
    /// 用于自动发现和注册技能条件
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class SkillConditionAttribute : Attribute
    {
        /// <summary>
        /// 条件唯一标识
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 条件显示名称
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 优先级，数值越大优先级越高
        /// </summary>
        public int Priority { get; set; } = 0;

        public SkillConditionAttribute(string id, string displayName = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            DisplayName = displayName ?? id;
        }
    }
}