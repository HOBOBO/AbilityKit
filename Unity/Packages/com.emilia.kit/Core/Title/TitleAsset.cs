#if UNITY_EDITOR
using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Emilia.Kit
{
    [Serializable]
    public abstract class TitleAsset : SerializedScriptableObject
    {
        public abstract string title { get; }
        public virtual void OnCustomGUI(Rect rect) { }
    }
}
#endif