using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Validation
{
    /// <summary>
    /// 校验上下文
    /// 提供校验器所需的只读信息访问
    /// </summary>
    public readonly struct ValidationContext<TCtx>
    {
        /// <summary>
        /// 已定义的 FunctionId 集合（字符串形式）
        /// </summary>
        public HashSet<string> DefinedFunctionIds { get; }

        /// <summary>
        /// 已注册的 ActionId 集合（字符串形式）
        /// </summary>
        public HashSet<string> DefinedActionIds { get; }

        /// <summary>
        /// 已定义的 EventKey 集合
        /// </summary>
        public HashSet<string> DefinedEventKeys { get; }

        /// <summary>
        /// 最大嵌套深度限制（UGC）
        /// </summary>
        public int MaxNestingDepth { get; }

        /// <summary>
        /// 最大节点数限制（UGC）
        /// </summary>
        public int MaxNodeCount { get; }

        /// <summary>
        /// 最大复杂度限制（UGC）
        /// </summary>
        public int MaxComplexity { get; }

        /// <summary>
        /// 最大递归深度限制（UGC）
        /// </summary>
        public int MaxRecursionDepth { get; }

        /// <summary>
        /// 最大 Action 数量限制
        /// </summary>
        public int MaxActionCount { get; }

        /// <summary>
        /// 是否启用严格模式（严格模式下更多检查会作为错误而非警告）
        /// </summary>
        public bool StrictMode { get; }

        /// <summary>
        /// 创建校验上下文
        /// </summary>
        public ValidationContext(
            HashSet<string> definedFunctionIds,
            HashSet<string> definedActionIds,
            HashSet<string> definedEventKeys,
            int maxNestingDepth = 10,
            int maxNodeCount = 100,
            int maxComplexity = 50,
            int maxRecursionDepth = 5,
            int maxActionCount = 20,
            bool strictMode = false)
        {
            DefinedFunctionIds = definedFunctionIds ?? new HashSet<string>();
            DefinedActionIds = definedActionIds ?? new HashSet<string>();
            DefinedEventKeys = definedEventKeys ?? new HashSet<string>();
            MaxNestingDepth = maxNestingDepth;
            MaxNodeCount = maxNodeCount;
            MaxComplexity = maxComplexity;
            MaxRecursionDepth = maxRecursionDepth;
            MaxActionCount = maxActionCount;
            StrictMode = strictMode;
        }

        /// <summary>
        /// 创建 UGC 校验上下文（更严格的限制）
        /// </summary>
        public static ValidationContext<TCtx> CreateForUgc(
            HashSet<string> definedFunctionIds = null,
            HashSet<string> definedActionIds = null,
            HashSet<string> definedEventKeys = null)
        {
            return new ValidationContext<TCtx>(
                definedFunctionIds, definedActionIds, definedEventKeys,
                maxNestingDepth: 5,
                maxNodeCount: 50,
                maxComplexity: 30,
                maxRecursionDepth: 3,
                maxActionCount: 10,
                strictMode: false);
        }

        /// <summary>
        /// 创建开发环境校验上下文（宽松限制）
        /// </summary>
        public static ValidationContext<TCtx> CreateForDevelopment(
            HashSet<string> definedFunctionIds = null,
            HashSet<string> definedActionIds = null,
            HashSet<string> definedEventKeys = null)
        {
            return new ValidationContext<TCtx>(
                definedFunctionIds, definedActionIds, definedEventKeys,
                maxNestingDepth: 50,
                maxNodeCount: 500,
                maxComplexity: 200,
                maxRecursionDepth: 10,
                maxActionCount: 100,
                strictMode: false);
        }
    }
}
