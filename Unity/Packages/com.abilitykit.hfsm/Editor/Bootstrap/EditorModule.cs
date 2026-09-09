using System;
using System.Collections.Generic;
using AbilityKit.Editor.Platform.Core;
using AbilityKit.HFSM.Editor.RuntimeMonitor;
using AbilityKit.HFSM.Graph;
using AbilityKit.HFSM.Visualization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.HFSM.Editor.Bootstrap
{
    public sealed class EditorModule : IEditorModule
    {
        public const string ModuleId = "abilitykit.hfsm";
        public const string AuthoringMenuId = ModuleId + ".menu.authoring";
        public const string CreateMenuId = ModuleId + ".menu.create";
        public const string RuntimeMenuId = ModuleId + ".menu.runtime";
        public const string AuthoringPanelId = ModuleId + ".panel.authoring";
        public const string RuntimePanelId = ModuleId + ".panel.runtime";

        private readonly List<IDisposable> _registrations = new List<IDisposable>();

        public EditorModuleDescriptor Descriptor { get; } = new EditorModuleDescriptor(
            ModuleId,
            "abilitykit.hfsm.module.name",
            order: 220);

        public void OnRegister(IEditorPlatformContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (_registrations.Count != 0)
                throw new InvalidOperationException("HFSM 编辑器模块已注册。");

            try
            {
                _registrations.Add(EditorLocalization.RegisterSource());
                _registrations.Add(context.Menus.Register(new EditorMenuContribution(
                    AuthoringMenuId,
                    "Window/AbilityKit/HFSM 状态机编辑器",
                    StateMachineEditorWindow.OpenWindow,
                    order: 220)));
                _registrations.Add(context.Menus.Register(new EditorMenuContribution(
                    CreateMenuId,
                    "Assets/Create/AbilityKit/HFSM 状态机图...",
                    StateMachineEditorWindow.CreateGraphAssetAndOpen,
                    order: 221)));
                _registrations.Add(context.Menus.Register(new EditorMenuContribution(
                    RuntimeMenuId,
                    "Window/AbilityKit/HFSM 运行时监视器",
                    RuntimeMonitorWindow.OpenWindow,
                    order: 222)));
                _registrations.Add(context.Panels.Register(new EditorPanelContribution(
                    AuthoringPanelId,
                    "abilitykit.hfsm.panel.authoring",
                    CreateAuthoringPanel,
                    order: 220)));
                _registrations.Add(context.Panels.Register(new EditorPanelContribution(
                    RuntimePanelId,
                    "abilitykit.hfsm.panel.runtime",
                    CreateRuntimePanel,
                    order: 221)));
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
                _registrations[index].Dispose();
            _registrations.Clear();
        }

        [MenuItem("Assets/Create/AbilityKit/HFSM 状态机图...", false, 220)]
        private static void CreateGraphFromMenu()
        {
            StateMachineEditorWindow.CreateGraphAssetAndOpen();
        }

        private static VisualElement CreateAuthoringPanel()
        {
            var localization = EditorLocalization.Localization;
            var root = CreatePanelRoot();
            var graphField = new ObjectField(localization.Get("abilitykit.hfsm.panel.graph"))
            {
                objectType = typeof(GraphAsset),
                allowSceneObjects = false,
                value = Selection.activeObject as GraphAsset
            };
            root.Add(graphField);

            var hint = new Label(localization.Get("abilitykit.hfsm.panel.noGraph"));
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.marginTop = 4f;
            root.Add(hint);

            var actions = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 8f } };
            var open = new Button(() => StateMachineEditorWindow.Open(graphField.value as GraphAsset))
            {
                text = localization.Get("abilitykit.hfsm.panel.open")
            };
            open.style.flexGrow = 1f;
            open.SetEnabled(graphField.value is GraphAsset);
            graphField.RegisterValueChangedCallback(evt => open.SetEnabled(evt.newValue is GraphAsset));

            var create = new Button(StateMachineEditorWindow.CreateGraphAssetAndOpen)
            {
                text = localization.Get("abilitykit.hfsm.panel.create")
            };
            create.style.flexGrow = 1f;
            actions.Add(open);
            actions.Add(create);
            root.Add(actions);
            return root;
        }

        private static VisualElement CreateRuntimePanel()
        {
            var localization = EditorLocalization.Localization;
            var root = CreatePanelRoot();
            var status = new Label();
            status.style.marginBottom = 8f;
            root.Add(status);
            root.Add(new Button(RuntimeMonitorWindow.OpenWindow)
            {
                text = localization.Get("abilitykit.hfsm.panel.debug")
            });

            Action refresh = () => status.text = localization.Format(
                "abilitykit.hfsm.panel.runtimeStatus",
                LiveRegistry.Count);
            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                LiveRegistry.Changed += refresh;
                refresh();
            });
            root.RegisterCallback<DetachFromPanelEvent>(_ => LiveRegistry.Changed -= refresh);
            refresh();
            return root;
        }

        private static VisualElement CreatePanelRoot()
        {
            var root = new VisualElement();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;
            return root;
        }
    }

    [InitializeOnLoad]
    internal static class EditorModuleBootstrap
    {
        private static readonly IDisposable Registration;

        static EditorModuleBootstrap()
        {
            Registration = AbilityKitEditorPlatform.Modules.Register(new EditorModule());
        }

        internal static void EnsureRegistered()
        {
            _ = Registration;
        }
    }
}
