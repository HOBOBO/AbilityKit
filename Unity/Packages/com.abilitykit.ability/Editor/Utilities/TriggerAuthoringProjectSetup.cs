#if UNITY_EDITOR
using System.IO;
using AbilityKit.Ability.Config.Authoring;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class TriggerAuthoringProjectSetup
    {
        [MenuItem("Assets/AbilityKit/Trigger Authoring/Create MOBA Project Setup")]
        private static void CreateMobaProjectSetup()
        {
            var projectPath = EditorUtility.SaveFilePanelInProject(
                "Create Trigger Authoring Project",
                "MobaTriggerAuthoringProject",
                "asset",
                "Choose where to create the project and its catalogs.");
            if (string.IsNullOrWhiteSpace(projectPath)) return;

            var directory = Path.GetDirectoryName(projectPath)?.Replace('\\', '/') ?? "Assets";
            var baseName = Path.GetFileNameWithoutExtension(projectPath);
            var project = CreateProjectWithCatalogs(directory, baseName);
            if (project == null) return;

            var module = CreateStarterModule(directory, baseName, project);
            var focus = module != null ? module : (UnityEngine.Object)project;
            Selection.activeObject = focus;
            EditorGUIUtility.PingObject(focus);
        }

        /// <summary>创建带 MOBA 默认目录（Event/Global Blackboard/Template）的项目资产。</summary>
        internal static TriggerAuthoringProjectAsset CreateProjectWithCatalogs(string directory, string baseName)
        {
            directory = string.IsNullOrWhiteSpace(directory) ? "Assets" : directory;
            baseName = string.IsNullOrWhiteSpace(baseName) ? "TriggerAuthoringProject" : baseName;

            var eventPath = AssetDatabase.GenerateUniqueAssetPath(directory + "/" + baseName + ".Events.asset");
            var blackboardPath = AssetDatabase.GenerateUniqueAssetPath(directory + "/" + baseName + ".Blackboard.asset");
            var templatePath = AssetDatabase.GenerateUniqueAssetPath(directory + "/" + baseName + ".Templates.asset");

            var eventCatalog = ScriptableObject.CreateInstance<TriggerEventCatalogAsset>();
            eventCatalog.Events = TriggerAuthoringProjectDefaults.CreateMobaEvents();
            AssetDatabase.CreateAsset(eventCatalog, eventPath);

            var blackboardCatalog = ScriptableObject.CreateInstance<TriggerGlobalBlackboardCatalogAsset>();
            blackboardCatalog.Keys = TriggerAuthoringProjectDefaults.CreateMobaBlackboardKeys();
            AssetDatabase.CreateAsset(blackboardCatalog, blackboardPath);

            var templateCatalog = ScriptableObject.CreateInstance<TriggerAuthoringTemplateCatalogAsset>();
            AssetDatabase.CreateAsset(templateCatalog, templatePath);

            var project = ScriptableObject.CreateInstance<TriggerAuthoringProjectAsset>();
            project.SetCatalogs(eventCatalog, blackboardCatalog, templateCatalog);
            project.SetRuntimeOutputRoot("Packages/com.abilitykit.demo.moba.view.runtime/Resources/ability/triggers");
            AssetDatabase.CreateAsset(
                project,
                AssetDatabase.GenerateUniqueAssetPath(directory + "/" + baseName + ".asset"));
            AssetDatabase.SaveAssets();
            return project;
        }

        /// <summary>创建一个空模块资产并双向登记到项目（模块清单是构建门禁的输入）。</summary>
        internal static TriggerAuthoringModuleAsset CreateStarterModule(
            string directory,
            string baseName,
            TriggerAuthoringProjectAsset project)
        {
            directory = string.IsNullOrWhiteSpace(directory) ? "Assets" : directory;
            baseName = string.IsNullOrWhiteSpace(baseName) ? "Module" : baseName;

            var module = ScriptableObject.CreateInstance<TriggerAuthoringModuleAsset>();
            module.Module = new TriggerAuthoringModuleData
            {
                ModuleId = "module_" + SanitizeModuleId(baseName),
                DisplayName = baseName,
                Kind = TriggerModuleKind.Ability
            };
            AssetDatabase.CreateAsset(
                module,
                AssetDatabase.GenerateUniqueAssetPath(directory + "/" + baseName + ".Module.asset"));
            if (project != null) TriggerAuthoringProjectMembership.Assign(module, project);
            EditorUtility.SetDirty(module);
            AssetDatabase.SaveAssets();
            return module;
        }

        private static string SanitizeModuleId(string value)
        {
            var builder = new System.Text.StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var c = char.ToLowerInvariant(value[i]);
                if (char.IsLetterOrDigit(c) || c == '_') builder.Append(c);
                else if (builder.Length > 0 && builder[builder.Length - 1] != '_') builder.Append('_');
            }
            return builder.ToString();
        }
    }
}
#endif
