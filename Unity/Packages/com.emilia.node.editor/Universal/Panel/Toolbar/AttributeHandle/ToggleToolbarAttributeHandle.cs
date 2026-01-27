using System;
using System.Reflection;
using Emilia.Node.Attributes;
using Emilia.Node.Editor;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 工具栏Toggle特性处理
    /// </summary>
    public class ToggleToolbarAttributeHandle : ToolbarViewControlAttributeHandle
    {
        public override void OnHandle(ToolbarView toolbarView, EditorGraphView editorGraphView)
        {
            Type graphAssetType = editorGraphView.graphAsset.GetType();

            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            PropertyInfo[] propertyInfos = graphAssetType.GetProperties(bindingFlags);
            int methodInfosLength = propertyInfos.Length;
            for (int i = 0; i < methodInfosLength; i++)
            {
                PropertyInfo propertyInfo = propertyInfos[i];
                if (! propertyInfo.CanRead || ! propertyInfo.CanWrite) continue;

                ToggleToolbarAttribute toggleToolbarAttribute = propertyInfo.GetCustomAttribute<ToggleToolbarAttribute>();
                if (toggleToolbarAttribute == null) continue;

                Func<bool> getMethod = Delegate.CreateDelegate(typeof(Func<bool>), propertyInfo.GetMethod) as Func<bool>;
                if (getMethod == null) return;

                Action<bool> setMethod = Delegate.CreateDelegate(typeof(Action<bool>), propertyInfo.SetMethod) as Action<bool>;
                if (setMethod == null) return;

                ToggleToolbarViewControl toggleToolbarViewControl = new(toggleToolbarAttribute.displayName, getMethod, setMethod);
                toolbarView.AddControl(toggleToolbarViewControl, toggleToolbarAttribute.position);
            }
        }
    }
}