using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.ExcelSync.Editor
{
    /// <summary>
    /// Excel 同步批量操作菜单
    /// </summary>
    public static class ExcelSyncBatchMenu
    {
        [MenuItem("Tools/Excel/批量操作/扫描 Excel 并创建模板")]
        public static void ScanAndCreateTemplates()
        {
            var config = ExcelSyncProjectConfig.Instance;
            if (config == null)
            {
                EditorUtility.DisplayDialog("错误", "未找到 ExcelSyncProjectConfig，请先创建配置资源", "确定");
                return;
            }

            var result = ExcelSyncTemplateService.ScanAndCreateTemplates(config);

            var message = $"扫描完成:\n";
            if (result.HasErrors)
            {
                message += $"  - 扫描过程中有错误，请检查 Console\n";
            }
            if (result.CreatedCount > 0)
            {
                message += $"  + 新建模板: {result.CreatedCount}\n";
            }
            if (result.ExistingCount > 0)
            {
                message += $"  - 已存在: {result.ExistingCount}\n";
            }

            EditorUtility.DisplayDialog(result.HasErrors ? "扫描完成（有错误）" : "扫描完成", message, "确定");
        }

        [MenuItem("Tools/Excel/批量操作/生成所有代码")]
        public static void GenerateAllCode()
        {
            var config = ExcelSyncProjectConfig.Instance;
            if (config == null)
            {
                EditorUtility.DisplayDialog("错误", "未找到 ExcelSyncProjectConfig", "确定");
                return;
            }

            var result = ExcelSyncTemplateService.GenerateAllCode(config);

            ShowBatchResult(result, "代码生成");
        }

        [MenuItem("Tools/Excel/批量操作/导入所有配置")]
        public static void ImportAll()
        {
            var config = ExcelSyncProjectConfig.Instance;
            if (config == null)
            {
                EditorUtility.DisplayDialog("错误", "未找到 ExcelSyncProjectConfig", "确定");
                return;
            }

            // 确认操作
            var enabledCount = config.EnabledTemplates.Count();
            if (enabledCount == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有启用的配置表", "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog("确认导入",
                $"即将导入 {enabledCount} 个配置表。\n\n是否继续？",
                "继续", "取消"))
            {
                return;
            }

            var result = ExcelSyncTemplateService.ImportAll(config);

            ShowBatchResult(result, "导入");
        }

        [MenuItem("Tools/Excel/批量操作/导出所有配置")]
        public static void ExportAll()
        {
            var config = ExcelSyncProjectConfig.Instance;
            if (config == null)
            {
                EditorUtility.DisplayDialog("错误", "未找到 ExcelSyncProjectConfig", "确定");
                return;
            }

            var enabledCount = config.EnabledTemplates.Count();
            if (enabledCount == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有启用的配置表", "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog("确认导出",
                $"即将导出 {enabledCount} 个配置表。\n\n注意：缺少基线的配置将跳过导入步骤。",
                "继续", "取消"))
            {
                return;
            }

            var result = ExcelSyncTemplateService.ExportAll(config);

            ShowBatchResult(result, "导出");
        }

        [MenuItem("Tools/Excel/批量操作/一键生成并导入所有")]
        public static void OneClickGenerateAndImportAll()
        {
            var config = ExcelSyncProjectConfig.Instance;
            if (config == null)
            {
                EditorUtility.DisplayDialog("错误", "未找到 ExcelSyncProjectConfig", "确定");
                return;
            }

            var enabledCount = config.EnabledTemplates.Count();
            if (enabledCount == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有启用的配置表", "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog("确认一键同步",
                $"即将对 {enabledCount} 个配置表执行一键同步：\n\n" +
                "1. 生成代码\n" +
                "2. 创建/更新 Table Asset\n" +
                "3. 导入数据\n\n" +
                "是否继续？",
                "继续", "取消"))
            {
                return;
            }

            // 显示进度
            EditorUtility.DisplayProgressBar("Excel 同步", "正在同步配置表...", 0f);

            var result = ExcelSyncTemplateService.GenerateAndImportAll(config);

            EditorUtility.ClearProgressBar();

            ShowBatchResult(result, "一键同步");
        }

        [MenuItem("Tools/Excel/批量操作/创建 Table Asset 并绑定")]
        public static void CreateAndBindAllAssets()
        {
            var config = ExcelSyncProjectConfig.Instance;
            if (config == null)
            {
                EditorUtility.DisplayDialog("错误", "未找到 ExcelSyncProjectConfig", "确定");
                return;
            }

            var successCount = 0;
            var errorCount = 0;
            var skipCount = 0;

            foreach (var template in config.EnabledTemplates)
            {
                if (template.TableAsset != null)
                {
                    skipCount++;
                    continue;
                }

                var result = ExcelSyncTemplateService.CreateOrBindTableAsset(template, config);
                if (result.IsSuccess)
                {
                    successCount++;
                }
                else
                {
                    errorCount++;
                    Debug.LogError($"[ExcelSync] Asset 创建失败: {template.GetDisplayName()}: {result.Message}");
                }
            }

            var message = $"Asset 创建完成:\n";
            message += $"  + 成功: {successCount}\n";
            message += $"  - 失败: {errorCount}\n";
            message += $"  · 跳过: {skipCount}";

            EditorUtility.DisplayDialog("创建完成", message, "确定");
        }

        [MenuItem("Tools/Excel/批量操作/刷新所有基线状态")]
        public static void RefreshAllBaselineStatus()
        {
            var config = ExcelSyncProjectConfig.Instance;
            if (config == null)
            {
                return;
            }

            var count = config.TableTemplates.Count;

            // 标记配置为 dirty 以触发重新计算
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Debug.Log($"[ExcelSync] 已刷新 {count} 个模板的基线状态");
        }

        [MenuItem("Tools/Excel/批量操作/启用所有配置")]
        public static void EnableAll()
        {
            var config = ExcelSyncProjectConfig.Instance;
            if (config == null) return;

            foreach (var template in config.TableTemplates)
            {
                if (template == null) continue;
                template.Enabled = true;
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Debug.Log($"[ExcelSync] 已启用所有 {config.TableTemplates.Count} 个配置表");
        }

        [MenuItem("Tools/Excel/批量操作/禁用所有配置")]
        public static void DisableAll()
        {
            var config = ExcelSyncProjectConfig.Instance;
            if (config == null) return;

            foreach (var template in config.TableTemplates)
            {
                if (template == null) continue;
                template.Enabled = false;
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Debug.Log($"[ExcelSync] 已禁用所有 {config.TableTemplates.Count} 个配置表");
        }

        [MenuItem("Tools/Excel/批量操作/打开模板管理器")]
        public static void OpenTemplateManager()
        {
            ExcelSyncTemplateManagerWindow.Open();
        }

        private static void ShowBatchResult(BatchResult result, string operation)
        {
            if (result.TotalCount == 0)
            {
                EditorUtility.DisplayDialog(operation, "没有需要操作的配置表", "确定");
                return;
            }

            var message = $"{operation}完成:\n\n";
            message += $"总计: {result.TotalCount}\n";
            message += $"成功: {result.SuccessCount}\n";
            message += $"失败: {result.ErrorCount}\n\n";

            if (result.HasErrors)
            {
                message += "失败项:\n";
                foreach (var error in result.Errors.Take(5))
                {
                    message += $"  - {error.Name}: {error.Message}\n";
                }
                if (result.ErrorCount > 5)
                {
                    message += $"  ... 还有 {result.ErrorCount - 5} 项\n";
                }
            }

            if (result.AllSuccess)
            {
                EditorUtility.DisplayDialog(operation + "成功", message, "确定");
            }
            else
            {
                EditorUtility.DisplayDialog(operation + "完成", message, "确定");
            }
        }
    }
}
