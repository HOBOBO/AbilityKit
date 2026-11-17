using UnityEngine;

namespace AbilityKit.Game.Flow.Battle.View
{
    internal sealed class BattleWorldFloatingText
    {
        public GameObject GameObject { get; set; }
        public TextMesh Text { get; set; }
        public TextMesh[] OutlineTexts { get; set; }
        public float Age { get; set; }
        public float Lifetime { get; set; }
        public Vector3 Velocity { get; set; }
        public Color BaseColor { get; set; }

        public void Reset(string text, in Vector3 worldPos, Color color)
        {
            if (GameObject == null || Text == null) return;

            GameObject.transform.position = worldPos;
            GameObject.SetActive(true);
            Text.text = text;
            Text.color = color;
            SetOutline(text, color.a);
            Age = 0f;
            Lifetime = 1.1f;
            Velocity = new Vector3(0f, 1.5f, 0f);
            BaseColor = color;
        }

        public bool Tick(float deltaTime)
        {
            if (GameObject == null || Text == null) return false;
 
            Age += deltaTime;
            GameObject.transform.position += Velocity * deltaTime;

            var t = Lifetime > 0f ? Mathf.Clamp01(Age / Lifetime) : 1f;
            var fade = Mathf.Clamp01((t - 0.72f) / 0.28f);
            var color = BaseColor;
            color.a = 1f - fade;
            Text.color = color;
            SetOutline(Text.text, color.a);

            return Age < Lifetime;
        }

        public void Deactivate()
        {
            if (GameObject != null)
            {
                GameObject.SetActive(false);
            }
        }

        public void Destroy()
        {
            if (GameObject != null)
            {
                if (Application.isPlaying) Object.Destroy(GameObject);
                else Object.DestroyImmediate(GameObject);
                GameObject = null;
            }
 
            Text = null;
            OutlineTexts = null;
        }

        private void SetOutline(string text, float alpha)
        {
            if (OutlineTexts == null) return;
            var color = new Color(0f, 0f, 0f, alpha);
            for (var i = 0; i < OutlineTexts.Length; i++)
            {
                if (OutlineTexts[i] == null) continue;
                OutlineTexts[i].text = text;
                OutlineTexts[i].color = color;
            }
        }
    }
}
