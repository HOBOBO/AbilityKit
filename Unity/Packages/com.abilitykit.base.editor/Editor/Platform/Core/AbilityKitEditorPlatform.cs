#if UNITY_EDITOR
using System.Collections.Generic;
using AbilityKit.Editor.Platform.Commands;
using AbilityKit.Editor.Platform.Localization;
using AbilityKit.Editor.Platform.State;
using UnityEditor;

namespace AbilityKit.Editor.Platform.Core
{
    /// <summary>
    /// Process-local composition root for editor infrastructure. Domain packages register
    /// explicitly and retain the returned handles for symmetric unregistration.
    /// </summary>
    [InitializeOnLoad]
    public static class AbilityKitEditorPlatform
    {
        private const string LanguageOverrideKey = "language.override";
        private static readonly EditorPrefsUserStateStore UserPreferences;

        static AbilityKitEditorPlatform()
        {
            Services = new EditorServiceRegistry();
            Menus = new EditorContributionRegistry<EditorMenuContribution>();
            Panels = new EditorContributionRegistry<EditorPanelContribution>();
            Context = new EditorPlatformContext(Services, Menus, Panels);
            Modules = new EditorModuleRegistry(Context);
            Commands = new EditorCommandRegistry();
            Localization = new EditorLocalizationService
            {
                ProjectDefaultLanguage = EditorPlatformProjectSettings.instance.DefaultLanguage
            };

            UserPreferences = new EditorPrefsUserStateStore("platform", "localization");
            Localization.UserLanguageOverride = UserPreferences.GetString(LanguageOverrideKey);
            Localization.LanguageChanged += PersistLanguageSelection;
            Localization.RegisterSource(CreatePlatformLocalizationSource());

            Services.Register<IEditorLocalization>(Localization);
            Services.Register(Localization);
            Services.Register(Commands);
            Services.Register(Modules);
        }

        public static EditorServiceRegistry Services { get; }
        public static EditorContributionRegistry<EditorMenuContribution> Menus { get; }
        public static EditorContributionRegistry<EditorPanelContribution> Panels { get; }
        public static EditorPlatformContext Context { get; }
        public static EditorModuleRegistry Modules { get; }
        public static EditorCommandRegistry Commands { get; }
        public static EditorLocalizationService Localization { get; }

        public static void SetProjectDefaultLanguage(string language)
        {
            EditorPlatformProjectSettings.instance.DefaultLanguage = language;
            Localization.ProjectDefaultLanguage = EditorPlatformProjectSettings.instance.DefaultLanguage;
        }

        private static void PersistLanguageSelection()
        {
            UserPreferences.SetString(LanguageOverrideKey, Localization.UserLanguageOverride);
        }

        private static DictionaryEditorLocalizationSource CreatePlatformLocalizationSource()
        {
            return new DictionaryEditorLocalizationSource(
                "abilitykit.editor.platform",
                new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    ["en"] = new Dictionary<string, string>
                    {
                        ["abilitykit.editor.search.tooltip"] = "Search",
                        ["abilitykit.editor.diagnostics.empty.title"] = "No diagnostics",
                        ["abilitykit.editor.diagnostics.empty.message"] = "No diagnostics match the current filter.",
                        ["abilitykit.editor.diagnostics.locate"] = "Locate",
                        ["abilitykit.editor.diagnostics.fix"] = "Fix"
                    },
                    ["zh-CN"] = new Dictionary<string, string>
                    {
                        ["abilitykit.editor.search.tooltip"] = "搜索",
                        ["abilitykit.editor.diagnostics.empty.title"] = "无诊断项",
                        ["abilitykit.editor.diagnostics.empty.message"] = "当前筛选条件下没有诊断项。",
                        ["abilitykit.editor.diagnostics.locate"] = "定位",
                        ["abilitykit.editor.diagnostics.fix"] = "修复"
                    }
                });
        }
    }
}
#endif
