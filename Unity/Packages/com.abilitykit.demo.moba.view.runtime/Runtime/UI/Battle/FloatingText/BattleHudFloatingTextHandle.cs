using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudFloatingTextHandle
    {
        public int TargetActorId;
        public RectTransform Root;
        public Text Text;
        public float Age;
        public float Lifetime;
        public Vector3 WorldOffset;
        public Vector2 ScreenOffset;
    }
}
