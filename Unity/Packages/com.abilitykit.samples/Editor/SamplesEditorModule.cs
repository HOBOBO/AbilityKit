#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Editor.Platform.Core;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Samples.Editor
{
    /// <summary>
    /// 把示例目录接进编辑器平台：注册一个 Hub 面板。
    ///
    /// 面板刻意做得很薄——只显示一行说明和一个「打开」按钮。示例目录本身有独立窗口，
    /// 在 Hub 面板里重画整棵目录会让每次重绘都去解析 manifest。
    ///
    /// 这里**不注册菜单贡献**：<c>SampleCatalogWindow</c> 自带
    /// <c>[MenuItem("Window/AbilityKit/...")]</c>，而 Hub 的
    /// <c>EditorMenuItemDiscovery</c> 会扫描该根路径下的真实 MenuItem，
    /// 再注册一条同路径贡献会在 Hub 列表里重复出现。
    /// </summary>
    public sealed class SamplesEditorModule : IEditorModule
    {
        internal const string PanelId = "abilitykit.samples.panel.catalog";

        private readonly List<IDisposable> _registrations = new List<IDisposable>();

        public EditorModuleDescriptor Descriptor { get; } = new EditorModuleDescriptor(
            SamplesEditorLocalization.ModuleId,
            "abilitykit.samples.module.name",
            order: 240);

        public void OnRegister(IEditorPlatformContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (_registrations.Count != 0)
            {
                throw new InvalidOperationException("示例目录编辑器模块已注册。");
            }

            try
            {
                _registrations.Add(SamplesEditorLocalization.RegisterSource());
                _registrations.Add(context.Panels.Register(new EditorPanelContribution(
                    PanelId,
                    "abilitykit.samples.panel.browse",
                    createVisualElement: null,
                    drawImGui: DrawCatalogPanel,
                    order: 240)));
            }
            catch
            {
                OnUnregister();
                throw;
            }
        }

        public void OnUnregister()
        {
            for (var index = _registrations.Count - 1; index >= 0; index--)
            {
                _registrations[index].Dispose();
            }

            _registrations.Clear();
        }

        private static void DrawCatalogPanel(Rect rect)
        {
            var localization = SamplesEditorLocalization.Localization;

            GUILayout.BeginArea(rect);
            GUILayout.Label(localization.Get("abilitykit.samples.panel.browse"), EditorStyles.boldLabel);
            GUILayout.Label(localization.Get("abilitykit.samples.panel.hint"), EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(4f);
            if (GUILayout.Button(localization.Get("abilitykit.samples.panel.open")))
            {
                SampleCatalogWindow.Open();
            }

            GUILayout.EndArea();
        }
    }

    /// <summary>
    /// 编辑器加载时把模块注册进平台。与其它编辑器模块同构。
    /// </summary>
    [InitializeOnLoad]
    internal static class SamplesEditorModuleBootstrap
    {
        private static readonly IDisposable Registration;

        static SamplesEditorModuleBootstrap()
        {
            Registration = AbilityKitEditorPlatform.Modules.Register(new SamplesEditorModule());
        }

        internal static void EnsureRegistered()
        {
            _ = Registration;
        }
    }
}
