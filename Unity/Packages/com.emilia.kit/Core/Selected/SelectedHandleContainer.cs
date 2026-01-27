#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Emilia.Kit
{
    public class SelectedHandleContainer : ISelectedHandle
    {
        public Object selectedObject { get; set; }

        public Action<SelectedHandleContainer> onSelected { get; set; }
        public Action<SelectedHandleContainer> onUnselected { get; set; }

        public SelectedHandleContainer(Object selectedObject)
        {
            this.selectedObject = selectedObject;
        }

        public bool Validate()
        {
            return true;
        }

        public bool IsSelected()
        {
            return Selection.objects.Contains(this.selectedObject);
        }

        public void Select()
        {
            onSelected?.Invoke(this);
        }

        public void Unselect()
        {
            onUnselected?.Invoke(this);
        }

        public IEnumerable<Object> GetSelectedObjects()
        {
            yield return this.selectedObject;
        }
    }
}
#endif