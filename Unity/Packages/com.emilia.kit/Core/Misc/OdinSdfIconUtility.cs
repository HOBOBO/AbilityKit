#if UNITY_EDITOR
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEngine;

namespace Emilia.Kit
{
    public static class OdinSdfIconUtility
    {
        private const int IconSize = 64;

        private static Dictionary<SdfIconType, Texture2D> _iconCache = new();

        public static Texture2D GetIcon(SdfIconType type)
        {
            if (_iconCache.TryGetValue(type, out Texture2D icon)) return icon;
            icon = SdfIcons.CreateTransparentIconTexture(type, Color.white, IconSize, IconSize, 0);
            _iconCache.Add(type, icon);
            return icon;
        }
    }
}
#endif