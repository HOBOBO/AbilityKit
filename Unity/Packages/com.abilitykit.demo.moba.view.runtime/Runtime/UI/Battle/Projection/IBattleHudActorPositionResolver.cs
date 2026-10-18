using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal interface IBattleHudActorPositionResolver
    {
        bool TryGetActorWorldPos(int actorId, out Vector3 pos);
    }
}
