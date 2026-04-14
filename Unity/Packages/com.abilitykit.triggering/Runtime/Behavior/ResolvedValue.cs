using System;
using AbilityKit.Triggering.Runtime.Config.Values;

namespace AbilityKit.Triggering.Runtime.Behavior
{
    /// <summary>
    /// 运行时解析后的数值
    /// 与 ValueRefConfig 区分：Config 是"引用描述"，ResolvedValue 是"实际数值"
    /// </summary>
    [System.Serializable]
    public struct ResolvedValue
    {
        public readonly double Value;
        public readonly bool IsValid;
        public readonly string Error;

        public static ResolvedValue Valid(double value) => new ResolvedValue(value, true, null);
        public static ResolvedValue Invalid(string error) => new ResolvedValue(0, false, error);
        public static ResolvedValue None => new ResolvedValue(0, true, null);

        private ResolvedValue(double value, bool isValid, string error)
        {
            Value = value;
            IsValid = isValid;
            Error = error;
        }
    }

    /// <summary>
    /// 数值引用转换器
    /// 将 IValueRefConfig 转换为 ResolvedValue
    /// </summary>
    public interface IValueRefConverter
    {
        ResolvedValue Convert(IValueRefConfig config, IBehaviorContext context);
    }

    /// <summary>
    /// 默认数值引用转换器实现
    /// </summary>
    public class DefaultValueRefConverter : IValueRefConverter
    {
        private readonly IValueResolver _resolver;

        public DefaultValueRefConverter(IValueResolver resolver)
        {
            _resolver = resolver;
        }

        public ResolvedValue Convert(IValueRefConfig config, IBehaviorContext context)
        {
            if (config == null)
                return ResolvedValue.None;

            try
            {
                var value = _resolver.Resolve(config, context);
                return ResolvedValue.Valid(value);
            }
            catch (Exception ex)
            {
                return ResolvedValue.Invalid(ex.Message);
            }
        }
    }
}