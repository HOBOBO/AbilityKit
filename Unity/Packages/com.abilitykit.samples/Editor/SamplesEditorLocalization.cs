#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Editor.Platform.Core;
using AbilityKit.Editor.Platform.Localization;

namespace AbilityKit.Samples.Editor
{
    /// <summary>
    /// 示例模块的本地化文案。与其它编辑器模块一致：注册一个字典源，键由 Hub 统一解析。
    /// </summary>
    internal static class SamplesEditorLocalization
    {
        internal const string ModuleId = "abilitykit.samples";

        internal static IEditorLocalization Localization => AbilityKitEditorPlatform.Localization;

        internal static IDisposable RegisterSource()
        {
            return AbilityKitEditorPlatform.Localization.RegisterSource(CreateSource());
        }

        private static IEditorLocalizationSource CreateSource()
        {
            return new DictionaryEditorLocalizationSource(
                ModuleId,
                new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    ["en"] = new Dictionary<string, string>
                    {
                        ["abilitykit.samples.module.name"] = "Samples",
                        ["abilitykit.samples.panel.browse"] = "Sample Catalog",
                        ["abilitykit.samples.panel.hint"] =
                            "Browse the graded sample catalog, run a sample and read its structured output.",
                        ["abilitykit.samples.panel.open"] = "Open sample catalog",
                    },
                    ["zh-CN"] = new Dictionary<string, string>
                    {
                        ["abilitykit.samples.module.name"] = "示例目录",
                        ["abilitykit.samples.panel.browse"] = "示例目录",
                        ["abilitykit.samples.panel.hint"] =
                            "浏览分级示例目录，运行单条示例并查看结构化输出。",
                        ["abilitykit.samples.panel.open"] = "打开示例目录",
                    },
                });
        }
    }
}
