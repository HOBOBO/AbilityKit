using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.ExcelSync.Editor
{
    /// <summary>
    /// Excel 同步项目配置 - 单例资源，管理所有配置表模板
    /// </summary>
    [CreateAssetMenu(fileName = "ExcelSyncProjectConfig", menuName = "工具/Excel/Excel同步项目配置")]
    public sealed class ExcelSyncProjectConfig : SerializedScriptableObject
    {
        private static ExcelSyncProjectConfig _instance;

        /// <summary>
        /// 获取项目配置单例
        /// </summary>
        public static ExcelSyncProjectConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<ExcelSyncProjectConfig>("ExcelSyncProjectConfig");
#if UNITY_EDITOR
                    if (_instance == null)
                    {
                        // 尝试在默认路径查找
                        var guids = UnityEditor.AssetDatabase.FindAssets("t:ExcelSyncProjectConfig");
                        if (guids != null && guids.Length > 0)
                        {
                            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                            _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<ExcelSyncProjectConfig>(path);
                        }
                    }
#endif
                }
                return _instance;
            }
        }

        #region 全局配置

        [FoldoutGroup("全局配置")]
        [LabelText("Excel 根目录")]
        [FolderPath]
        [Tooltip("Excel 配置文件所在的根目录，相对于项目根目录或绝对路径")]
        public string ExcelRootPath = "../LubanConfig";

        [FoldoutGroup("全局配置")]
        [LabelText("代码输出目录")]
        [FolderPath]
        [Tooltip("生成的 C# 代码文件输出目录")]
        public string DefaultCodeOutputFolder = "Assets/Scripts/Editor/Excel/Generated";

        [FoldoutGroup("全局配置")]
        [LabelText("Asset 输出目录")]
        [FolderPath]
        [Tooltip("生成的 Table.asset 文件输出目录")]
        public string DefaultAssetOutputFolder = "Assets/Game/Configs/Excel";

        [FoldoutGroup("全局配置")]
        [LabelText("命名空间")]
        [Tooltip("生成的代码默认命名空间")]
        public string DefaultNamespace = "AbilityKit.ExcelSync.Generated";

        #endregion

        #region 默认表选项

        [FoldoutGroup("默认表选项")]
        [LabelText("表头行")]
        [Tooltip("Excel 中表头所在的行号（从1开始）")]
        public int DefaultHeaderRowIndex = 6;

        [FoldoutGroup("默认表选项")]
        [LabelText("数据起始行")]
        [Tooltip("Excel 中数据开始的行号（从1开始）")]
        public int DefaultDataStartRowIndex = 8;

        [FoldoutGroup("默认表选项")]
        [LabelText("主键列名")]
        [Tooltip("用于唯一标识每行数据的主键列名")]
        public string DefaultPrimaryKeyColumnName = "code";

        [FoldoutGroup("默认表选项")]
        [LabelText("Sheet 名称")]
        [Tooltip("Excel 中工作表的名称，为空则使用第一个 Sheet")]
        public string DefaultSheetName = "";

        #endregion

        #region 模板管理

        [FoldoutGroup("配置表模板")]
        [LabelText("配置表列表")]
        [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true, Expanded = true)]
        public List<ExcelSyncTableTemplate> TableTemplates = new List<ExcelSyncTableTemplate>();

        [ShowInInspector]
        [ReadOnly]
        [HideLabel]
        [InfoBox("暂无配置表，点击『扫描 Excel』自动添加，或点击『创建模板』手动添加", InfoMessageType.Info)]
        private bool HasNoTemplates => TableTemplates == null || TableTemplates.Count == 0;

        /// <summary>
        /// 获取启用的模板列表
        /// </summary>
        public IEnumerable<ExcelSyncTableTemplate> EnabledTemplates
        {
            get
            {
                foreach (var template in TableTemplates)
                {
                    if (template != null && template.Enabled)
                    {
                        yield return template;
                    }
                }
            }
        }

        /// <summary>
        /// 添加模板
        /// </summary>
        public void AddTemplate(ExcelSyncTableTemplate template)
        {
            if (template == null) return;
            if (!TableTemplates.Contains(template))
            {
                TableTemplates.Add(template);
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        /// <summary>
        /// 移除模板
        /// </summary>
        public void RemoveTemplate(ExcelSyncTableTemplate template)
        {
            if (template == null) return;
            if (TableTemplates.Remove(template))
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        /// <summary>
        /// 获取模板索引
        /// </summary>
        public int GetTemplateIndex(ExcelSyncTableTemplate template)
        {
            return TableTemplates?.IndexOf(template) ?? -1;
        }

        /// <summary>
        /// 根据 Excel 相对路径查找模板
        /// </summary>
        public ExcelSyncTableTemplate FindTemplateByExcelPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath) || TableTemplates == null) return null;

            foreach (var template in TableTemplates)
            {
                if (template != null && string.Equals(template.ExcelRelativePath, relativePath, StringComparison.OrdinalIgnoreCase))
                {
                    return template;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取模板的完整 Excel 路径
        /// </summary>
        public string GetExcelAbsolutePath(ExcelSyncTableTemplate template)
        {
            if (template == null || string.IsNullOrEmpty(template.ExcelRelativePath)) return null;

            if (System.IO.Path.IsPathRooted(template.ExcelRelativePath))
            {
                return template.ExcelRelativePath;
            }

            if (!string.IsNullOrEmpty(ExcelRootPath))
            {
                if (System.IO.Path.IsPathRooted(ExcelRootPath))
                {
                    return System.IO.Path.GetFullPath(System.IO.Path.Combine(ExcelRootPath, template.ExcelRelativePath));
                }
                else
                {
                    var projectRoot = System.IO.Path.GetFullPath(UnityEngine.Application.dataPath + "/..");
                    return System.IO.Path.GetFullPath(System.IO.Path.Combine(projectRoot, ExcelRootPath, template.ExcelRelativePath));
                }
            }

            return template.ExcelRelativePath;
        }

        /// <summary>
        /// 获取代码输出目录（优先使用模板自己的，否则使用全局）
        /// </summary>
        public string GetCodeOutputFolder(ExcelSyncTableTemplate template)
        {
            return string.IsNullOrEmpty(template?.CodeOutputPath) ? DefaultCodeOutputFolder : template.CodeOutputPath;
        }

        /// <summary>
        /// 获取 Asset 输出目录（优先使用模板自己的，否则使用全局）
        /// </summary>
        public string GetAssetOutputFolder(ExcelSyncTableTemplate template)
        {
            return string.IsNullOrEmpty(template?.AssetOutputPath) ? DefaultAssetOutputFolder : template.AssetOutputPath;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 创建默认的表选项
        /// </summary>
        public ExcelTableOptions CreateDefaultOptions()
        {
            return new ExcelTableOptions
            {
                SheetName = DefaultSheetName,
                HeaderRowIndex = DefaultHeaderRowIndex,
                DataStartRowIndex = DefaultDataStartRowIndex,
                PrimaryKeyColumnName = DefaultPrimaryKeyColumnName
            };
        }

        /// <summary>
        /// 获取 Excel 文件夹下的所有 xlsx 文件
        /// </summary>
        public string[] ScanExcelFiles()
        {
            if (string.IsNullOrEmpty(ExcelRootPath)) return Array.Empty<string>();

            string absRoot;
            if (System.IO.Path.IsPathRooted(ExcelRootPath))
            {
                absRoot = ExcelRootPath;
            }
            else
            {
                var projectRoot = System.IO.Path.GetFullPath(UnityEngine.Application.dataPath + "/..");
                absRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(projectRoot, ExcelRootPath));
            }

            if (!System.IO.Directory.Exists(absRoot))
            {
                UnityEngine.Debug.LogWarning($"[ExcelSync] Excel 根目录不存在: {absRoot}");
                return Array.Empty<string>();
            }

            return System.IO.Directory.GetFiles(absRoot, "*.xlsx", System.IO.SearchOption.AllDirectories);
        }

        /// <summary>
        /// 获取 Excel 的相对路径
        /// </summary>
        public string GetExcelRelativePath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath) || string.IsNullOrEmpty(ExcelRootPath)) return absolutePath;

            string absRoot;
            if (System.IO.Path.IsPathRooted(ExcelRootPath))
            {
                absRoot = ExcelRootPath;
            }
            else
            {
                var projectRoot = System.IO.Path.GetFullPath(UnityEngine.Application.dataPath + "/..");
                absRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(projectRoot, ExcelRootPath));
            }

            absRoot = absRoot.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
            var normalizedPath = System.IO.Path.GetFullPath(absolutePath);

            if (normalizedPath.StartsWith(absRoot, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedPath.Substring(absRoot.Length);
            }

            return absolutePath;
        }

        #endregion
    }
}
