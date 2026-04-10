using System;
using System.Runtime.CompilerServices;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 修改器生命周期状态
    // ============================================================================

    /// <summary>
    /// 修改器生命周期状态
    /// </summary>
    public enum ModifierLifecycleState : byte
    {
        /// <summary>未激活</summary>
        Inactive = 0,

        /// <summary>激活中</summary>
        Active = 1,

        /// <summary>即将过期（可用于触发预警事件）</summary>
        Expiring = 2,

        /// <summary>已过期</summary>
        Expired = 3,

        /// <summary>已移除</summary>
        Removed = 4,
    }

    /// <summary>
    /// 修改器生命周期信息
    /// </summary>
    [Serializable]
    public struct ModifierLifecycle
    {
        /// <summary>生效开始帧</summary>
        public int StartFrame;

        /// <summary>持续帧数（0 表示永久）</summary>
        public int DurationFrames;

        /// <summary>当前状态</summary>
        public ModifierLifecycleState State;

        /// <summary>是否永久有效</summary>
        public bool IsPermanent => DurationFrames == 0;

        /// <summary>
        /// 计算当前帧是否已过期
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsExpired(int currentFrame)
        {
            if (IsPermanent) return false;
            if (State >= ModifierLifecycleState.Removed) return true;
            return currentFrame > StartFrame + DurationFrames;
        }

        /// <summary>
        /// 获取剩余帧数
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetRemainingFrames(int currentFrame)
        {
            if (IsPermanent) return int.MaxValue;
            int remaining = StartFrame + DurationFrames - currentFrame;
            return remaining > 0 ? remaining : 0;
        }

        /// <summary>
        /// 获取已过帧数
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetElapsedFrames(int currentFrame)
        {
            if (StartFrame == 0) return 0;
            return currentFrame > StartFrame ? currentFrame - StartFrame : 0;
        }

        /// <summary>
        /// 获取进度（0-1）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetProgress(int currentFrame)
        {
            if (IsPermanent || DurationFrames == 0) return 1f;
            int elapsed = GetElapsedFrames(currentFrame);
            return (float)elapsed / DurationFrames;
        }

        /// <summary>
        /// 创建永久生命周期
        /// </summary>
        public static ModifierLifecycle Permanent(int startFrame = 0)
            => new() { StartFrame = startFrame, DurationFrames = 0, State = ModifierLifecycleState.Active };

        /// <summary>
        /// 创建临时生命周期
        /// </summary>
        public static ModifierLifecycle Temporary(int startFrame, int durationFrames)
            => new() { StartFrame = startFrame, DurationFrames = durationFrames, State = ModifierLifecycleState.Active };
    }

    // ============================================================================
    // 修改器上下文数据（纯数据，传参用）
    // ============================================================================

    /// <summary>
    /// 修改器上下文数据（纯值类型，用于计算）。
    /// 不包含任何引用类型。
    /// </summary>
    public struct ModifierContextData
    {
        /// <summary>当前等级</summary>
        public float Level;

        /// <summary>当前帧</summary>
        public int CurrentFrame;

        /// <summary>当前时间戳（毫秒）</summary>
        public int CurrentTimeMs;

        /// <summary>增量时间（毫秒）</summary>
        public int DeltaTimeMs;

        /// <summary>修改器生效已过帧数</summary>
        public int ElapsedFrames;

        /// <summary>修改器生效已过时间（毫秒）</summary>
        public int ElapsedTimeMs;

        /// <summary>元数据</summary>
        public ModifierMetadata Metadata;

        /// <summary>
        /// 获取属性值（需要外部实现）
        /// </summary>
        public float GetAttribute(ModifierKey key) => 0f;

        /// <summary>
        /// 获取曲线数据（需要外部实现）
        /// </summary>
        public float[] GetCurveData(int index) => null;

        /// <summary>创建空上下文</summary>
        public static ModifierContextData Empty => default;
    }
}