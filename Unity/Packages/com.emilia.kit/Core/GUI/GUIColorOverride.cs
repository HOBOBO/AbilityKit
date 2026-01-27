#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Emilia.Kit.Editor
{
    public struct GUIColorOverride : IDisposable
    {
        private Color oldColor;

        public GUIColorOverride(Color newColor)
        {
            this.oldColor = GUI.color;
            GUI.color = newColor;
        }

        public void Dispose()
        {
            GUI.color = this.oldColor;
        }
    }
}
#endif