using System;
using System.Threading;

namespace AbilityKit.Triggering.Runtime.Random
{
    /// <summary>
    /// 确定性随机数生成器（Xoroshiro128+ 算法）
    /// 支持通过种子生成可重现的随机序列
    /// </summary>
    public sealed class DeterministicRandom : IRandomProvider
    {
        private ulong _s0;
        private ulong _s1;
        private uint _sequence;

        /// <summary>当前序列号</summary>
        public uint CurrentSequence => _sequence;

        /// <summary>创建确定性随机数生成器</summary>
        /// <param name="seed">随机种子（默认 0x12345678）</param>
        public DeterministicRandom(uint seed = 0x12345678)
        {
            SetSeed(seed);
            _sequence = 0;
        }

        /// <summary>设置随机种子</summary>
        public void SetSeed(uint seed)
        {
            // SplitMix64 初始化
            var splitmix = new SplitMix64(seed);
            _s0 = splitmix.Next();
            _s1 = splitmix.Next();
            _sequence = 0;
        }

        /// <summary>获取指定范围的随机整数 [min, max)</summary>
        public int Next(int min, int max)
        {
            if (min >= max) throw new ArgumentException("min >= max");
            var value = (int)(Next() % (ulong)(max - min)) + min;
            _sequence++;
            return value;
        }

        /// <summary>获取随机浮点数 [0.0, 1.0)</summary>
        public float NextFloat()
        {
            var value = (float)NextDouble();
            _sequence++;
            return value;
        }

        /// <summary>生成下一个随机数（64位）</summary>
        private ulong Next()
        {
            var result = _s0 + _s1;

            var s1 = _s0 ^ _s1;
            _s0 = RotateLeft(_s0, 55) ^ s1 ^ (s1 << 14);
            _s1 = RotateLeft(s1, 36);

            return result;
        }

        /// <summary>生成下一个双精度浮点数 [0.0, 1.0)</summary>
        private double NextDouble()
        {
            return (Next() >> 11) * (1.0 / (1UL << 53));
        }

        /// <summary>左旋转</summary>
        private static ulong RotateLeft(ulong x, int k)
        {
            return (x << k) | (x >> (64 - k));
        }

        /// <summary>SplitMix64 辅助类（用于种子初始化）</summary>
        private sealed class SplitMix64
        {
            private ulong _x;

            public SplitMix64(uint seed)
            {
                _x = seed;
            }

            public ulong Next()
            {
                ulong z = (_x += 0x9E3779B97F4A7C15);
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
                return z ^ (z >> 31);
            }
        }
    }
}
