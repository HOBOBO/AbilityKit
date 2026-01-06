using UnityEngine;

namespace AbilityKit.Game.UI
{
    public abstract class UIPanel : UIBase
    {
        public RectTransform RectTransform => (RectTransform)transform;

        public virtual bool IsFullScreen => true;
        public virtual bool UseStack => Layer == UILayer.Popup;
    }
}
