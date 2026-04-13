using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// int 主键的配置表实现
    /// </summary>
    /// <typeparam name="TEntry">配置条目类型</typeparam>
    public sealed class IntKeyConfigTable<TEntry> : IConfigTable<TEntry> where TEntry : class
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

            // Fallback: try "Code" field (used by Luban DR* types like DRCharacters)
            field = type.GetField("Code");
            if (field != null && field.FieldType == typeof(int)) return (int)field.GetValue(dto);
            property = type.GetProperty("Code");
            if (property != null && property.PropertyType == typeof(int)) return (int)property.GetValue(dto);

            throw new InvalidOperationException($"DTO must have int Id or Code field/property. type={type.FullName}");
        }
    }
}
