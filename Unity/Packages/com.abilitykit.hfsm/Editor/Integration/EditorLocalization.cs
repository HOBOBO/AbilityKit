using System;
using System.Collections.Generic;
using AbilityKit.Editor.Platform.Core;
using AbilityKit.Editor.Platform.Localization;

namespace AbilityKit.HFSM.Editor
{
    internal static class EditorLocalization
    {
        internal const string ModuleId = "abilitykit.hfsm";

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
                        ["abilitykit.hfsm.module.name"] = "HFSM",
                        ["abilitykit.hfsm.panel.authoring"] = "HFSM 编辑",
                        ["abilitykit.hfsm.panel.runtime"] = "HFSM 运行时调试",
                        ["abilitykit.hfsm.panel.graph"] = "状态机图",
                        ["abilitykit.hfsm.panel.noGraph"] = "请选择要编辑的 HFSM 状态机图资源。",
                        ["abilitykit.hfsm.panel.open"] = "打开编辑器",
                        ["abilitykit.hfsm.panel.create"] = "新建状态机图",
                        ["abilitykit.hfsm.panel.debug"] = "打开运行时调试器",
                        ["abilitykit.hfsm.panel.runtimeStatus"] = "已注册运行时实例：{0}"
                    },
                    ["zh-CN"] = new Dictionary<string, string>
                    {
                        ["abilitykit.hfsm.module.name"] = "HFSM",
                        ["abilitykit.hfsm.panel.authoring"] = "HFSM 编辑",
                        ["abilitykit.hfsm.panel.runtime"] = "HFSM 运行时调试",
                        ["abilitykit.hfsm.panel.graph"] = "状态机图",
                        ["abilitykit.hfsm.panel.noGraph"] = "请选择要编辑的 HFSM 状态机图资源。",
                        ["abilitykit.hfsm.panel.open"] = "打开编辑器",
                        ["abilitykit.hfsm.panel.create"] = "新建状态机图",
                        ["abilitykit.hfsm.panel.debug"] = "打开运行时调试器",
                        ["abilitykit.hfsm.panel.runtimeStatus"] = "已注册运行时实例：{0}"
                    }
                });
        }
    }
}
