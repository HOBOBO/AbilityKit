#if UNITY_EDITOR
using UnityEngine;

namespace Emilia.Kit
{
    public interface IGUILayer
    {
        int order { get; }

        void Draw(Rect rect, object userData);
    }
}
#endif