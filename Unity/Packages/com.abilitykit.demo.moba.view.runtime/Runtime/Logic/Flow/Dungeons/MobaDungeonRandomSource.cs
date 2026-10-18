using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow.Dungeons
{
    /// <summary>
    /// 副本内的确定性随机源。随机波次、随机生成点、随机 BOSS 选择等副本级决策必须走这里，
    /// 而不是直接用 <see cref="Random"/>：只有被记录的决策才能被回放复现。
    ///
    /// 约定：
    /// - 使用 xorshift32，不依赖 <see cref="Random"/> 的平台实现差异。
    /// - 每个决策记录其原始随机值，回放端按同样顺序喂回即可复现同一结果序列。
    /// </summary>
    public sealed class MobaDungeonRandomSource
    {
        private readonly List<DungeonRandomDecision> _decisions = new List<DungeonRandomDecision>();
        private uint _state;

        public MobaDungeonRandomSource(int seed)
        {
            Seed = seed;
            _state = Mix(unchecked((uint)seed));
        }

        /// <summary>本局种子，与回放清单里的 <c>RandomSeed</c> 对应。</summary>
        public int Seed { get; }

        /// <summary>已记录的决策，按发生顺序。</summary>
        public IReadOnlyList<DungeonRandomDecision> Decisions => _decisions;

        /// <summary>在 [minInclusive, maxExclusive) 内取一个确定性随机整数。</summary>
        public int NextInt(int minInclusive, int maxExclusive, int frame, string channel)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            var raw = NextUInt();
            _decisions.Add(new DungeonRandomDecision(frame, channel, _decisions.Count, raw));
            var span = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(raw % span);
        }

        /// <summary>按概率返回 true。概率被夹到 [0,1]，0/1 不消耗随机数。</summary>
        public bool NextChance(double probability, int frame, string channel)
        {
            if (probability <= 0d) return false;
            if (probability >= 1d) return true;
            return NextInt(0, 1000000, frame, channel) < probability * 1000000d;
        }

        /// <summary>已记录的原始随机值，按发生顺序。</summary>
        public IReadOnlyList<uint> RawValues
        {
            get
            {
                var values = new uint[_decisions.Count];
                for (var i = 0; i < _decisions.Count; i++) values[i] = _decisions[i].RawValue;
                return values;
            }
        }

        /// <summary>
        /// 回放端校验：确认按给定原始值重放时，第 index 个决策与录制端一致。
        /// </summary>
        public bool TryVerify(int index, uint replayedRawValue, out string error)
        {
            error = null;
            if (index < 0 || index >= _decisions.Count)
            {
                error = "Decision index " + index + " is out of range (recorded " +
                        _decisions.Count + ").";
                return false;
            }
            if (_decisions[index].RawValue != replayedRawValue)
            {
                error = "Decision " + index + " on channel '" + _decisions[index].Channel +
                        "' diverged: recorded " + _decisions[index].RawValue +
                        ", replayed " + replayedRawValue + ".";
                return false;
            }
            return true;
        }

        /// <summary>xorshift32：跨平台可复现。</summary>
        private uint NextUInt()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }

        private static uint Mix(uint value)
        {
            var x = value;
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x == 0u ? 0x9E3779B9u : x;
        }
    }

    /// <summary>
    /// 一条副本随机决策记录。<see cref="RawValue"/> 是决策的原始随机值，
    /// 回放端按同样顺序喂回同一串原始值即可复现结果。
    /// </summary>
    public readonly struct DungeonRandomDecision
    {
        public DungeonRandomDecision(int frame, string channel, int ordinal, uint rawValue)
        {
            Frame = frame;
            Channel = channel ?? string.Empty;
            Ordinal = ordinal;
            RawValue = rawValue;
        }

        /// <summary>决策发生的逻辑帧。</summary>
        public int Frame { get; }
        /// <summary>决策通道，例如 "wave" / "spawn" / "boss"，用于回放定位。</summary>
        public string Channel { get; }
        /// <summary>本局内的决策序号。</summary>
        public int Ordinal { get; }
        public uint RawValue { get; }
    }
}
