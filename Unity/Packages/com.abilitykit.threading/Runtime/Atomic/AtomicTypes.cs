using System;
using System.Threading;

namespace AbilityKit.Threading
{
    /// <summary>
    /// 原子计数器（32位）
    /// </summary>
    public struct AtomicCounter
    {
        private int _value;

        public int Value => _value;

        public AtomicCounter(int initialValue)
        {
            _value = initialValue;
        }

        public static AtomicCounter operator +(AtomicCounter counter, int value)
        {
            return new AtomicCounter(counter._value + value);
        }

        public static AtomicCounter operator -(AtomicCounter counter, int value)
        {
            return new AtomicCounter(counter._value - value);
        }

        /// <summary>
        /// 原子递增
        /// </summary>
        public int Increment()
        {
            return Interlocked.Increment(ref _value);
        }

        /// <summary>
        /// 原子递减
        /// </summary>
        public int Decrement()
        {
            return Interlocked.Decrement(ref _value);
        }

        /// <summary>
        /// 原子加法
        /// </summary>
        public int Add(int value)
        {
            return Interlocked.Add(ref _value, value);
        }

        /// <summary>
        /// 原子交换
        /// </summary>
        public int Exchange(int value)
        {
            return Interlocked.Exchange(ref _value, value);
        }

        /// <summary>
        /// 比较并交换
        /// </summary>
        public bool CompareExchange(int expected, int desired, out int original)
        {
            original = Interlocked.CompareExchange(ref _value, desired, expected);
            return original == expected;
        }

        /// <summary>
        /// 重置为0
        /// </summary>
        public void Reset()
        {
            Interlocked.Exchange(ref _value, 0);
        }
    }

    /// <summary>
    /// 原子计数器（64位）
    /// </summary>
    public struct AtomicCounter64
    {
        private long _value;

        public long Value => _value;

        public AtomicCounter64(long initialValue)
        {
            _value = initialValue;
        }

        public static AtomicCounter64 operator +(AtomicCounter64 counter, long value)
        {
            return new AtomicCounter64(counter._value + value);
        }

        public static AtomicCounter64 operator -(AtomicCounter64 counter, long value)
        {
            return new AtomicCounter64(counter._value - value);
        }

        /// <summary>
        /// 原子递增
        /// </summary>
        public long Increment()
        {
            return Interlocked.Increment(ref _value);
        }

        /// <summary>
        /// 原子递减
        /// </summary>
        public long Decrement()
        {
            return Interlocked.Decrement(ref _value);
        }

        /// <summary>
        /// 原子加法
        /// </summary>
        public long Add(long value)
        {
            return Interlocked.Add(ref _value, value);
        }

        /// <summary>
        /// 原子交换
        /// </summary>
        public long Exchange(long value)
        {
            return Interlocked.Exchange(ref _value, value);
        }

        /// <summary>
        /// 比较并交换
        /// </summary>
        public bool CompareExchange(long expected, long desired, out long original)
        {
            original = Interlocked.CompareExchange(ref _value, desired, expected);
            return original == expected;
        }

        /// <summary>
        /// 重置为0
        /// </summary>
        public void Reset()
        {
            Interlocked.Exchange(ref _value, 0);
        }
    }

    /// <summary>
    /// 原子布尔值
    /// </summary>
    public struct AtomicBoolean
    {
        private int _value;

        public bool Value => _value != 0;

        public AtomicBoolean(bool initialValue)
        {
            _value = initialValue ? 1 : 0;
        }

        public static implicit operator bool(AtomicBoolean atomic)
        {
            return atomic.Value;
        }

        /// <summary>
        /// 设置为 true
        /// </summary>
        public bool Set()
        {
            return Interlocked.Exchange(ref _value, 1) == 1;
        }

        /// <summary>
        /// 设置为 false
        /// </summary>
        public bool Reset()
        {
            return Interlocked.Exchange(ref _value, 0) == 1;
        }

        /// <summary>
        /// 比较并交换
        /// </summary>
        public bool CompareExchange(bool expected, bool desired, out bool original)
        {
            var expectedInt = expected ? 1 : 0;
            var desiredInt = desired ? 1 : 0;
            var originalInt = Interlocked.CompareExchange(ref _value, desiredInt, expectedInt);
            original = originalInt == 1;
            return originalInt == expectedInt;
        }

        /// <summary>
        /// 尝试将 false 设为 true
        /// </summary>
        public bool TrySet()
        {
            return Interlocked.CompareExchange(ref _value, 1, 0) == 0;
        }
    }

