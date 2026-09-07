#if UNITY_EDITOR
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

    /// <summary>项目自定义积木字段渲染器的注册表（框架窗口的 <c>DrawBlockFields</c> 反射兜底前先查这里）。</summary>
    public static class BattleBlockFieldRendererRegistry
    {
        /// <summary>当前注册的渲染器；未注册时项目积木走反射兜底。</summary>
        public static IBattleBlockFieldRenderer? Renderer { get; set; }
    }
}
#endif
