using System;
using System.Collections.Generic;

namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 标签模板注册表，用于管理所有标签模板。
    /// </summary>
    public sealed class TagTemplateRegistry
    {
        /// <summary>
        /// 单例实例
        /// </summary>
        public static TagTemplateRegistry Instance { get; } = new TagTemplateRegistry();

        private readonly Dictionary<int, TemplateEntry> _templatesById = new Dictionary<int, TemplateEntry>();
        private readonly Dictionary<string, int> _templatesByName = new Dictionary<string, int>(StringComparer.Ordinal);
        private int _nextId = 1;

        private TagTemplateRegistry()
        {
        }

        /// <summary>
        /// 注册模板
        /// </summary>
        public int Register(string name, GameplayTagTemplate template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));

            if (_templatesByName.TryGetValue(name, out var existingId))
            {
                _templatesById[existingId] = new TemplateEntry(existingId, name, template);
                return existingId;
            }

            var id = _nextId++;
            _templatesById[id] = new TemplateEntry(id, name, template);
            _templatesByName[name] = id;
            return id;
        }

        /// <summary>
        /// 尝试通过 ID 获取模板
        /// </summary>
        public bool TryGet(int id, out GameplayTagTemplate template)
        {
            template = null;
            if (_templatesById.TryGetValue(id, out var entry))
            {
                template = entry.Template;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 尝试通过名称获取模板
        /// </summary>
        public bool TryGet(string name, out GameplayTagTemplate template)
        {
            template = null;
            if (_templatesByName.TryGetValue(name, out var id))
            {
                return TryGet(id, out template);
            }
            return false;
        }

        /// <summary>
        /// 获取模板数量
        /// </summary>
        public int Count => _templatesById.Count;

        /// <summary>
        /// 获取所有已注册的模板 ID
        /// </summary>
        public IEnumerable<int> GetAllIds()
        {
            return _templatesById.Keys;
        }

        /// <summary>
        /// 清空所有注册的模板
        /// </summary>
        public void Clear()
        {
            _templatesById.Clear();
            _templatesByName.Clear();
            _nextId = 1;
        }

        private readonly struct TemplateEntry
        {
            public int Id { get; }
            public string Name { get; }
            public GameplayTagTemplate Template { get; }

            public TemplateEntry(int id, string name, GameplayTagTemplate template)
            {
                Id = id;
                Name = name;
                Template = template;
            }
        }
    }
}
