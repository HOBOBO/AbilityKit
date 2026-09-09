using AbilityKit.Editor.Platform.Core;
using AbilityKit.HFSM.Editor.Bootstrap;
using NUnit.Framework;

namespace AbilityKit.Tests
{
    public sealed class EditorModuleIntegrationTests
    {
        [Test]
        public void RegistersAuthoringCreationAndRuntimeEntriesWithEditorPlatform()
        {
            var menus = new EditorContributionRegistry<EditorMenuContribution>();
            var panels = new EditorContributionRegistry<EditorPanelContribution>();
            var context = new EditorPlatformContext(
                new EditorServiceRegistry(),
                menus,
                panels);
            var module = new EditorModule();

            try
            {
                module.OnRegister(context);

                Assert.That(module.Descriptor.Id, Is.EqualTo(EditorModule.ModuleId));
                Assert.That(menus.TryGet(EditorModule.AuthoringMenuId, out var authoringMenu), Is.True);
                Assert.That(menus.TryGet(EditorModule.CreateMenuId, out var createMenu), Is.True);
                Assert.That(menus.TryGet(EditorModule.RuntimeMenuId, out var runtimeMenu), Is.True);
                Assert.That(authoringMenu.Path, Is.EqualTo("Window/AbilityKit/HFSM 状态机编辑器"));
                Assert.That(createMenu.Path, Is.EqualTo("Assets/Create/AbilityKit/HFSM 状态机图..."));
                Assert.That(runtimeMenu.Path, Is.EqualTo("Window/AbilityKit/HFSM 运行时监视器"));
                Assert.That(
                    AbilityKitEditorPlatform.Localization.Get("abilitykit.hfsm.panel.authoring"),
                    Is.EqualTo("HFSM 编辑"));
                Assert.That(panels.TryGet(EditorModule.AuthoringPanelId, out var authoring), Is.True);
                Assert.That(panels.TryGet(EditorModule.RuntimePanelId, out var runtime), Is.True);
                Assert.That(authoring.CreateVisualElement(), Is.Not.Null);
                Assert.That(runtime.CreateVisualElement(), Is.Not.Null);
            }
            finally
            {
                module.OnUnregister();
            }

            Assert.That(menus.Items, Is.Empty);
            Assert.That(panels.Items, Is.Empty);
        }
    }
}
