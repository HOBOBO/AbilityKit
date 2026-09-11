#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// 可复用于 Console、文件、UI、Unity、MonoGame 或自定义宿主的示例目录。
    /// </summary>
    public sealed class SampleCatalog
    {
        private readonly List<SampleCatalogEntry> _entries = new();

        /// <summary>
        /// 按宿主显示顺序排列的目录项。
        /// </summary>
        public IReadOnlyList<SampleCatalogEntry> Entries => _entries;

        /// <summary>
        /// 注册示例类型并创建对应目录项。
        /// </summary>
        public SampleCatalogEntry Register(
            Type sampleType,
            int priority = 100,
            string[]? tags = null,
            Func<ISample>? factory = null,
            string? id = null,
            string? title = null,
            string? description = null,
            SampleCategory? category = null,
            string? status = null,
            string? level = null,
            string[]? modules = null,
            string[]? next = null,
            SampleGuideContent? guide = null,
            SampleCodeWalkthroughStep[]? codeWalkthrough = null,
            SampleLearningContract? learningContract = null,
            SampleVisualFrame[]? visualFrames = null,
            SampleInputField[]? inputFields = null,
            SampleLearningCheckpoint[]? learningCheckpoints = null,
            string? visualTemplate = null,
            SampleVisualModel? visualModel = null,
            bool isManifestEntry = false)
        {
            if (sampleType == null)
                throw new ArgumentNullException(nameof(sampleType));
            if (!typeof(ISample).IsAssignableFrom(sampleType))
                throw new ArgumentException("Type must implement ISample.", nameof(sampleType));

            ISample Create()
            {
                if (factory != null)
                    return factory();

                return (ISample)(Activator.CreateInstance(sampleType)
                    ?? throw new InvalidOperationException($"Cannot create sample: {sampleType.FullName}"));
            }

            var preview = Create();
            var resolvedCategory = category ?? preview.Category;
            var resolvedTitle = title ?? preview.Title;
            var entry = new SampleCatalogEntry(
                _entries.Count,
                string.IsNullOrWhiteSpace(id) ? CreateStableId(resolvedCategory, resolvedTitle) : id,
                resolvedTitle,
                description ?? preview.Description,
                resolvedCategory,
                sampleType,
                Create,
                priority,
                tags,
                status,
                level,
                modules,
                next,
                guide,
                codeWalkthrough,
                learningContract,
                visualFrames,
                inputFields,
                learningCheckpoints,
                visualTemplate,
                visualModel,
                isManifestEntry);

            _entries.Add(entry);
            return entry;
        }

        /// <summary>
        /// 按显示索引查找目录项。
        /// </summary>
        public bool TryGetByIndex(int index, out SampleCatalogEntry entry)
        {
            if (index >= 0 && index < _entries.Count)
            {
                entry = _entries[index];
                return true;
            }

            entry = null!;
            return false;
        }

        /// <summary>
        /// 按稳定 ID 查找目录项。
        /// </summary>
        public bool TryGetById(string id, out SampleCatalogEntry entry)
        {
            entry = _entries.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase))!;
            return entry != null;
        }

        /// <summary>
        /// 为菜单、标签页或树形视图分组目录项。
        /// </summary>
        public IReadOnlyDictionary<SampleCategory, IReadOnlyList<SampleCatalogEntry>> GroupByCategory()
        {
            return _entries
                .GroupBy(x => x.Category)
                .OrderBy(x => (int)x.Key)
                .ToDictionary(
                    x => x.Key,
                    x => (IReadOnlyList<SampleCatalogEntry>)x.OrderBy(e => e.Index).ToList());
        }

        /// <summary>
        /// 根据分类和标题创建稳定 ID。
        /// </summary>
        public static string CreateStableId(SampleCategory category, string title)
        {
            var builder = new StringBuilder();
            builder.Append(category.GetDisplayName().ToLowerInvariant());
            builder.Append('/');

            foreach (var ch in title ?? string.Empty)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(char.ToLowerInvariant(ch));
                }
                else if (ch == '.' || ch == '-' || ch == '_' || char.IsWhiteSpace(ch))
                {
                    if (builder[^1] != '-')
                        builder.Append('-');
                }
            }

            return builder.ToString().TrimEnd('-');
        }
    }
}
