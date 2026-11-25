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
        public float ShieldOverflow;
        public Vector3 WorldOffset;
        public BattleHudOverheadView View;
        public int LayoutId;
        public bool HasHealth;
        public readonly System.Collections.Generic.Dictionary<int, BattleHudIndicatorRow> Rows =
            new System.Collections.Generic.Dictionary<int, BattleHudIndicatorRow>();
    }
}
