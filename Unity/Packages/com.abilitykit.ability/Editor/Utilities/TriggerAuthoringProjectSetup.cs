#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Config.Authoring;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class TriggerAuthoringProjectSetup
    {
        [MenuItem("Assets/AbilityKit/触发器编辑/创建 MOBA 项目配置")]
        private static void CreateMobaProjectSetup()
        {
            var projectPath = EditorUtility.SaveFilePanelInProject(
                "创建触发器项目",
                "MobaTriggerAuthoringProject",
                "asset",
                "请选择项目及其目录资产的创建位置。");
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

        internal static TriggerAuthoringTemplateAsset CreateTemplateForProject(
            TriggerAuthoringProjectAsset project)
        {
            if (project == null || project.TemplateCatalog == null)
            {
                EditorUtility.DisplayDialog("创建触发器模板", "所选项目尚未配置模板目录。", "确定");
                return null;
            }

            var projectPath = AssetDatabase.GetAssetPath(project);
            var directory = string.IsNullOrWhiteSpace(projectPath)
                ? "Assets"
                : Path.GetDirectoryName(projectPath)?.Replace('\\', '/') ?? "Assets";
            var path = EditorUtility.SaveFilePanelInProject(
                "创建触发器模板",
                "NewTriggerTemplate",
                "asset",
                "请选择模板资产的创建位置。",
                directory);
            if (string.IsNullOrWhiteSpace(path)) return null;

            return CreateTemplate(
                Path.GetDirectoryName(path)?.Replace('\\', '/') ?? directory,
                Path.GetFileNameWithoutExtension(path),
                project,
                path);
        }

        internal static TriggerAuthoringTemplateAsset CreateTemplate(
            string directory,
            string baseName,
            TriggerAuthoringProjectAsset project,
            string explicitPath = null)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (project.TemplateCatalog == null)
                throw new InvalidOperationException("项目必须先配置模板目录。");

            directory = string.IsNullOrWhiteSpace(directory) ? "Assets" : directory;
            baseName = string.IsNullOrWhiteSpace(baseName) ? "TriggerTemplate" : baseName;
            var asset = ScriptableObject.CreateInstance<TriggerAuthoringTemplateAsset>();
            asset.Template = new TriggerAuthoringTemplateData
            {
                TemplateId = CreateUniqueTemplateId(baseName, project.TemplateCatalog.Templates),
                TemplateVersion = "1.0.0",
                DisplayName = baseName,
                Parameters = new List<TriggerAuthoringTemplateParameterData>(),
                Definition = new TriggerDefinitionData
                {
                    Name = baseName,
                    Enabled = true,
                    EntryMode = TriggerEntryMode.Callable,
                    Event = string.Empty,
                    Actions = new TriggerNodeData
                    {
                        Kind = TriggerNodeKind.Action,
                        Type = "seq"
                    }
                }
            };
            var assetPath = string.IsNullOrWhiteSpace(explicitPath)
                ? AssetDatabase.GenerateUniqueAssetPath(directory + "/" + baseName + ".Template.asset")
                : explicitPath;
            AssetDatabase.CreateAsset(asset, assetPath);
            TriggerAuthoringTemplateMembership.Assign(asset, project);
            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(project.TemplateCatalog);
            AssetDatabase.SaveAssets();
            return asset;
        }

        internal static string CreateUniqueTemplateId(
            string baseName,
            IReadOnlyList<TriggerAuthoringTemplateAsset> templates)
        {
            var stem = "template_" + SanitizeModuleId(
                string.IsNullOrWhiteSpace(baseName) ? "trigger" : baseName);
            var candidate = stem;
            var suffix = 2;
            while (ContainsTemplateId(templates, candidate))
                candidate = stem + "_" + suffix++;
            return candidate;
        }

        private static bool ContainsTemplateId(
            IReadOnlyList<TriggerAuthoringTemplateAsset> templates,
            string templateId)
        {
            if (templates == null) return false;
            for (var i = 0; i < templates.Count; i++)
            {
                var template = templates[i]?.Template;
                if (template != null &&
                    string.Equals(template.TemplateId, templateId, StringComparison.Ordinal))
                    return true;
            }
            return false;
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
