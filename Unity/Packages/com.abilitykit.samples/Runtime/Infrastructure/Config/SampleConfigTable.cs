#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Samples.Logic.Infrastructure.Config
{
    /// <summary>
    /// 绀轰緥閰嶇疆琛ㄥ畾涔?
    /// </summary>
    public class ConfigTableDefinition
    {
        /// <summary>
        /// 閰嶇疆鏂囦欢璺緞锛堜笉鍚墿灞曞悕锛?
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// DTO 类型（原始数据类型）
        /// </summary>
        public Type DtoType { get; set; }

        /// <summary>
        /// 入口类型（运行时使用的数据对象类型）
        /// </summary>
        public Type EntryType { get; set; }

        public ConfigTableDefinition() { }

        public ConfigTableDefinition(string filePath, Type dtoType, Type entryType)
        {
            FilePath = filePath;
            DtoType = dtoType;
            EntryType = entryType;
        }
    }

    /// <summary>
    /// 閰嶇疆琛ㄦ帴鍙?
    /// </summary>
    public interface ISampleConfigTable<TEntry> where TEntry : class
    {
        int Count { get; }
        TEntry Get(int id);
        bool TryGet(int id, out TEntry entry);
        IEnumerable<TEntry> All();
    }

    /// <summary>
    /// 配置表注册器
    /// </summary>
    public class SampleConfigTableRegistry
    {
        private readonly Dictionary<string, ConfigTableDefinition> _byPath = new();
        private readonly List<ConfigTableDefinition> _tables = new();

        public IReadOnlyList<ConfigTableDefinition> Tables => _tables;

        public void Register(params ConfigTableDefinition[] tables)
        {
            foreach (var table in tables)
            {
                _byPath[table.FilePath] = table;
                _tables.Add(table);
            }
        }

        public ConfigTableDefinition GetTable(string filePath)
        {
            return _byPath.TryGetValue(filePath, out var definition) ? definition : null;
        }
    }

    /// <summary>
    /// 閰嶇疆琛ㄥ疄鐜?
    /// </summary>
    public class SampleConfigTable<TEntry> : ISampleConfigTable<TEntry> where TEntry : class
    {
        private readonly Dictionary<int, TEntry> _byId = new();
        private readonly Func<int, TEntry> _factory;

        public SampleConfigTable(Func<int, TEntry> factory = null)
        {
            _factory = factory;
        }

        public int Count => _byId.Count;

        public void Add(int id, TEntry entry)
        {
            _byId[id] = entry;
        }

        public TEntry Get(int id)
        {
            return _byId.TryGetValue(id, out var entry) ? entry : null;
        }

        public bool TryGet(int id, out TEntry entry)
        {
            return _byId.TryGetValue(id, out entry);
        }

        public IEnumerable<TEntry> All()
        {
            return _byId.Values;
        }
    }
}
