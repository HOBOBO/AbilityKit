using System;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudImageElementFactory
    {
        public BattleHudImageElement Create(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPos,
            Vector2 size,
            Color color,
            bool raycastTarget,
            params Type[] extraComponents)
        {
            var go = new GameObject(name, BuildComponentTypes(extraComponents));
            go.transform.SetParent(parent, worldPositionStays: false);

            var rect = go.GetComponent<RectTransform>();
            BattleHudRectTransformLayout.SetAnchored(rect, anchorMin, anchorMax, anchoredPos, size);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;

            return new BattleHudImageElement(go, rect, image);
        }

        private static Type[] BuildComponentTypes(Type[] extraComponents)
        {
            var extraCount = extraComponents != null ? extraComponents.Length : 0;
            var types = new Type[2 + extraCount];
            types[0] = typeof(RectTransform);
            types[1] = typeof(Image);

            for (var i = 0; i < extraCount; i++)
            {
                types[2 + i] = extraComponents[i];
            }

            return types;
        }
    }
}
