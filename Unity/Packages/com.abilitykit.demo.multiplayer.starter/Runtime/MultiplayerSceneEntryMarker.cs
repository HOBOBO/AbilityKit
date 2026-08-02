using UnityEngine;

namespace AbilityKit.Starter
{
    public enum MultiplayerStarterGameplay
    {
        Moba = 0,
        Shooter = 1
    }

    [DisallowMultipleComponent]
    public sealed class MultiplayerSceneEntryMarker : MonoBehaviour
    {
        [SerializeField] private MultiplayerStarterGameplay gameplay;

        public MultiplayerStarterGameplay Gameplay => gameplay;
    }
}
