#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Sirenix.Utilities;
using UnityEditor;
using UnityEngine;

namespace Emilia.Kit
{
    public static class HideUtility
    {
        private static Dictionary<Type, IObjectHideGetter> hideGetterMap = new Dictionary<Type, IObjectHideGetter>();

        static HideUtility()
        {
            IList<Type> types = TypeCache.GetTypesDerivedFrom<IObjectHideGetter>();

            int amount = types.Count;
            for (int i = 0; i < amount; i++)
            {
                Type type = types[i];
                if (type.IsAbstract || type.IsInterface) continue;

                ObjectHideAttribute attribute = type.GetCustomAttribute<ObjectHideAttribute>();
                if (attribute == null) continue;

                IObjectHideGetter getter = (IObjectHideGetter) Activator.CreateInstance(type);
                if (getter == null) continue;

                hideGetterMap.Add(attribute.objectType, getter);
            }
        }

        public static bool IsHide(Type type)
        {
            HideAttribute hideAttribute = type.GetCustomAttribute<HideAttribute>(true);
            if (hideAttribute != null) return true;
            return false;
        }

        public static bool IsHide(GameObject gameObject)
        {
            IHideDynamic hideDynamic = gameObject.GetComponent<IHideDynamic>();
            if (hideDynamic != null) return hideDynamic.isHide;

            IHide hide = gameObject.GetComponent<IHide>();
            if (hide != null) return true;

            return false;
        }

        public static bool IsHide(object instance, object owner = null, object userData = null)
        {
            Type type = instance.GetType();
            IObjectHideGetter getter = hideGetterMap.GetValueOrDefault(type);
            if (getter != null) return getter.IsHide(instance, owner, userData);

            IHideDynamic hideDynamic = instance as IHideDynamic;
            if (hideDynamic != null) return hideDynamic.isHide;

            IHide hide = instance as IHide;
            if (hide != null) return true;

            return IsHide(type);
        }
    }
}
#endif