using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Explain
{
    [Serializable]
    public sealed class NavigationTarget
    {
        public string Kind;
        public string EditorId;

        public string TableName;
        public string RowId;
        public string FieldPath;

        public string AssetGuid;
        public string FilePath;
        public int Line;

        public Dictionary<string, string> Extra;

        public static NavigationTarget OpenTableRow(string tableName, string rowId, string fieldPath = null)
        {
            return new NavigationTarget
            {
                Kind = "open_table_row",
                TableName = tableName,
                RowId = rowId,
                FieldPath = fieldPath
            };
        }

        public static NavigationTarget OpenAsset(string assetGuid)
        {
            return new NavigationTarget
            {
                Kind = "open_asset",
                AssetGuid = assetGuid
            };
        }

        public static NavigationTarget OpenFile(string filePath, int line = 0)
        {
            return new NavigationTarget
            {
                Kind = "open_file",
                FilePath = filePath,
                Line = line
            };
        }

        public static NavigationTarget OpenEditor(string editorId, Dictionary<string, string> extra = null)
        {
            return new NavigationTarget
            {
                Kind = "open_editor",
                EditorId = editorId,
                Extra = extra
            };
        }
    }
}
