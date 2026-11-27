#nullable enable

using System.Collections.Generic;

namespace AbilityKit.Triggering.Validation
{
    /// <summary>
    /// 提供计划引用校验所需的函数和动作目录，不指定诊断策略或限额。
    /// </summary>
    public readonly struct ValidationContext<TCtx>
    {
        /// <summary>
        /// 已定义的函数 ID（数值的字符串形式）；为空或未提供时跳过函数 ID 核对。
        /// </summary>
        public HashSet<string>? DefinedFunctionIds { get; }

        /// <summary>
        /// 已定义的动作 ID（数值的字符串形式）；为空或未提供时跳过动作 ID 核对。
        /// </summary>
        public HashSet<string>? DefinedActionIds { get; }

        /// <summary>
        /// 创建校验上下文。目录由调用方持有，不做拷贝，校验期间不得修改。
        /// </summary>
        public ValidationContext(
            HashSet<string>? definedFunctionIds = null,
            HashSet<string>? definedActionIds = null)
        {
            DefinedFunctionIds = definedFunctionIds;
            DefinedActionIds = definedActionIds;
        }
    }
}
