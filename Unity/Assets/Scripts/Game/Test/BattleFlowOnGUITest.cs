using System;
using AbilityKit.Game.Flow;
using UnityEngine;

namespace AbilityKit.Game.Test
{
    public sealed class BattleFlowOnGUITest : MonoBehaviour
    {
        private void Start()
        {
            if (!AbilityKit.Game.GameEntry.IsInitialized) return;

            var entry = AbilityKit.Game.GameEntry.Instance;
            entry.DebugEnabled = true;

            if (entry.Root.IsValid && entry.Root.TryGetComponent(out GameFlowDomain flow) && flow != null)
            {
                flow.EnterBattle(new TestBattleBootstrapper());
            }
        }
    }
}
