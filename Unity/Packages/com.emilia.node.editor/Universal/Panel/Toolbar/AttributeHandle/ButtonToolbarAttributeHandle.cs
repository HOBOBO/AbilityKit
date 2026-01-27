using System;
using System.Reflection;
using Emilia.Node.Attributes;
using Emilia.Node.Editor;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 工具栏按钮特性处理
    /// </summary>
    public class ButtonToolbarAttributeHandle : ToolbarViewControlAttributeHandle
    {
        public override void OnHandle(ToolbarView toolbarView, EditorGraphView editorGraphView)
        {
            Type graphAssetType = editorGraphView.graphAsset.GetType();

            MethodInfo[] methodInfos = graphAssetType.GetMethods();
            int methodInfosLength = methodInfos.Length;
            for (int i = 0; i < methodInfosLength; i++)
            {
                MethodInfo methodInfo = methodInfos[i];
                ButtonToolbarAttribute buttonToolbarAttribute = methodInfo.GetCustomAttribute<ButtonToolbarAttribute>();
                if (buttonToolbarAttribute == null) continue;
                Action action = Delegate.CreateDelegate(typeof(Action), methodInfo) as Action;
                if (action == null) return;
                ButtonToolbarViewControl buttonToolbarViewControl = new(buttonToolbarAttribute.displayName, action);
                toolbarView.AddControl(buttonToolbarViewControl, buttonToolbarAttribute.position);
            }
        }
    }
}