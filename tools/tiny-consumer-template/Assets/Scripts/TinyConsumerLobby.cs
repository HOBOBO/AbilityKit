using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Tiny.View;
using UnityEngine;

namespace TinyConsumer
{
    public sealed class TinyConsumerLobby : MonoBehaviour
    {
        public void Enter(DemoMultiplayerLaunchRequest authenticatedLaunch)
        {
            TinyProjectLaunch.Open(authenticatedLaunch, gameObject.scene.name);
        }
    }
}
