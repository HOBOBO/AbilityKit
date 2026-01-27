using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Emilia.Reflection.Editor;
using MonoHook;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Emilia.Kit.Editor
{
    public class GraphView_Hook : GraphView_Internals
    {
        [InitializeOnLoadMethod]
        static void InstallationHook()
        {
            Type graphViewType = typeof(GraphView);
            Type graphViewHookType = typeof(GraphView_Hook);

            HookUpdateContentZoomer(graphViewType, graphViewHookType);
            HookOnKeyDownShortcut(graphViewType, graphViewHookType);
        }

        private static void HookUpdateContentZoomer(Type graphViewType, Type graphViewHookType)
        {
            MethodInfo methodInfo = graphViewType.GetMethod("UpdateContentZoomer", BindingFlags.Instance | BindingFlags.NonPublic);

            MethodInfo hookInfo = graphViewHookType.GetMethod(nameof(UpdateContentZoomer_Hook), BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo proxyInfo = graphViewHookType.GetMethod(nameof(UpdateContentZoomer_Proxy), BindingFlags.Instance | BindingFlags.NonPublic);

            MethodHook hook = new(methodInfo, hookInfo, proxyInfo);
            hook.Install();
        }

        private static void HookOnKeyDownShortcut(Type graphViewType, Type graphViewHookType)
        {
            MethodInfo methodInfo = graphViewType.GetMethod("OnKeyDownShortcut", BindingFlags.Instance | BindingFlags.NonPublic);

            MethodInfo hookInfo = graphViewHookType.GetMethod(nameof(OnKeyDownShortcut_Hook), BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo proxyInfo = graphViewHookType.GetMethod(nameof(OnKeyDownShortcut_Proxy), BindingFlags.Instance | BindingFlags.NonPublic);

            MethodHook hook = new(methodInfo, hookInfo, proxyInfo);
            hook.Install();
        }

        private void UpdateContentZoomer_Hook()
        {
            if (ReflectUtility.Invoke(this, nameof(OverrideUpdateContentZoomer), new object[] { }) is bool result && result) return;
            UpdateContentZoomer_Proxy();
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private void UpdateContentZoomer_Proxy()
        {
            Debug.Log(nameof(UpdateContentZoomer_Proxy));
        }

        private void OnKeyDownShortcut_Hook(KeyDownEvent evt)
        {
            if (ReflectUtility.Invoke(this, nameof(OverrideOnKeyDownShortcut), new object[] { }) is bool result && result) return;
            OnKeyDownShortcut_Proxy(evt);
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private void OnKeyDownShortcut_Proxy(KeyDownEvent evt)
        {
            Debug.Log(nameof(OnKeyDownShortcut_Proxy));
        }

        protected virtual bool OverrideUpdateContentZoomer() => false;

        protected virtual bool OverrideOnKeyDownShortcut(KeyDownEvent evt) => false;
    }
}