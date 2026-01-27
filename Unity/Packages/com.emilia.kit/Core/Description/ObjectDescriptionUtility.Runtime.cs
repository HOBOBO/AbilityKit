#if !UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Emilia.Kit
{
    public partial class ObjectDescriptionUtility
    {
        public static string GetGameObjectDescriptionFirst(GameObject gameObject, object owner = null, object userData = null) => null;
        public static string GetGameObjectDescription(GameObject gameObject, string separator = ",", object owner = null, object userData = null) => null;
        public static List<string> GetGameObjectDescriptions(GameObject gameObject, object owner = null, object userData = null) => null;
        public static string GetDescription(object obj, object owner = null, object userData = null) => null;
    }
}
#endif