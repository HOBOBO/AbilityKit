using UnityEngine;
using UnityHFSM.Extension;

namespace AbilityKit.Examples.CompositeActionState
{
    public sealed class UnityActionTimeSource : IActionTimeSource
    {
        public float DeltaTime => Time.deltaTime;
        public float UnscaledDeltaTime => Time.unscaledDeltaTime;
    }

    public sealed class FixedActionTimeSource : IActionTimeSource
    {
        public float DeltaTime { get; private set; }
        public float UnscaledDeltaTime { get; private set; }

        public FixedActionTimeSource(float deltaTime, float unscaledDeltaTime)
        {
            DeltaTime = deltaTime;
            UnscaledDeltaTime = unscaledDeltaTime;
        }

        public void SetDelta(float deltaTime, float unscaledDeltaTime)
        {
            DeltaTime = deltaTime;
            UnscaledDeltaTime = unscaledDeltaTime;
        }
    }
}
