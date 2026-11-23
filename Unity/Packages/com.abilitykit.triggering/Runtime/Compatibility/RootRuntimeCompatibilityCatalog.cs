using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Validation;

namespace AbilityKit.Triggering.Runtime.Compatibility
{
    /// <summary>
    /// 只登记 Runtime 根目录的兼容占位脚本，不枚举位于子目录的保留 API。
    /// </summary>
    public static class RootRuntimeCompatibilityCatalog
    {
        /// <summary>
        /// 兼容占位入口下线时使用的默认门槛说明。
        /// </summary>
        public const string DefaultRemovalGate = "No package references, external samples migrated, Unity meta GUID no longer referenced, and a major compatibility cleanup batch is active.";

        private static readonly CompatibilityEntry[] _entries =
        {
        };

        /// <summary>
        /// 返回已登记的根目录占位入口；空集合表示当前无此类入口。
        /// </summary>
        public static IReadOnlyList<CompatibilityEntry> Entries => _entries;

        /// <summary>
        /// 按不区分大小写的登记名查找，未找到时返回默认条目。
        /// </summary>
        public static bool TryGetEntry(string entryPath, out CompatibilityEntry entry)
        {
            if (!string.IsNullOrEmpty(entryPath))
            {
                for (int i = 0; i < _entries.Length; i++)
                {
                    if (string.Equals(_entries[i].EntryPath, entryPath, StringComparison.OrdinalIgnoreCase))
                    {
                        entry = _entries[i];
                        return true;
                    }
                }
            }

            entry = default;
            return false;
        }

        /// <summary>
        /// 比较调用方提供的根目录脚本名和登记名，不读取文件系统。
        /// </summary>
        public static RuntimeCompatibilityScanResult ScanRootEntries(IEnumerable<string> rootRuntimeFileNames)
        {
            var issues = new List<RuntimeCompatibilityScanIssue>();
            var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (rootRuntimeFileNames != null)
            {
                foreach (var fileName in rootRuntimeFileNames)
                {
                    var normalized = NormalizeRootFileName(fileName);
                    if (!string.IsNullOrEmpty(normalized) && normalized.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    {
                        actual.Add(normalized);
                    }
                }
            }

            foreach (var fileName in actual)
            {
                if (!TryGetEntry(fileName, out _))
                {
                    issues.Add(new RuntimeCompatibilityScanIssue(
                        fileName,
                        ValidationErrorCodes.RUNTIME_COMPATIBILITY_ENTRY_MISSING,
                        $"Root Runtime compatibility entry is not registered: {fileName}"));
                }
            }

            for (int i = 0; i < _entries.Length; i++)
            {
                var entry = _entries[i];
                if (!actual.Contains(entry.EntryPath))
                {
                    issues.Add(new RuntimeCompatibilityScanIssue(
                        entry.EntryPath,
                        ValidationErrorCodes.RUNTIME_COMPATIBILITY_ENTRY_STALE,
                        $"Registered root Runtime compatibility entry is not present on disk: {entry.EntryPath}"));
                }
            }

            return new RuntimeCompatibilityScanResult(issues.ToArray());
        }

        private static string NormalizeRootFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            var normalized = fileName.Replace('\\', '/');
            var index = normalized.LastIndexOf('/');
            return index >= 0 ? normalized.Substring(index + 1) : normalized;
        }
    }
}
