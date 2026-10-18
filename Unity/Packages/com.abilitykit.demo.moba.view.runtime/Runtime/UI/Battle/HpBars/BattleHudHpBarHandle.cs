using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudHpBarHandle
    {
        public int ActorId;
        public RectTransform Root;
        public Image HpFill;
        public float Hp;
        public float MaxHp;
        public Vector3 WorldOffset;
    }
}
