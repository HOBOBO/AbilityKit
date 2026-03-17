using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.ExcelSync.Editor
{
    public sealed class ExcelSoSyncBaselineAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Row
        {
            public string Key;
            public List<string> Values = new List<string>();
        }

        [LabelText("Excel 路径")]
        public string ExcelAbsolutePath;

        [LabelText("工作表")]
        public string SheetName;

        [LabelText("表头行")]
        public int HeaderRowIndex;

        [LabelText("数据起始行")]
        public int DataStartRowIndex;

        [LabelText("主键")]
        public string PrimaryKeyColumnName;

        [LabelText("表头")]
        public List<string> Headers = new List<string>();

        [LabelText("行数据")]
        public List<Row> Rows = new List<Row>();

        public void Set(string excelAbsolutePath, ExcelTableOptions options, IReadOnlyList<string> headers, Dictionary<string, List<string>> rows)
        {
            ExcelAbsolutePath = excelAbsolutePath;
            SheetName = options.SheetName;
            HeaderRowIndex = options.HeaderRowIndex;
            DataStartRowIndex = options.DataStartRowIndex;
            PrimaryKeyColumnName = options.PrimaryKeyColumnName;

            Headers.Clear();
            if (headers != null)
            {
                Headers.AddRange(headers);
            }

            Rows.Clear();
            if (rows != null)
            {
                foreach (var kv in rows)
                {
                    var r = new Row();
                    r.Key = kv.Key;
                    if (kv.Value != null)
                    {
                        r.Values.AddRange(kv.Value);
                    }
                    Rows.Add(r);
                }
            }
        }

        public Dictionary<string, List<string>> BuildRowMap()
        {
            var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Rows.Count; i++)
            {
                var r = Rows[i];
                if (r == null || string.IsNullOrWhiteSpace(r.Key))
                {
                    continue;
                }
                if (!dict.ContainsKey(r.Key))
                {
                    dict.Add(r.Key, r.Values);
                }
            }
            return dict;
        }
    }
}
