using System;
using System.Reflection;
using Emilia.Node.Attributes;
using Emilia.Node.Editor;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 工具栏自定义GUI特性处理
    /// </summary>
    public class CustomToolbarAttributeHandle : ToolbarViewControlAttributeHandle
    {
        public override void OnHandle(ToolbarView toolbarView, EditorGraphView editorGraphView)
        {
            Type graphAssetType = editorGraphView.graphAsset.GetType();

            MethodInfo[] methodInfos = graphAssetType.GetMethods();
            int methodInfosLength = methodInfos.Length;
            for (int i = 0; i < methodInfosLength; i++)
            {
                MethodInfo methodInfo = methodInfos[i];
                CustomToolbarAttribute customToolbarAttribute = methodInfo.GetCustomAttribute<CustomToolbarAttribute>();
                if (customToolbarAttribute == null) continue;
                Action action = Delegate.CreateDelegate(typeof(Action), methodInfo) as Action;
                if (action == null) return;
                CustomToolbarViewControl customToolbarViewControl = new(action);
                toolbarView.AddControl(customToolbarViewControl, customToolbarAttribute.position);
            }
        }
    }
}