using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Tiny.View;
using UnityEngine;

namespace AbilityKit.Demo.Tiny.Samples
{
    public sealed class TinyProjectLobbyEntry : MonoBehaviour
    {
        public void Enter(DemoMultiplayerLaunchRequest authenticatedLaunch)
        {
            TinyProjectLaunch.Open(authenticatedLaunch, gameObject.scene.name);
        }
    }
}
