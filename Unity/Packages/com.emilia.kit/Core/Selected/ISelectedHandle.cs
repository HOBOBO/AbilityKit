#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Emilia.Kit
{
    public interface ISelectedHandle
    {
        bool Validate();

        bool IsSelected();

        void Select();

        void Unselect();

        IEnumerable<Object> GetSelectedObjects();
    }
}
#endif