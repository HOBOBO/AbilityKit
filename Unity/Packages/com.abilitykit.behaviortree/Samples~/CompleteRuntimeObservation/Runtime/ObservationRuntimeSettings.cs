using System;
using UnityEngine;

namespace AbilityKit.BehaviorTree.Samples.CompleteRuntimeObservation
{
    /// <summary>行为树示例的确定性调度与生命周期配置。</summary>
    [Serializable]
    public sealed class ObservationRuntimeSettings
    {
        [SerializeField, InspectorName("进入 Play Mode 时启动")] private bool _startOnEnable = true;
        [SerializeField, InspectorName("结束后重新开始")] private bool _autoRestart = true;
        [SerializeField, Min(1), InspectorName("每秒逻辑帧数")] private int _ticksPerSecond = 30;
        [SerializeField, InspectorName("随机种子")] private ulong _seed = 0xC0FFEEUL;

        public bool StartOnEnable => _startOnEnable;
        public bool AutoRestart => _autoRestart;
        public int TicksPerSecond => Math.Max(1, _ticksPerSecond);
        public ulong Seed => _seed;
    }
}
