using System;
using System.IO;
using System.Text;
using AbilityKit.Ability.Editor;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    /// <summary>
    /// 提供 P1/P2 示例同步的只读写入预检和确认，不改变已有批处理契约。
    /// </summary>
    internal static class MobaShowcaseMaintenance
    {
        private const string MenuRoot = "示例维护/";

        private static bool CanApply => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

        /// <summary>
        /// 登记交互预检入口；构建菜单时不读取或修改资产。
        /// </summary>
        internal static void AddMenuItems(GenericMenu menu)
        {
            AddMenuItem(menu, "预检 P1 技能经济同步", PreviewP1);
            AddMenuItem(menu, "预检 P2 技能编程同步", PreviewP2);
        }

        private static void AddMenuItem(GenericMenu menu, string label, GenericMenu.MenuFunction preview)
        {
            var content = new GUIContent(MenuRoot + label);
            if (CanApply) menu.AddItem(content, false, preview);
            else menu.AddDisabledItem(content);
        }

        private static void PreviewP1()
        {
            PreviewAndApply("P1 技能经济示例", (scope, blockers) =>
            {
                CheckSource(MobaP1SkillFlowShowcaseSync.SourceAssetPath, scope, blockers);
                CheckTarget<SkillFlowSO>(MobaP1SkillFlowShowcaseSync.TargetAssetPath, "覆盖技能流程数据", scope, blockers);
            }, MobaP1SkillFlowShowcaseSync.Sync);
        }

        private static void PreviewP2()
        {
            PreviewAndApply("P2 技能编程示例", (scope, blockers) =>
            {
                CheckSource(MobaP2SkillProgrammingShowcaseSync.TriggerSourceAssetPath, scope, blockers);
                CheckSource(MobaP2SkillProgrammingShowcaseSync.FlowSourceAssetPath, scope, blockers);
                var project = CheckTarget<TriggerAuthoringProjectAsset>(MobaP2SkillProgrammingShowcaseSync.ProjectAssetPath,
                    "保存项目并可能更新模块成员关系", scope, blockers, mustExist: true);
                var module = CheckTarget<TriggerAuthoringModuleAsset>(MobaP2SkillProgrammingShowcaseSync.TriggerModuleAssetPath,
                    "覆盖触发器模块数据", scope, blockers);
                if (module != null && module.Project != null && module.Project != project)
                    blockers.AppendLine("P2 模块属于其他项目；示例同步不会迁移所属项目。");
                CheckTarget<SkillFlowSO>(MobaP2SkillProgrammingShowcaseSync.FlowAssetPath, "覆盖技能流程数据", scope, blockers);
            }, MobaP2SkillProgrammingShowcaseSync.Sync);
        }

        private static void PreviewAndApply(string title, Action<StringBuilder, StringBuilder> inspect, Action sync)
        {
            if (!CanApply) return;

            try
            {
                var scope = new StringBuilder();
                var blockers = new StringBuilder();
                inspect(scope, blockers);
                if (blockers.Length > 0)
                {
                    EditorUtility.DisplayDialog(title + "：无法同步", blockers + "\n" + scope, "关闭");
                    return;
                }

                scope.AppendLine().AppendLine("仅预检路径、类型和写入方式，不比较字段或验证全部内容。")
                    .AppendLine("同步会保存其他脏资产。请先处理无关改动；操作不是事务，失败后请根据版本控制检查并恢复受影响资产。");
                if (!EditorUtility.DisplayDialog(title + "：确认写入", scope.ToString(), "确认同步", "取消") || !CanApply)
                    return;

                sync();
                EditorUtility.DisplayDialog(title, "同步完成。请核对资产差异后再提交。", "关闭");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(title + "：操作失败",
                    exception.Message + "\n如果已确认同步，请检查是否有部分写入。详情见 Console。", "关闭");
            }
        }

        private static void CheckSource(string path, StringBuilder scope, StringBuilder blockers)
        {
            scope.AppendLine("读取：" + path);
            if (!File.Exists(ToLocalPath(path))) blockers.AppendLine("缺少源文件：" + path);
        }

        private static T CheckTarget<T>(string path, string existingOperation, StringBuilder scope,
            StringBuilder blockers, bool mustExist = false) where T : UnityEngine.Object
        {
            var target = AssetDatabase.LoadMainAssetAtPath(path);
            var localPath = ToLocalPath(path);
            if (target == null && !File.Exists(localPath) && !Directory.Exists(localPath))
            {
                scope.AppendLine((mustExist ? "缺少项目：" : "新建：") + path);
                if (mustExist) blockers.AppendLine("缺少必需项目资产：" + path);
                else if (!AssetDatabase.IsValidFolder(Path.GetDirectoryName(path).Replace('\\', '/')))
                    blockers.AppendLine("目标目录未导入或不存在：" + path);
                return null;
            }

            scope.AppendLine(existingOperation + "：" + path);
            if (!(target is T typedTarget))
            {
                blockers.AppendLine("目标未导入或类型不符：" + path);
                return null;
            }

            if (EditorUtility.IsDirty(typedTarget)) blockers.AppendLine("目标有未保存修改：" + path);
            if (File.Exists(localPath) && (File.GetAttributes(localPath) & FileAttributes.ReadOnly) != 0)
                blockers.AppendLine("目标文件为只读：" + path);
            return typedTarget;
        }

        private static string ToLocalPath(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
    }
}
