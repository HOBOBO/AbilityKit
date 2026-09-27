using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Tiny.Turn.View;
using UnityEngine;

namespace TinyConsumer.Turn
{
    public sealed class TinyConsumerTurnLobby : MonoBehaviour
    {
        public void Enter(DemoMultiplayerLaunchRequest authenticatedLaunch)
        {
            TinyTurnProjectLaunch.Open(authenticatedLaunch, gameObject.scene.name);
        }
    }
}
