using System.Collections.Generic;
using Emilia.Kit;
using UnityEditor;
using UnityEngine;

namespace Emilia.Variables.Editor
{
    public partial class EditorParametersManager : ISelectedHandle
    {
        public bool Validate() => true;

        public bool IsSelected() => Selection.activeObject == this;

        public void Select() { }
        public void Unselect() { }

        public IEnumerable<Object> GetSelectedObjects()
        {
            yield return this;
        }
    }
}