using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using UnityEngine;

namespace AbilityKit.Game.Battle.Moba.Config.Sources
{
    /// <summary>
    /// Resources 配置源 - 从 Resources 目录加载
    /// </summary>
    public sealed class ResourcesConfigSource : IConfigSource
    {
        private readonly string _basePath;

        public ResourcesConfigSource(string basePath = null)
        {
            _basePath = basePath;
        }

        public bool TryGetText(string path, out string text)
        {
            text = null;
            var fullPath = GetFullPath(path);
            var asset = Resources.Load<TextAsset>(fullPath);
            if (asset == null) return false;
            text = asset.text;
            return !string.IsNullOrEmpty(text);
        }

        public bool TryGetBytes(string path, out byte[] bytes)
        {
            bytes = null;
            var fullPath = GetFullPath(path);
            var asset = Resources.Load<TextAsset>(fullPath);
            if (asset == null) return false;
            bytes = asset.bytes;
            return bytes != null && bytes.Length > 0;
        }

        private string GetFullPath(string path)
        {
            return string.IsNullOrEmpty(_basePath) ? path : $"{_basePath}/{path}";
        }
    }

    /// <summary>
    /// 字典配置源 - 从内存字典加载
    /// </summary>
    public sealed class DictionaryConfigSource : IConfigSource
    {
        private readonly IReadOnlyDictionary<string, string> _texts;
        private readonly IReadOnlyDictionary<string, byte[]> _bytes;

        public DictionaryConfigSource(IReadOnlyDictionary<string, string> texts = null, IReadOnlyDictionary<string, byte[]> bytes = null)
        {
            _texts = texts;
            _bytes = bytes;
        }

        public bool TryGetText(string path, out string text)
        {
            text = null;
            if (_texts != null && _texts.TryGetValue(path, out text))
                return !string.IsNullOrEmpty(text);
            return false;
        }

        public bool TryGetBytes(string path, out byte[] bytes)
        {
            bytes = null;
            if (_bytes != null && _bytes.TryGetValue(path, out bytes))
                return bytes != null && bytes.Length > 0;
            return false;
        }
    }

    /// <summary>
    /// 组合配置源 - 多个源按优先级尝试
    /// </summary>
    public sealed class CompositeConfigSource : IConfigSource
    {
        private readonly List<IConfigSource> _sources;
        private readonly List<IConfigTextSink> _textSinks;

        public CompositeConfigSource(params IConfigSource[] sources)
        {
            _sources = new List<IConfigSource>(sources ?? Array.Empty<IConfigSource>());
            _textSinks = new List<IConfigTextSink>();
        }

        public CompositeConfigSource AddSource(IConfigSource source)
        {
            if (source != null) _sources.Add(source);
            return this;
        }

        public CompositeConfigSource AddTextSink(IConfigTextSink sink)
        {
            if (sink != null) _textSinks.Add(sink);
            return this;
        }

        public bool TryGetText(string path, out string text)
        {
            text = null;

            foreach (var source in _sources)
            {
                if (source.TryGetText(path, out text) && !string.IsNullOrEmpty(text))
                    return true;
            }

            foreach (var sink in _textSinks)
            {
                if (sink.TryGetText(path, out text) && !string.IsNullOrEmpty(text))
                    return true;
            }

            return false;
        }

        public bool TryGetBytes(string path, out byte[] bytes)
        {
            bytes = null;

            foreach (var source in _sources)
            {
                if (source.TryGetBytes(path, out bytes) && bytes != null && bytes.Length > 0)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// 增量配置变更
    /// </summary>
    public sealed class ConfigChangeSet
    {
        public string TableName { get; }
        public byte[] Bytes { get; }
        public string Text { get; }
        public bool IsDeleted { get; }

        public ConfigChangeSet(string tableName, byte[] bytes)
        {
            TableName = tableName;
            Bytes = bytes;
            IsDeleted = bytes == null || bytes.Length == 0;
        }

        public ConfigChangeSet(string tableName, string text)
        {
            TableName = tableName;
            Text = text;
            IsDeleted = string.IsNullOrEmpty(text);
        }
    }

    /// <summary>
    /// 增量配置加载器接口
    /// </summary>
    public interface IIncrementalConfigLoader
    {
        /// <summary>
        /// 增量加载配置变更
        /// </summary>
        ConfigReloadResult ApplyChanges(IReadOnlyList<ConfigChangeSet> changes);
    }
}
