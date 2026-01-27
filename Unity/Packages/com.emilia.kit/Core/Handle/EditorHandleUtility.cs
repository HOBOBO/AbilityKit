#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Emilia.Kit.Editor;
using Sirenix.Utilities;
using UnityEditor;
using UnityEngine;

namespace Emilia.Kit
{
    public static class EditorHandleUtility
    {
        private static Dictionary<Type, List<Type>> handleTypeCache = new Dictionary<Type, List<Type>>();

        static EditorHandleUtility()
        {
            IList<Type> generateTypes = TypeCache.GetTypesWithAttribute<EditorHandleGenerateAttribute>();

            int generateTypeCount = generateTypes.Count;
            for (int i = 0; i < generateTypeCount; i++)
            {
                Type generateType = generateTypes[i];

                IList<Type> types = TypeCache.GetTypesDerivedFrom(generateType);

                int count = types.Count;
                for (int j = 0; j < count; j++)
                {
                    Type type = types[j];
                    if (type.IsAbstract || type.IsInterface) continue;

                    EditorHandleAttribute attribute = type.GetCustomAttribute<EditorHandleAttribute>();
                    if (attribute == null) continue;

                    AddCache(attribute.targetType, type);
                }
            }

            void AddCache(Type targetType, Type type)
            {
                if (handleTypeCache.TryGetValue(targetType, out List<Type> types) == false)
                {
                    types = new List<Type>();
                    handleTypeCache.Add(targetType, types);
                }

                types.Add(type);
            }
        }

        public static T CreateHandle<T>(Type type)
        {
            Type filterType = typeof(T);
            Type currentType = type;

            Type[] interfaces = ReflectUtility.GetDirectInterfaces(currentType);
            for (int i = 0; i < interfaces.Length; i++)
            {
                Type interfaceType = interfaces[i];
                if (handleTypeCache.TryGetValue(interfaceType, out List<Type> types) == false) continue;

                T handle = GetHandle(types);
                if (handle != null) return handle;
            }

            while (currentType != null)
            {
                if (handleTypeCache.TryGetValue(currentType, out List<Type> types) == false)
                {
                    currentType = currentType.BaseType;
                    continue;
                }

                T handle = GetHandle(types);
                if (handle != null) return handle;

                currentType = currentType.BaseType;
            }

            T GetHandle(List<Type> types)
            {
                int count = types.Count;
                for (int i = 0; i < count; i++)
                {
                    Type handleType = types[i];
                    if (filterType.IsAssignableFrom(handleType) == false) continue;

                    if (handleType.IsSubclassOf(typeof(ScriptableObject))) return (T) (object) ScriptableObject.CreateInstance(handleType);
                    return (T) ReflectUtility.CreateInstance(handleType);
                }

                return default;
            }

            return default;
        }
    }
}
#endif