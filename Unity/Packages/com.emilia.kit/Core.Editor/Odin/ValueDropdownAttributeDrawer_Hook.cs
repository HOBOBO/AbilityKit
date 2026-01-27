using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Emilia.Reflection.Editor;
using MonoHook;
using Sirenix.OdinInspector.Editor;
using Sirenix.OdinInspector.Editor.Drawers;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace Emilia.Kit.Editor
{
    public class ValueDropdownAttributeDrawer_Hook : ValueDropdownAttributeDrawer_Internal
    {
        [InitializeOnLoadMethod]
        private static void InitializeHook()
        {
            Type type = typeof(ValueDropdownAttributeDrawer);
            Type hookType = typeof(ValueDropdownAttributeDrawer_Hook);
            
            HookShowSelector(type, hookType);
        }

        protected static void HookShowSelector(Type type, Type hookType)
        {
            MethodInfo methodInfo = type.GetMethod("ShowSelector", BindingFlags.Instance | BindingFlags.NonPublic);

            MethodInfo hookInfo = hookType.GetMethod(nameof(ShowSelector_Hook), BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo proxyInfo = hookType.GetMethod(nameof(ShowSelector_Proxy), BindingFlags.Instance | BindingFlags.NonPublic);

            MethodHook hook = new(methodInfo, hookInfo, proxyInfo);
            hook.Install();
        }

        private OdinSelector<object> ShowSelector_Hook(Rect rect)
        {
            if (Property.GetAttribute<ValueDropdownPinYinAttribute>() == null) return ShowSelector_Proxy(rect);

            var selector = this.CreateSelector_Internal();

            rect.x = (int) rect.x;
            rect.y = (int) rect.y;
            rect.width = (int) rect.width;
            rect.height = (int) rect.height;

            if (this.Attribute.AppendNextDrawer && ! this.GetIsList_Internal())
            {
                rect.xMax = GUIHelper.GetCurrentLayoutRect().xMax;
            }

            selector.SelectionTree.SetSearchFunction(item => {
                string target = item.SearchString;
                string input = selector.SelectionTree.Config.SearchTerm;
                return SearchUtility.SmartSearch(target, input);
            });

            selector.ShowInPopup(rect, new Vector2(this.Attribute.DropdownWidth, this.Attribute.DropdownHeight));
            return selector;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private OdinSelector<object> ShowSelector_Proxy(Rect rect)
        {
            Debug.Log(nameof(ShowSelector_Proxy));
            return default;
        }
    }
}