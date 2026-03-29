using System;
using System.IO;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.ExcelSync.Editor
{
    /// <summary>
    /// 单个配置表的同步模板
    /// </summary>
    [Serializable]
    public class ExcelSyncTableTemplate
    {
        #region 基本信息

        [FoldoutGroup("基本信息")]
        [LabelText("启用")]
        [Tooltip("是否启用此模板的同步功能")]
        public bool Enabled = true;

        [FoldoutGroup("基本信息")]
        [LabelText("配置名称")]
        [Tooltip("配置的显示名称，用于标识，留空则自动从 Excel 路径推断")]
        public string DisplayName = "";

        [FoldoutGroup("基本信息")]
        [LabelText("描述")]
        [MultiLineProperty(2)]
        public string Description = "";

        #endregion

        #region Excel 配置

        [FoldoutGroup("Excel 配置")]
        [LabelText("Excel 相对路径")]
        [Tooltip("相对于 Excel 根目录的路径，如 Moba/Characters.xlsx")]
        public string ExcelRelativePath = "";

        [FoldoutGroup("Excel 配置")]
        [LabelText("Sheet 名称")]
        [Tooltip("Excel 中工作表的名称，为空则使用第一个 Sheet")]
        public string SheetName = "";

        [FoldoutGroup("Excel 配置")]
        [LabelText("表头行")]
        [Tooltip("Excel 中表头所在的行号（从1开始）")]
        [Range(1, 100)]
        public int HeaderRowIndex = 6;

        [FoldoutGroup("Excel 配置")]
        [LabelText("数据起始行")]
        [Tooltip("Excel 中数据开始的行号（从1开始）")]
        [Range(1, 100)]
        public int DataStartRowIndex = 8;

        [FoldoutGroup("Excel 配置")]
        [LabelText("主键列名")]
        [Tooltip("用于唯一标识每行数据的主键列名")]
        public string PrimaryKeyColumnName = "code";

        #endregion

        #region 代码生成

        [FoldoutGroup("代码生成")]
        [LabelText("自定义代码目录")]
        [FolderPath]
        [Tooltip("留空则使用全局配置目录")]
        public string CodeOutputPath = "";

        [FoldoutGroup("代码生成")]
        [LabelText("命名空间")]
        [Tooltip("留空则使用全局命名空间")]
        public string Namespace = "";

        #endregion

        #region Asset 输出

        [FoldoutGroup("Asset 输出")]
        [LabelText("自定义 Asset 目录")]
        [FolderPath]
        [Tooltip("留空则使用全局配置目录")]
        public string AssetOutputPath = "";

        #endregion

        #region 运行时类型（可选）

        [FoldoutGroup("运行时类型")]
        [LabelText("运行时行类型")]
        [Tooltip("完整的运行时类型名称，用于 Schema 校验，如 HotUpdate.Config.Characters")]
        public string RuntimeRowTypeName = "";

        [FoldoutGroup("运行时类型")]
        [LabelText("启用 Schema 校验")]
        [Tooltip("是否校验编辑器数据类与运行时类型的 Schema 一致性")]
        public bool ValidateSchema = true;

        #endregion

        #region 绑定状态（只读）

        [FoldoutGroup("绑定状态")]
        [LabelText("Table Asset")]
        [ReadOnly]
        [ShowInInspector]
        public string TableAssetPath => _tableAsset != null ? UnityEditor.AssetDatabase.GetAssetPath(_tableAsset) : "(未绑定)";

        [FoldoutGroup("绑定状态")]
        [LabelText("基线状态")]
        [ReadOnly]
        [ShowInInspector]
        public BaselineStatus BaselineStatus => CheckBaselineStatus();

        [HideInInspector]
        [SerializeField]
        private ScriptableObject _tableAsset;

        #endregion

        /// <summary>
        /// 获取绑定的 Table Asset
        /// </summary>
        public ScriptableObject TableAsset
        {
            get => _tableAsset;
            set
            {
                if (_tableAsset != value)
                {
                    _tableAsset = value;
                }
            }
        }

        /// <summary>
        /// 获取显示名称（如果没有设置则从 Excel 路径推断）
        /// </summary>
        public string GetDisplayName()
        {
            if (!string.IsNullOrEmpty(DisplayName))
            {
                return DisplayName;
            }

            if (!string.IsNullOrEmpty(ExcelRelativePath))
            {
                var name = Path.GetFileNameWithoutExtension(ExcelRelativePath);
                if (!string.IsNullOrEmpty(SheetName) && SheetName != "Sheet1")
                {
                    name += "_" + SheetName;
                }
                return name;
            }

            return "未命名配置";
        }

        /// <summary>
        /// 获取表格选项
        /// </summary>
        public ExcelTableOptions CreateOptions()
        {
            return new ExcelTableOptions
            {
                SheetName = SheetName,
                HeaderRowIndex = HeaderRowIndex,
                DataStartRowIndex = DataStartRowIndex,
                PrimaryKeyColumnName = PrimaryKeyColumnName
            };
        }

        /// <summary>
        /// 检查基线状态
        /// </summary>
        private BaselineStatus CheckBaselineStatus()
        {
            if (_tableAsset == null)
            {
                return BaselineStatus.NotBound;
            }

            var assetPath = UnityEditor.AssetDatabase.GetAssetPath(_tableAsset);
            if (string.IsNullOrEmpty(assetPath))
            {
                return BaselineStatus.NotBound;
            }

            var baselinePath = assetPath + ".excelBaseline.asset";
            if (!File.Exists(baselinePath))
            {
                return BaselineStatus.Missing;
            }

            return BaselineStatus.Exists;
        }

        /// <summary>
        /// 获取生成的行类型名称
        /// </summary>
        public string GetRowTypeName()
        {
            if (!string.IsNullOrEmpty(ExcelRelativePath))
            {
                var name = Path.GetFileNameWithoutExtension(ExcelRelativePath);
                var sheetName = !string.IsNullOrEmpty(SheetName) ? SheetName : "Sheet1";
                if (!sheetName.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return sheetName + "Row";
                }
                return name + "Row";
            }
            return "Row";
        }

        /// <summary>
        /// 获取生成的表类型名称
        /// </summary>
        public string GetTableTypeName()
        {
            if (!string.IsNullOrEmpty(ExcelRelativePath))
            {
                var name = Path.GetFileNameWithoutExtension(ExcelRelativePath);
                var sheetName = !string.IsNullOrEmpty(SheetName) ? SheetName : "Sheet1";
                if (!sheetName.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return sheetName + "Table";
                }
                return name + "Table";
            }
            return "Table";
        }
    }

    /// <summary>
    /// 基线状态
    /// </summary>
    public enum BaselineStatus
    {
        NotBound,    // 未绑定 Table Asset
        Missing,     // 已绑定但缺少基线
        Exists       // 基线存在
    }
}
