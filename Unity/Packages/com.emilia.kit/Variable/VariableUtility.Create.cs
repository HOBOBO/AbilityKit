using System;
using UnityEngine;

namespace Emilia.Variables
{
    public static partial class VariableUtility
    {
        public static Variable Create<T>(bool fromPool = false)
        {
            Type type = typeof(T);
            return Create(type, fromPool);
        }

        public static Variable Create(Type type, bool fromPool = false)
        {
            if (fromPool)
            {
                if (variableCreateFromPool.TryGetValue(type, out var createFromPool)) return createFromPool();
                Debug.LogError($"{type} 未注册");
                return null;
            }
            else
            {
                if (variableCreate.TryGetValue(type, out var create)) return create();
                Debug.LogError($"{type} 未注册");
                return null;
            }
        }
    }
}