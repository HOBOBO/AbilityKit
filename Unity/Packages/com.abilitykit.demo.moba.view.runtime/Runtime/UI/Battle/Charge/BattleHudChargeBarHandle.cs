using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow.Battle.Hud
{
    internal sealed class BattleHudChargeBarHandle
    {
        public int ActorId;
        public int Slot;
        public RectTransform Root;
        public Image Fill;
        public Vector3 WorldOffset;

        public void SetVisible(bool visible)
        {
            if (Root != null) Root.gameObject.SetActive(visible);
        }
    }
}
