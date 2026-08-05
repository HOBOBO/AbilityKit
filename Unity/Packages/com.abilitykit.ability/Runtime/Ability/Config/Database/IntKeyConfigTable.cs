using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Config
{
    internal interface IConfigTableContentReplacer
    {
        void ReplaceContentsFrom(object source);
    }

    /// <summary>
    /// int 主键的配置表实现
    /// </summary>
    /// <typeparam name="TEntry">配置条目类型</typeparam>
    public sealed class IntKeyConfigTable<TEntry> : IConfigTable<TEntry>, IConfigTableContentReplacer
        where TEntry : class
    {
        private readonly Dictionary<int, TEntry> _byId = new Dictionary<int, TEntry>();

        public int Count => _byId.Count;

        /// <summary>
        /// 添加配置条目
        /// </summary>
        public void Add(int id, TEntry entry)
        {
            if (entry == null) return;
            _byId[id] = entry;
        }

        /// <summary>
        /// 从 DTO 创建并添加配置条目
        /// </summary>
        /// <param name="dto">DTO 对象</param>
        /// <param name="entryFactory">DTO 到 Entry 的转换工厂</param>
        public void AddFromDto(object dto, Func<object, TEntry> entryFactory)
        {
            if (dto == null) return;
            var id = ReadId(dto);
            var entry = entryFactory(dto);
            _byId[id] = entry;
        }

        /// <summary>
        /// 添加多个 DTO
        /// </summary>
        public void AddRangeFromDtos(IEnumerable<object> dtos, Func<object, TEntry> entryFactory)
        {
            if (dtos == null) return;
            foreach (var dto in dtos)
            {
                AddFromDto(dto, entryFactory);
            }
        }

        /// <summary>
        /// 清空所有配置
        /// </summary>
        public void Clear()
        {
            _byId.Clear();
        }

        internal void ReplaceWith(IntKeyConfigTable<TEntry> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _byId.Clear();
            foreach (var pair in source._byId)
            {
                _byId[pair.Key] = pair.Value;
            }
        }

        void IConfigTableContentReplacer.ReplaceContentsFrom(object source)
        {
            if (!(source is IntKeyConfigTable<TEntry> typedSource))
                throw new InvalidOperationException($"Unexpected replacement table type for {typeof(TEntry).FullName}.");
            ReplaceWith(typedSource);
        }

        public TEntry Get(int id)
        {
            return _byId.TryGetValue(id, out var entry) 
                ? entry 
                : throw new KeyNotFoundException($"Config not found: type={typeof(TEntry).Name} id={id}");
        }

        public bool TryGet(int id, out TEntry entry)
        {
            return _byId.TryGetValue(id, out entry);
        }

        public IEnumerable<TEntry> All()
        {
            return _byId.Values;
        }

        private static int ReadId(object dto)
        {
            var type = dto.GetType();
            var field = type.GetField("Id");
            if (field != null && field.FieldType == typeof(int)) return (int)field.GetValue(dto);
            var property = type.GetProperty("Id");
            if (property != null && property.PropertyType == typeof(int)) return (int)property.GetValue(dto);

            // 回退：尝试读取 Code 字段（例如 Luban DRCharacters 等 DR* 类型会使用该字段）。
            field = type.GetField("Code");
            if (field != null && field.FieldType == typeof(int)) return (int)field.GetValue(dto);
            property = type.GetProperty("Code");
            if (property != null && property.PropertyType == typeof(int)) return (int)property.GetValue(dto);

            throw new InvalidOperationException($"DTO must have int Id or Code field/property. type={type.FullName}");
        }
    }

    /// <summary>
    /// Reflection-free builders used by generated config table manifests.
    /// </summary>
    public static class ConfigTableFactory
    {
        /// <summary>
        /// Builds a strongly typed DTO table without reflection.
        /// </summary>
        public static object CreateDtoTable<TDto>(Array dtos, Func<TDto, int> keySelector)
            where TDto : class
        {
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            var table = new ConfigDatabase.DtoTable<TDto>(keySelector);
            if (dtos == null) return table;

            for (var i = 0; i < dtos.Length; i++)
            {
                if (dtos.GetValue(i) is TDto dto) table.Add(dto);
            }

            return table;
        }

        /// <summary>
        /// Builds a strongly typed runtime entry table without reflection.
        /// </summary>
        public static object CreateEntryTable<TDto, TEntry>(
            Array dtos,
            Func<TDto, int> keySelector,
            Func<TDto, TEntry> entryFactory)
            where TDto : class
            where TEntry : class
        {
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (entryFactory == null) throw new ArgumentNullException(nameof(entryFactory));
            var table = new IntKeyConfigTable<TEntry>();
            if (dtos == null) return table;

            for (var i = 0; i < dtos.Length; i++)
            {
                if (!(dtos.GetValue(i) is TDto dto)) continue;
                var entry = entryFactory(dto);
                if (entry != null) table.Add(keySelector(dto), entry);
            }

            return table;
        }

        /// <summary>
        /// Collects DTO keys without reflection.
        /// </summary>
        public static void CollectChangedIds<TDto>(
            Array dtos,
            ISet<int> changedIds,
            Func<TDto, int> keySelector)
            where TDto : class
        {
            if (changedIds == null || dtos == null) return;
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            for (var i = 0; i < dtos.Length; i++)
            {
                if (dtos.GetValue(i) is TDto dto) changedIds.Add(keySelector(dto));
            }
        }
    }
}
