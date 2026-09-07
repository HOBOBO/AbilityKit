#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Editor.Platform.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

using AbilityKit.BehaviorTree.Editor;
using UnityEngine.Scripting.APIUpdating;
namespace AbilityKit.BehaviorTree.Editor.Bootstrap
{
    /// <summary>
    /// Behavior Tree 编辑器在 Editor Platform 中的组合根。
    /// 仅负责生命周期与贡献注册，不持有 authoring/runtime 领域状态。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtEditorModule")]
    public sealed class EditorModule : IEditorModule
    {
        public const string ModuleId = "abilitykit.behaviortree";
        public const string ObservationMenuId = ModuleId + ".menu.observation";
        public const string CreateMenuId = ModuleId + ".menu.create";
        public const string ObservationPanelId = ModuleId + ".panel.observation";
        public const string CreatePanelId = ModuleId + ".panel.create";

        private readonly List<IDisposable> _registrations = new();

        public EditorModuleDescriptor Descriptor { get; } = new(
            ModuleId,
            "abilitykit.behaviortree.module.name",
            order: 200);

        public void OnRegister(IEditorPlatformContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (_registrations.Count != 0)
                throw new InvalidOperationException("Behavior Tree editor module is already registered.");

            try
            {
                _registrations.Add(EditorLocalization.RegisterSource());
                _registrations.Add(context.Menus.Register(new EditorMenuContribution(
                    ObservationMenuId,
                    "Window/AbilityKit/Behavior Tree Observation",
                    OpenObservation,
                    order: 200)));
                _registrations.Add(context.Menus.Register(new EditorMenuContribution(
                    CreateMenuId,
                    "Assets/AbilityKit/Behavior Tree/Create Tree Wizard",
                    AuthoringCreateWizard.Open,
                    order: 210)));
                _registrations.Add(context.Panels.Register(new EditorPanelContribution(
                    ObservationPanelId,
                    "abilitykit.behaviortree.panel.observation",
                    createVisualElement: () => CreateLauncherPanel(
                        "abilitykit.behaviortree.panel.observation",
                        "abilitykit.behaviortree.panel.observation.open",
                        OpenObservation),
                    order: 200)));
                _registrations.Add(context.Panels.Register(new EditorPanelContribution(
                    CreatePanelId,
                    "abilitykit.behaviortree.panel.create",
                    createVisualElement: () => CreateLauncherPanel(
                        "abilitykit.behaviortree.panel.create",
                        "abilitykit.behaviortree.panel.create.open",
                        AuthoringCreateWizard.Open),
                    order: 210)));
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

        private static void OpenObservation()
        {
            var window = EditorWindow.GetWindow<DebugObservationWindow>();
            window.titleContent = new GUIContent("BT Observation");
            window.minSize = new Vector2(640f, 420f);
            window.Show();
        }

        private static VisualElement CreateLauncherPanel(string titleKey, string buttonKey, Action open)
        {
            var localization = EditorLocalization.Localization;
            var root = new VisualElement { name = "abilitykit-behaviortree-launcher" };
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.Add(new Label(localization.Get(titleKey))
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold }
            });
            root.Add(new Button(open) { text = localization.Get(buttonKey) });
            return root;
        }
    }

    /// <summary>Unity domain-load bootstrap. The module registry owns symmetric teardown.</summary>
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
