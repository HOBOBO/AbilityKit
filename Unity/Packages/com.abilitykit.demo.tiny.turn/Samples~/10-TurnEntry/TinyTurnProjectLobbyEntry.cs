using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Tiny.Turn.View;
using UnityEngine;

namespace AbilityKit.Demo.Tiny.Turn.Samples
{
    public sealed class TinyTurnProjectLobbyEntry : MonoBehaviour
    {
        public void Enter(DemoMultiplayerLaunchRequest authenticatedLaunch)
        {
            TinyTurnProjectLaunch.Open(authenticatedLaunch, gameObject.scene.name);
        }
    }
}
