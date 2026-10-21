using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudHpBarHandle
    {
        public int ActorId;
        public RectTransform Root;
        public Image HpFill;
        public Image ShieldFill;
        public float Hp;
        public float MaxHp;
        public float Shield;
        public float ShieldCapacity;
        public Vector3 WorldOffset;
    }
}
