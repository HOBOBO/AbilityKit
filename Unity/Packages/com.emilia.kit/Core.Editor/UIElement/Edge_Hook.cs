using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Emilia.Reflection.Editor;
using MonoHook;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Emilia.Kit.Editor
{
    public class Edge_Hook : Edge_Internals
    {
        [InitializeOnLoadMethod]
        static void InstallationHook()
        {
            Type edgeType = typeof(Edge);
            Type graphViewHookType = typeof(Edge_Hook);

            HookCtor(edgeType, graphViewHookType);
        }

        private static void HookCtor(Type edgeType, Type graphViewHookType)
        {
            MethodBase methodInfo = edgeType.GetConstructor(new Type[] { });

            MethodInfo hookInfo = graphViewHookType.GetMethod(nameof(Ctor_Hook), BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo proxyInfo = graphViewHookType.GetMethod(nameof(Ctor_Proxy), BindingFlags.Instance | BindingFlags.NonPublic);

            MethodHook hook = new(methodInfo, hookInfo, proxyInfo);
            hook.Install();
        }

        private void Ctor_Hook()
        {
            if (ReflectUtility.Invoke(this, nameof(OverrideCtor), new object[] { }) is bool result && result) return;
            Ctor_Proxy();
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private void Ctor_Proxy()
        {
            Debug.Log(nameof(Ctor_Proxy));
        }

        protected virtual bool OverrideCtor() => false;

        private static MethodBase graphElementCtor;

        protected void BaseCtor()
        {
            SetDefaultValue();
            BaseCtorMethod();
        }

        protected void SetDefaultValue()
        {
            m_EdgeWidth_Internal = s_DefaultEdgeWidth_Internal;
            m_SelectedColor_Internal = s_DefaultSelectedColor_Internal;
            m_DefaultColor_Internal = s_DefaultColor_Internal;
            m_GhostColor_Internal = s_DefaultGhostColor_Internal;
        }

        protected void BaseCtorMethod()
        {
            if (graphElementCtor == null) graphElementCtor = typeof(GraphElement).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { }, null);
            graphElementCtor.Invoke(this, null);
        }
    }
}