using System.Collections.Generic;
using UnityEngine;
using EC = AbilityKit.Ability.EC;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleFloatingTextSystem
    {
        private sealed class FloatingText
        {
            public GameObject Go;
            public TextMesh Text;
            public float Age;
            public float Lifetime;
            public Vector3 Velocity;
            public Color BaseColor;
        }

        private readonly List<FloatingText> _floatingTexts = new List<FloatingText>(64);

        public void Spawn(in EC.Entity vfxNode, string text, in Vector3 worldPos, Color color)
        {
            if (!vfxNode.IsValid) return;

            var go = new GameObject("DamageText");
            go.transform.position = worldPos;

            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.color = color;
            tm.fontSize = 42;
            tm.characterSize = 0.06f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;

            var ft = new FloatingText
            {
                Go = go,
                Text = tm,
                Age = 0f,
                Lifetime = 0.9f,
                Velocity = new Vector3(0f, 1.5f, 0f),
                BaseColor = color,
            };
            _floatingTexts.Add(ft);
        }

        public void Tick(float deltaTime)
        {
            if (_floatingTexts.Count == 0) return;

            for (int i = _floatingTexts.Count - 1; i >= 0; i--)
            {
                var ft = _floatingTexts[i];
                if (ft == null || ft.Go == null || ft.Text == null)
                {
                    _floatingTexts.RemoveAt(i);
                    continue;
                }

                ft.Age += deltaTime;
                ft.Go.transform.position += ft.Velocity * deltaTime;

                var t = ft.Lifetime > 0f ? Mathf.Clamp01(ft.Age / ft.Lifetime) : 1f;
                var c = ft.BaseColor;
                c.a = 1f - t;
                ft.Text.color = c;

                if (ft.Age >= ft.Lifetime)
                {
                    Object.Destroy(ft.Go);
                    _floatingTexts.RemoveAt(i);
                }
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _floatingTexts.Count; i++)
            {
                var ft = _floatingTexts[i];
                if (ft?.Go != null)
                {
                    Object.Destroy(ft.Go);
                }
            }
            _floatingTexts.Clear();
        }
    }
}
