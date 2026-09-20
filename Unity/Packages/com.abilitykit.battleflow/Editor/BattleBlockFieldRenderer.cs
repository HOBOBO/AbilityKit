#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.BattleFlow;

namespace AbilityKit.BattleFlow.Editor
{
    /// <summary>
    /// 项目自定义积木的字段渲染钩子：框架窗口不知道项目积木（如 MOBA 断言积木）的字段语义，
    /// 项目实现它用下拉框/必填等友好控件渲染，替代框架的反射兜底。
    /// </summary>
    public interface IBattleBlockFieldRenderer
    {
        /// <summary>渲染一个积木的可编辑字段；返回 true 表示已处理，框架不再走反射兜底。</summary>
        bool TryDrawFields(BattleBlock block);
    }

    /// <summary>Provides project renderers with the surrounding document needed for config-aware fields.</summary>
    public sealed class BattleBlockFieldContext
    {
        public BattleBlockFieldContext(
            IReadOnlyList<BattleBlock> authoring,
            IReadOnlyList<BattleBlock> setup)
        {
            Authoring = authoring ?? Array.Empty<BattleBlock>();
            Setup = setup ?? Array.Empty<BattleBlock>();
        }

        public IReadOnlyList<BattleBlock> Authoring { get; }
        public IReadOnlyList<BattleBlock> Setup { get; }
    }

    /// <summary>Optional richer renderer contract for fields that depend on other blocks in the document.</summary>
    public interface IContextualBattleBlockFieldRenderer : IBattleBlockFieldRenderer
    {
        bool TryDrawFields(BattleBlock block, BattleBlockFieldContext context);
    }

    /// <summary>项目自定义积木字段渲染器的注册表（框架窗口的 <c>DrawBlockFields</c> 反射兜底前先查这里）。</summary>
    public static class BattleBlockFieldRendererRegistry
    {
        /// <summary>当前注册的渲染器；未注册时项目积木走反射兜底。</summary>
        public static IBattleBlockFieldRenderer? Renderer { get; set; }
    }
}
#endif
