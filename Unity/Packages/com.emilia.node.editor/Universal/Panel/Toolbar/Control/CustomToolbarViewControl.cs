using System;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 工具栏自定义控件
    /// </summary>
    public class CustomToolbarViewControl : ToolbarViewControl
    {
        /// <summary>
        /// 自定义绘制事件
        /// </summary>
        public Action onCustom;

        public CustomToolbarViewControl(Action onCustom)
        {
            this.onCustom = onCustom;
        }

        public override void OnDraw()
        {
            onCustom?.Invoke();
        }
    }
}