using System;
using UnityEngine;

namespace AbilityKit.BehaviorTree.Samples.CompleteRuntimeObservation
{
    /// <summary>行为树示例的确定性调度与生命周期配置。</summary>
    [Serializable]
    public sealed class ObservationRuntimeSettings
    {
        [SerializeField] private bool _startOnEnable = true;
        [SerializeField] private bool _autoRestart = true;
        [SerializeField, Min(1)] private int _ticksPerSecond = 30;
        [SerializeField] private ulong _seed = 0xC0FFEEUL;

        public bool StartOnEnable => _startOnEnable;
        public bool AutoRestart => _autoRestart;
        public int TicksPerSecond => Math.Max(1, _ticksPerSecond);
        public ulong Seed => _seed;
    }
}
