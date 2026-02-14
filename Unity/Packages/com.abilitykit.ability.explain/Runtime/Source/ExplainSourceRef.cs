using System;

namespace AbilityKit.Ability.Explain
{
    [Serializable]
    public sealed class ExplainSourceRef
    {
        public string Kind;
        public string TableName;
        public string RowId;
        public string FieldPath;
        public string AssetGuid;
        public string FilePath;
        public int Line;

        public static ExplainSourceRef TableRow(string tableName, string rowId, string fieldPath = null)
        {
            return new ExplainSourceRef
            {
                Kind = "table_row",
                TableName = tableName,
                RowId = rowId,
                FieldPath = fieldPath
            };
        }

        public static ExplainSourceRef Asset(string assetGuid)
        {
            return new ExplainSourceRef
            {
                Kind = "asset",
                AssetGuid = assetGuid
            };
        }

        public static ExplainSourceRef File(string filePath, int line = 0)
        {
            return new ExplainSourceRef
            {
                Kind = "file",
                FilePath = filePath,
                Line = line
            };
        }
    }
}
