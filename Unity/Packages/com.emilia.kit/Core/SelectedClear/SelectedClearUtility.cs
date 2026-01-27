#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.Utilities;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Emilia.Kit
{
    public static class SelectedClearUtility
    {
        [InitializeOnLoadMethod]
        public static void ClearSelected()
        {
            List<Object> selectedObjects = Selection.objects.ToList();
            if (selectedObjects.Count == 0) return;

            for (int i = selectedObjects.Count - 1; i >= 0; i--)
            {
                Object selectedObject = selectedObjects[i];
                if (selectedObject == null)
                {
                    selectedObjects.RemoveAt(i);
                    continue;
                }

                Type type = selectedObject.GetType();
                SelectedClearAttribute attribute = type.GetCustomAttribute<SelectedClearAttribute>(true);
                if (attribute != null)
                {
                    selectedObjects.RemoveAt(i);
                    continue;
                }
            }

            if (selectedObjects.Count == 0)
            {
                Selection.objects = Array.Empty<Object>();
                return;
            }

            Selection.objects = selectedObjects.ToArray();
        }
    }
}
#endif