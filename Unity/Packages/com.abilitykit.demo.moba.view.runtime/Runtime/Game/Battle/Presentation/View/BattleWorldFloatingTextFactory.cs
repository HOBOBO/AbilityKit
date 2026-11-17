using System.Collections.Generic;
using AbilityKit.Game.Battle.Hierarchy;
using UnityEngine;

namespace AbilityKit.Game.Flow.Battle.View
{
    internal sealed class BattleWorldFloatingTextFactory
    {
        private readonly Stack<BattleWorldFloatingText> _pool = new Stack<BattleWorldFloatingText>(64);
        private BattleViewHierarchyManager _hierarchy;

        public BattleWorldFloatingTextFactory(BattleViewHierarchyManager hierarchy = null)
        {
            _hierarchy = hierarchy;
        }

        public void SetHierarchyManager(BattleViewHierarchyManager hierarchy)
        {
            _hierarchy = hierarchy;
        }

        public BattleWorldFloatingText Create(string text, in Vector3 worldPos, Color color)
        {
            var floatingText = _pool.Count > 0 ? _pool.Pop() : CreateNew();
            _hierarchy?.ParentToCategory(BattleViewCategory.ActiveFloatingText, floatingText.GameObject);
            floatingText.Reset(text, in worldPos, color);
            return floatingText;
        }

        public void Release(BattleWorldFloatingText floatingText)
        {
            if (floatingText == null) return;
            if (floatingText.GameObject == null)
            {
                floatingText.Destroy();
                return;
            }

            floatingText.Deactivate();
            _hierarchy?.ParentToCategory(BattleViewCategory.PoolFloatingText, floatingText.GameObject);
            _pool.Push(floatingText);
        }

        public void ClearPool()
        {
            while (_pool.Count > 0)
            {
                _pool.Pop()?.Destroy();
            }
        }

        private static BattleWorldFloatingText CreateNew()
        {
            var go = new GameObject("DamageText");
            var textMesh = go.AddComponent<TextMesh>();
            textMesh.fontSize = 54;
            textMesh.characterSize = 0.075f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;

            var outlineTexts = new TextMesh[4];
            var offsets = new[]
            {
                new Vector3(-0.012f, 0f, 0f), new Vector3(0.012f, 0f, 0f),
                new Vector3(0f, -0.012f, 0f), new Vector3(0f, 0.012f, 0f),
            };
            for (var i = 0; i < outlineTexts.Length; i++)
            {
                var outline = new GameObject("Outline");
                outline.transform.SetParent(go.transform, false);
                outline.transform.localPosition = offsets[i];
                var outlineText = outline.AddComponent<TextMesh>();
                outlineText.font = textMesh.font;
                outlineText.fontSize = textMesh.fontSize;
                outlineText.characterSize = textMesh.characterSize;
                outlineText.anchor = textMesh.anchor;
                outlineText.alignment = textMesh.alignment;
                outlineText.color = Color.black;
                outlineText.GetComponent<MeshRenderer>().sortingOrder = -1;
                outlineTexts[i] = outlineText;
            }

            return new BattleWorldFloatingText
            {
                GameObject = go,
                Text = textMesh,
                OutlineTexts = outlineTexts,
                Lifetime = 1.1f,
                Velocity = new Vector3(0f, 1.5f, 0f),
            };
        }
    }
}
