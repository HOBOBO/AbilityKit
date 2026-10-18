using UnityTime = UnityEngine.Time;

namespace AbilityKit.Game.Battle.Shared.Time
{
    public sealed class UnityBattleViewTimeSource : IBattleViewTimeSource
    {
        public static readonly UnityBattleViewTimeSource Shared = new UnityBattleViewTimeSource();

        public float TimeSeconds => UnityTime.time;
        public int FrameCount => UnityTime.frameCount;
    }
}
