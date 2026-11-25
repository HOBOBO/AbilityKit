using System;
using System.Collections.Generic;
using System.Reflection;

namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// 统一工厂与反射回退的配置表构建，不持有数据库状态或发布重载事件。
    /// </summary>
    internal static class ConfigTableMaterializer
    {
        internal static object CreateAndPopulateTable(ConfigTableDefinition definition, Array dtos)
        {
            if (definition.EntryTableFactory != null) return definition.EntryTableFactory(dtos);

            var entryType = definition.EntryType;
            var tableType = typeof(IntKeyConfigTable<>).MakeGenericType(entryType);
            var table = Activator.CreateInstance(tableType);

            if (dtos != null)
            {
                for (int i = 0; i < dtos.Length; i++)
                {
                    var dto = dtos.GetValue(i);
                    if (dto == null) continue;

                    /*
                     * 直接通过构造函数创建条目。
                     */
                    var entry = Activator.CreateInstance(entryType, dto);
                    if (entry == null) continue;

                    /*
                     * 直接调用 Add 方法。
                     */
                    var addMethod = tableType.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public);
                    if (addMethod == null) throw new InvalidOperationException($"Add not found on {tableType.FullName}");

                    /*
                     * 从 DTO 读取 Id。
                     */
                    var id = ReadIdFromDto(dto);
                    addMethod.Invoke(table, new[] { id, entry });
                }
            }

            return table;
        }

        private static int ReadIdFromDto(object dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            var type = dto.GetType();

            var field = type.GetField("Id", BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(int)) return (int)field.GetValue(dto);

            var property = type.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.PropertyType == typeof(int)) return (int)property.GetValue(dto);

            /*
             * 回退：尝试读取 Code 字段（Luban DR* 类型会使用该字段）。
             */
            field = type.GetField("Code", BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(int)) return (int)field.GetValue(dto);

            property = type.GetProperty("Code", BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.PropertyType == typeof(int)) return (int)property.GetValue(dto);

            throw new InvalidOperationException($"DTO must have int Id or Code field/property. type={type.FullName}");
        }

        internal static object CreateDtoTableFromDtos(ConfigTableDefinition definition, Array dtos)
        {
            if (definition.DtoTableFactory != null) return definition.DtoTableFactory(dtos);

            var dtoType = definition.DtoType;
            var tableType = typeof(ConfigDatabase.DtoTable<>).MakeGenericType(dtoType);
            var table = Activator.CreateInstance(tableType);
            var addMethod = tableType.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public);
            if (addMethod == null) throw new InvalidOperationException($"Add not found on {tableType.FullName}");

            if (dtos != null)
            {
                for (int i = 0; i < dtos.Length; i++)
                {
                    var dto = dtos.GetValue(i);
                    if (dto != null)
                    {
                        addMethod.Invoke(table, new[] { dto });
                    }
                }
            }

            return table;
        }

        internal static void CollectChangedIds(
            ConfigTableDefinition definition,
            Array entries,
            HashSet<int> changedIds)
        {
            if (definition.ChangedIdCollector != null)
            {
                definition.ChangedIdCollector(entries, changedIds);
                return;
            }

            if (entries == null) return;

            for (int i = 0; i < entries.Length; i++)
            {
                var dto = entries.GetValue(i);
                if (dto != null)
                {
                    var id = ReadIdFromDto(dto);
                    changedIds.Add(id);
                }
            }
        }
    }
}
