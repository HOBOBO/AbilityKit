using System;
using AbilityKit.ExcelSync.Editor.Codecs;
using UnityEngine;

namespace AbilityKit.ExcelSync.Editor
{
    public static class ExcelSyncService
    {
        public static void Import(
            ScriptableObject targetAsset,
            string excelFilePath,
            ExcelTableOptions options,
            ITableReaderWriterFactory backend,
            ExcelCodecRegistry registry = null)
        {
            if (backend == null)
            {
                throw new ArgumentNullException(nameof(backend));
            }

            ScriptableObjectExcelSync.ImportToSingleAssetDataList(targetAsset, excelFilePath, options, backend, registry);
        }

        public static void Export(
            ScriptableObject targetAsset,
            string excelFilePath,
            ExcelTableOptions options,
            ITableReaderWriterFactory backend,
            ExcelCodecRegistry registry = null)
        {
            if (backend == null)
            {
                throw new ArgumentNullException(nameof(backend));
            }

            ScriptableObjectExcelSync.ExportFromSingleAssetDataList(targetAsset, excelFilePath, options, backend, registry);
        }

        public static void ValidateSchema(
            ScriptableObject targetAsset,
            string runtimeRowTypeName)
        {
            ScriptableObjectExcelSync.ValidateSchemaConsistency(targetAsset, runtimeRowTypeName);
        }

        public static string GenerateEditorPartialRawFromExcel(
            ScriptableObject targetAsset,
            string runtimeRowTypeName,
            string excelFilePath,
            ExcelTableOptions options,
            string outputFolderAssetsPath)
        {
            return ScriptableObjectExcelSync.GenerateEditorPartialRawFromExcel(targetAsset, runtimeRowTypeName, excelFilePath, options, outputFolderAssetsPath);
        }
    }
}
