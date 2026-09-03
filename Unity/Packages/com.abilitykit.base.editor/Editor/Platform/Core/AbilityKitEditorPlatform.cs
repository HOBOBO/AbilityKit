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
                        ["abilitykit.editor.diagnostics.fix"] = "Fix",
                        ["abilitykit.editor.sourceSync.title"] = "Source Sync",
                        ["abilitykit.editor.sourceSync.path"] = "Path",
                        ["abilitykit.editor.sourceSync.unbound"] = "<unbound>",
                        ["abilitykit.editor.sourceSync.import"] = "Import",
                        ["abilitykit.editor.sourceSync.export"] = "Export",
                        ["abilitykit.editor.sourceSync.copyPath"] = "Copy Path",
                        ["abilitykit.editor.sourceSync.reveal"] = "Reveal",
                        ["abilitykit.editor.sourceSync.state.Untracked"] = "Untracked",
                        ["abilitykit.editor.sourceSync.state.InSync"] = "In Sync",
                        ["abilitykit.editor.sourceSync.state.LocalChanged"] = "Local Changed",
                        ["abilitykit.editor.sourceSync.state.SourceChanged"] = "Source Changed",
                        ["abilitykit.editor.sourceSync.state.Conflict"] = "Conflict",
                        ["abilitykit.editor.sourceSync.state.SourceMissing"] = "Source Missing",
                        ["abilitykit.editor.sourceSync.state.InvalidSource"] = "Invalid Source",
                        ["abilitykit.editor.sourceSync.message.Untracked"] = "The source has no synchronized baseline.",
                        ["abilitykit.editor.sourceSync.message.InSync"] = "Local and source content are synchronized.",
                        ["abilitykit.editor.sourceSync.message.LocalChanged"] = "Local changes are ready to export.",
                        ["abilitykit.editor.sourceSync.message.SourceChanged"] = "External changes are ready to import.",
                        ["abilitykit.editor.sourceSync.message.Conflict"] = "Local and source content have diverged.",
                        ["abilitykit.editor.sourceSync.message.SourceMissing"] = "The bound source file is missing.",
                        ["abilitykit.editor.sourceSync.message.InvalidSource"] = "The source cannot be read."
                    },
                    ["zh-CN"] = new Dictionary<string, string>
                    {
                        ["abilitykit.editor.search.tooltip"] = "搜索",
                        ["abilitykit.editor.diagnostics.empty.title"] = "无诊断项",
                        ["abilitykit.editor.diagnostics.empty.message"] = "当前筛选条件下没有诊断项。",
                        ["abilitykit.editor.diagnostics.locate"] = "定位",
                        ["abilitykit.editor.diagnostics.fix"] = "修复",
                        ["abilitykit.editor.sourceSync.title"] = "源同步",
                        ["abilitykit.editor.sourceSync.path"] = "路径",
                        ["abilitykit.editor.sourceSync.unbound"] = "<未绑定>",
                        ["abilitykit.editor.sourceSync.import"] = "导入",
                        ["abilitykit.editor.sourceSync.export"] = "导出",
                        ["abilitykit.editor.sourceSync.copyPath"] = "复制路径",
                        ["abilitykit.editor.sourceSync.reveal"] = "定位文件",
                        ["abilitykit.editor.sourceSync.state.Untracked"] = "未跟踪",
                        ["abilitykit.editor.sourceSync.state.InSync"] = "已同步",
                        ["abilitykit.editor.sourceSync.state.LocalChanged"] = "本地已修改",
                        ["abilitykit.editor.sourceSync.state.SourceChanged"] = "源文件已修改",
                        ["abilitykit.editor.sourceSync.state.Conflict"] = "冲突",
                        ["abilitykit.editor.sourceSync.state.SourceMissing"] = "源文件缺失",
                        ["abilitykit.editor.sourceSync.state.InvalidSource"] = "源文件无效",
                        ["abilitykit.editor.sourceSync.message.Untracked"] = "源文件尚无同步基线。",
                        ["abilitykit.editor.sourceSync.message.InSync"] = "本地内容与源文件已同步。",
                        ["abilitykit.editor.sourceSync.message.LocalChanged"] = "本地修改可导出到源文件。",
                        ["abilitykit.editor.sourceSync.message.SourceChanged"] = "外部修改可导入到本地。",
                        ["abilitykit.editor.sourceSync.message.Conflict"] = "本地内容与源文件已分叉。",
                        ["abilitykit.editor.sourceSync.message.SourceMissing"] = "绑定的源文件不存在。",
                        ["abilitykit.editor.sourceSync.message.InvalidSource"] = "无法读取源文件。"
                    }
                });
        }
    }
}
#endif
