#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Emilia.Kit.Editor;
using Sirenix.Utilities;
using UnityEditor;

namespace Emilia.Kit
{
    public static class HierarchyAssetMessageHandleUtility
    {
        private static Dictionary<(Type, string), IHierarchyAssetMessageHandle> messageHandles = new();

        static HierarchyAssetMessageHandleUtility()
        {
            IList<Type> types = TypeCache.GetTypesDerivedFrom<IHierarchyAssetMessageHandle>();

            int count = types.Count;
            for (int i = 0; i < count; i++)
            {
                Type type = types[i];
                if (type.IsAbstract || type.IsInterface) continue;

                HierarchyAssetMessageHandleAttribute attribute = type.GetCustomAttribute<HierarchyAssetMessageHandleAttribute>(true);
                if (attribute == null) continue;

                IHierarchyAssetMessageHandle handle = (IHierarchyAssetMessageHandle) ReflectUtility.CreateInstance(type);
                messageHandles[(attribute.targetType, attribute.message)] = handle;
            }
        }

        public static IHierarchyAssetMessageHandle GetHandle(Type type, string message)
        {
            if (messageHandles.TryGetValue((type, message), out IHierarchyAssetMessageHandle handle)) return handle;
            if (type.BaseType != null) return GetHandle(type.BaseType, message);
            return null;
        }
    }
}
#endif