using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;

namespace AbilityKit.Ability.Editor
{
    internal static class TriggerTypeScanUtil
    {
        internal readonly struct Entry
        {
            public Entry(string type, string displayName, string category, int order, Type implType)
            {
                Type = type ?? string.Empty;
                DisplayName = string.IsNullOrEmpty(displayName) ? Type : displayName;
                Category = category ?? string.Empty;
                Order = order;
                ImplType = implType;
            }

            public string Type { get; }
            public string DisplayName { get; }
            public string Category { get; }
            public int Order { get; }
            public Type ImplType { get; }

            public string BuildMenuPath()
            {
                var leaf = $"{DisplayName}  [{Type}]";
                return string.IsNullOrEmpty(Category) ? leaf : (Category + "/" + leaf);
            }
        }

        public static List<ValueDropdownItem<Type>> CollectStrongConfigTypes(Type baseType)
        {
            if (baseType == null) throw new ArgumentNullException(nameof(baseType));

            var list = new List<Entry>();
            foreach (var t in GetTypesSafe())
            {
                if (t == null || t.IsAbstract) continue;
                if (!baseType.IsAssignableFrom(t)) continue;

                var c = (TriggerConditionTypeAttribute)Attribute.GetCustomAttribute(t, typeof(TriggerConditionTypeAttribute));
                if (c != null && !string.IsNullOrEmpty(c.Type))
                {
                    list.Add(new Entry(c.Type, c.DisplayName, c.Category, c.Order, t));
                    continue;
                }

                var a = (TriggerActionTypeAttribute)Attribute.GetCustomAttribute(t, typeof(TriggerActionTypeAttribute));
                if (a != null && !string.IsNullOrEmpty(a.Type))
                {
                    list.Add(new Entry(a.Type, a.DisplayName, a.Category, a.Order, t));
                }
            }

            return list
                .OrderBy(e => e.Category, StringComparer.Ordinal)
                .ThenBy(e => e.Order)
                .ThenBy(e => e.DisplayName, StringComparer.Ordinal)
                .ThenBy(e => e.Type, StringComparer.Ordinal)
                .Select(e => new ValueDropdownItem<Type>(e.BuildMenuPath(), e.ImplType))
                .ToList();
        }

        private static IEnumerable<Type> GetTypesSafe()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type[] types;
                try
                {
                    types = assemblies[i].GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                if (types == null) continue;
                for (int j = 0; j < types.Length; j++)
                {
                    yield return types[j];
                }
            }
        }
    }
}
