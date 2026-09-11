#nullable enable

using System;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Editor
{
    /// <summary>
    /// Unity 宿主的即时执行环境：一次性跑完样本，不跨帧。
    ///
    /// 这与 Console 宿主的即时模式语义一致，因此同一份示例在两边产出相同输出——
    /// golden 基线正是按即时模式固化的。跨帧实时驱动（<see cref="ExecutionMode.Realtime"/>）
    /// 留给后续的可视化步骤，届时挂到 EditorApplication.update 上。
    /// </summary>
    internal sealed class SampleHostEnvironment : ISampleEnvironment
    {
        private float _time;

        public float Time => _time;

        public float DeltaTime => 0f;

        public bool IsPaused => false;

#pragma warning disable CS0067 // 即时模式下不会产生 Tick 回调
        public event Action<float>? OnTick;
#pragma warning restore CS0067

        public void Advance(float delta)
        {
            _time += delta;
        }

        public void Pause()
        {
        }

        public void Resume()
        {
        }

        public void Reset()
        {
            _time = 0f;
        }

        public void AdvanceTo(float targetTime)
        {
            _time = targetTime;
        }

        public void Tick()
        {
            OnTick?.Invoke(0f);
        }

        public void ExecuteUntilComplete()
        {
            OnTick?.Invoke(0f);
        }
    }
}
