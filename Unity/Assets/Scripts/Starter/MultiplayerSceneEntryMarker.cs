using AbilityKit.Demo.Shooter.View.PlayMode;
using AbilityKit.Game.Flow;
using UnityEngine;

namespace AbilityKit.Starter
{
    public enum MultiplayerStarterGameplay
    {
        Moba = 0,
        Shooter = 1
    }

    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class MultiplayerSceneEntryMarker : MonoBehaviour
    {
        [SerializeField] private MultiplayerStarterGameplay gameplay;

        private void Awake()
        {
            if (gameplay == MultiplayerStarterGameplay.Moba)
            {
                MobaMultiplayerLaunchContext.Request();
                return;
            }

            ShooterMultiplayerLaunchContext.Request();
        }
    }
}