    /// <summary>
    /// 原子引用
    /// </summary>
    public struct AtomicReference<T> where T : class
    {
        private volatile T _value;

        public T Value => _value;

        public AtomicReference(T initialValue)
        {
            _value = initialValue;
        }

        public static implicit operator T(AtomicReference<T> atomic)
        {
            return atomic._value;
        }

        /// <summary>
        /// 获取当前值
        /// </summary>
        public T Get()
        {
            return Volatile.Read(ref _value);
        }

        /// <summary>
        /// 设置新值
        /// </summary>
        public void Set(T value)
        {
            Volatile.Write(ref _value, value);
        }

        /// <summary>
        /// 原子交换
        /// </summary>
        public T Exchange(T value)
        {
            return Interlocked.Exchange(ref _value, value);
        }

        /// <summary>
        /// 比较并交换
        /// </summary>
        public bool CompareExchange(T expected, T desired, out T original)
        {
            original = Interlocked.CompareExchange(ref _value, desired, expected);
            return ReferenceEquals(original, expected);
        }

        /// <summary>
        /// 比较并交换（返回原始值）
        /// </summary>
        public T CompareExchange(T expected, T desired)
        {
            return Interlocked.CompareExchange(ref _value, desired, expected);
        }
    }

    /// <summary>
    /// 原子 double 值
    /// </summary>
    public struct AtomicDouble
    {
        private long _value;

        public double Value => BitConverter.Int64BitsToDouble(_value);

        public AtomicDouble(double initialValue)
        {
            _value = BitConverter.DoubleToInt64Bits(initialValue);
        }

        /// <summary>
        /// 原子读取
        /// </summary>
        public double Get()
        {
            return BitConverter.Int64BitsToDouble(Volatile.Read(ref _value));
        }

        /// <summary>
        /// 原子写入
        /// </summary>
        public void Set(double value)
        {
            Volatile.Write(ref _value, BitConverter.DoubleToInt64Bits(value));
        }

        /// <summary>
        /// 原子加法
        /// </summary>
        public double Add(double value)
        {
            var current = BitConverter.Int64BitsToDouble(_value);
            var newValue = BitConverter.DoubleToInt64Bits(current + value);

            while (Interlocked.CompareExchange(ref _value, newValue, BitConverter.DoubleToInt64Bits(current)) != BitConverter.DoubleToInt64Bits(current))
            {
                current = BitConverter.Int64BitsToDouble(_value);
                newValue = BitConverter.DoubleToInt64Bits(current + value);
            }

            return current + value;
        }

        /// <summary>
        /// 比较并交换
        /// </summary>
        public bool CompareExchange(double expected, double desired, out double original)
        {
            var expectedLong = BitConverter.DoubleToInt64Bits(expected);
            var desiredLong = BitConverter.DoubleToInt64Bits(desired);
            var originalLong = Interlocked.CompareExchange(ref _value, desiredLong, expectedLong);
            original = BitConverter.Int64BitsToDouble(originalLong);
            return originalLong == expectedLong;
        }
    }

    /// <summary>
    /// 原子标记（用于无锁数据结构）
    /// </summary>
    public struct AtomicMarker
    {
        private int _value;

        public int Value => _value & MarkerMask;
        public int Version => _value >> VersionShift;

        private const int MarkerMask = 0x7FFFFFFF;
        private const int VersionShift = 31;

        public AtomicMarker(int initialValue)
        {
            _value = initialValue & MarkerMask;
        }

        /// <summary>
        /// 获取标记值
        /// </summary>
        public int Get()
        {
            return Volatile.Read(ref _value) & MarkerMask;
        }

        /// <summary>
        /// 设置标记值
        /// </summary>
        public void Set(int value)
        {
            var version = _value >> VersionShift;
            Volatile.Write(ref _value, (value & MarkerMask) | (version << VersionShift));
        }

        /// <summary>
        /// 递增版本号
        /// </summary>
        public int IncrementVersion()
        {
            var oldValue = _value;
            var oldVersion = oldValue >> VersionShift;
            var newVersion = oldVersion + 1;
            var newValue = (oldValue & MarkerMask) | (newVersion << VersionShift);

            while (Interlocked.CompareExchange(ref _value, newValue, oldValue) != oldValue)
            {
                oldValue = _value;
                oldVersion = oldValue >> VersionShift;
                newVersion = oldVersion + 1;
                newValue = (oldValue & MarkerMask) | (newVersion << VersionShift);
            }

            return (int)newVersion;
        }
    }
}
