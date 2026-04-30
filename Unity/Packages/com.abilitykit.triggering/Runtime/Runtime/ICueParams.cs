using System;
using System.Runtime.CompilerServices;

namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// Cue 参数标记接口
    /// 用于泛型化 ITriggerCue 和 TriggerCueContext，使 Cue 回调携带类型安全的参数
    ///
    /// 设计原则：
    /// - 框架层只定义契约，不含任何业务字段
    /// - 业务层定义自己的参数结构，实现此接口
    /// - 示例：
    ///   <code>
    ///   public readonly struct DamageCueParams : ICueParams
    ///   {
    ///       public double DamageValue;
    ///       public bool IsCritical;
    ///   }
    ///   </code>
    /// </summary>
    public interface ICueParams { }
}
