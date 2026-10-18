using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow
{
    internal readonly struct BattleHudImageElement
    {
        public BattleHudImageElement(GameObject gameObject, RectTransform rect, Image image)
        {
            GameObject = gameObject;
            Rect = rect;
            Image = image;
        }

        public GameObject GameObject { get; }

        public RectTransform Rect { get; }

        public Image Image { get; }
    }
}
