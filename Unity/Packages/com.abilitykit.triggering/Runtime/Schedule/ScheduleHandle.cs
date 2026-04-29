using System;

namespace AbilityKit.Triggering.Runtime.Schedule
{
    /// <summary>
    /// 调度句柄
    /// 用于引用和操作已注册的调度项
    /// </summary>
    public readonly struct ScheduleHandle : IEquatable<ScheduleHandle>
    {
        /// <summary>句柄ID</summary>
        public readonly int HandleId;

        /// <summary>内部索引</summary>
        public readonly int Index;

        public bool IsValid => HandleId > 0;

        public static ScheduleHandle Invalid => default;

        public ScheduleHandle(int handleId, int index)
        {
            HandleId = handleId;
            Index = index;
        }

        public bool Equals(ScheduleHandle other) => HandleId == other.HandleId;
        public override bool Equals(object obj) => obj is ScheduleHandle other && Equals(other);
        public override int GetHashCode() => HandleId;
        public static bool operator ==(ScheduleHandle left, ScheduleHandle right) => left.Equals(right);
        public static bool operator !=(ScheduleHandle left, ScheduleHandle right) => !left.Equals(right);
        public override string ToString() => IsValid ? $"Schedule[{HandleId}]" : "Schedule[Invalid]";
    }
}
