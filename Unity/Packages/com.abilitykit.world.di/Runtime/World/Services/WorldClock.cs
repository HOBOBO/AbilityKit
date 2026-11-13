using System;

namespace AbilityKit.Ability.World.Services
{
    public sealed class WorldClock : IWorldClock
    {
        public float DeltaTime { get; private set; }
        public float Time { get; private set; }

        /// <summary>Restore an exact local simulation checkpoint at a synchronous restore boundary.
        /// This does not tick the world or derive time from a frame number.</summary>
        public void RestoreState(float time, float deltaTime)
        {
            if (float.IsNaN(time) || float.IsInfinity(time) || time < 0f)
                throw new ArgumentOutOfRangeException(nameof(time));
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            Time = time;
            DeltaTime = deltaTime;
        }

        public void Tick(float deltaTime)
        {
            DeltaTime = deltaTime;
            Time += deltaTime;
        }
    }
}
