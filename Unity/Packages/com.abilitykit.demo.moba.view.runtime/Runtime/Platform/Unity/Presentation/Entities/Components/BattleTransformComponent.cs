using UnityEngine;

namespace AbilityKit.Game.Battle.Component
{
    public sealed class BattleTransformComponent
    {
        public Vector3 Position = new();
        public Vector3 Forward;
        public Vector3 Scale = Vector3.one;
    }
}
