using System;
using System.Collections.Generic;
using Emilia.Kit.Editor;
using UnityEditor;

namespace Emilia.Node.Editor
{
    public class GraphDataCache
    {
        private static Dictionary<Type, Type> graphDataCache = new();

        static GraphDataCache()
        {
            IList<Type> types = TypeCache.GetTypesDerivedFrom<GraphData>();
            for (var i = 0; i < types.Count; i++)
            {
                Type type = types[i];
                GraphDataAttribute graphDataAttribute = ReflectUtility.GetAttribute<GraphDataAttribute>(type);
                if (graphDataAttribute == null) continue;
                graphDataCache.Add(graphDataAttribute.graphAssetType, type);
            }
        }

        public static Type GetGraphDataType(Type graphAssetType) => graphDataCache.GetValueOrDefault(graphAssetType);
    }
}