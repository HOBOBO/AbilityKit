using UnityEngine;

namespace AbilityKit.Game.Flow.Battle.View
{
    internal sealed class BattleWorldFloatingText
    {
        public GameObject GameObject { get; set; }
        public TextMesh Text { get; set; }
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
            Age = 0f;
            Lifetime = 0.9f;
            Velocity = new Vector3(0f, 1.5f, 0f);
            BaseColor = color;
        }

        public bool Tick(float deltaTime)
        {
            if (GameObject == null || Text == null) return false;
 
            Age += deltaTime;
            GameObject.transform.position += Velocity * deltaTime;

            var t = Lifetime > 0f ? Mathf.Clamp01(Age / Lifetime) : 1f;
            var color = BaseColor;
            color.a = 1f - t;
            Text.color = color;

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
        }
    }
}
