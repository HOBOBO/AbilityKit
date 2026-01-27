#if UNITY_EDITOR
using System.Collections.Generic;
using Sirenix.OdinInspector.Editor;
using UnityEngine;

namespace Emilia.Kit
{
    public static class SelectedOwnerUtility
    {
        private static Dictionary<Object, object> selectedObjectOwnerMap = new Dictionary<Object, object>();

        public static void SetSelectedOwner(Object selectedObject, object owner)
        {
            if (selectedObject == null) return;
            if (owner == null) return;

            selectedObjectOwnerMap[selectedObject] = owner;
        }

        public static object GetSelectedOwner(Object selectedObject)
        {
            if (selectedObject == null) return null;
            return selectedObjectOwnerMap.GetValueOrDefault(selectedObject);
        }

        public static object GetSelectedOwner(InspectorProperty inspectorProperty)
        {
            while (inspectorProperty != null)
            {
                if (inspectorProperty.ValueEntry?.WeakSmartValue is Object selectedObject)
                {
                    if (selectedObject != null)
                    {
                        object owner = GetSelectedOwner(selectedObject);
                        if (owner != null) return owner;
                    }
                }

                inspectorProperty = inspectorProperty.Parent;
            }

            return null;
        }

        public static void Update()
        {
            List<Object> removeList = new List<Object>();

            foreach (var pair in selectedObjectOwnerMap)
            {
                if (pair.Key == null)
                {
                    removeList.Add(pair.Key);
                    continue;
                }

                ISelectedOwner selectedOwner = pair.Value as ISelectedOwner;
                if (selectedOwner != null && selectedOwner.Validate() == false)
                {
                    removeList.Add(pair.Key);
                    continue;
                }
            }

            foreach (Object selectedObject in removeList) selectedObjectOwnerMap.Remove(selectedObject);
        }
    }
}
#endif