using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Explain
{
    [Serializable]
    public sealed class ExplainResolveContext
    {
        public PipelineItemKey Key;

        public Dictionary<string, string> Values;

        /// <summary>
        /// 强类型修饰预览输入（"加成效果"通道）。业务 ContextEditor 写入、resolver 读取并计算。
        /// 与 Values 区分：Values 是通用 UI/开关字符串通道；Modifiers 是结构化数值修饰通道。
        /// </summary>
        public List<ModifierPreviewInput> Modifiers;

        public static ExplainResolveContext For(in PipelineItemKey key)
        {
            return new ExplainResolveContext { Key = key };
        }
    }
}
