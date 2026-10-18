using UnityEngine;

namespace AbilityKit.Game.UI
{
    public abstract class UIWidget : UIBase
    {
        public RectTransform RectTransform => (RectTransform)transform;
    }
}
