using UnityEngine;

namespace AbilityKit.Game.Battle.Vfx
{
    /// <summary>Runtime cue values exposed to VFX-specific view scripts.</summary>
    public sealed class BattleVfxCueMetadata : MonoBehaviour
    {
        public int StackCount { get; private set; }
        public int MaxStackCount { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public float RemainingSeconds { get; private set; }

        public void Apply(int stackCount, int maxStackCount, float elapsedSeconds, float remainingSeconds)
        {
            StackCount = Mathf.Max(0, stackCount);
            MaxStackCount = Mathf.Max(0, maxStackCount);
            ElapsedSeconds = Mathf.Max(0f, elapsedSeconds);
            RemainingSeconds = Mathf.Max(0f, remainingSeconds);
        }

        private void Update()
        {
            var delta = Mathf.Max(0f, Time.deltaTime);
            ElapsedSeconds += delta;
            if (RemainingSeconds > 0f)
                RemainingSeconds = Mathf.Max(0f, RemainingSeconds - delta);
        }
    }
}
