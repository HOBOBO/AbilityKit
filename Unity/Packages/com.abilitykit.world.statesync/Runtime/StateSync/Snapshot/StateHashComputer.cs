using System;
using System.Security.Cryptography;

namespace AbilityKit.Ability.StateSync.Snapshot
{
    /// <summary>
    /// 计算或比较框架元数据哈希及可选业务哈希；相同哈希不证明状态完整、可恢复或可信。
    /// </summary>
    public static class StateHashComputer
    {
        private const uint HASH_SEED = 0x9E3779B9u;

        /// <summary>
        /// 依次组合版本、帧、时间戳和标志，不包含世界标识、完整快照标记或业务状态；空快照返回无效值。
        /// </summary>
        public static StateHash Compute(WorldStateSnapshot snapshot)
        {
            if (snapshot == null) return StateHash.Invalid;

            unchecked
            {
                ulong hash = (ulong)HASH_SEED;
                hash = HashCombine(hash, (long)snapshot.Version);
                hash = HashCombine(hash, (long)snapshot.Frame);
                hash = HashCombine(hash, snapshot.Timestamp);
                hash = HashCombine(hash, (long)snapshot.WorldFlags);

                return new StateHash(hash);
            }
        }

        /// <summary>
        /// 在框架元数据哈希上合并指定提供器的一次计算结果；不传提供器时与基础计算一致。
        /// 快照为空时不调用提供器并返回无效值；提供器异常向调用方传播。
        /// </summary>
        public static StateHash ComputeWithBusinessData(
            WorldStateSnapshot snapshot,
            IBusinessHashProvider businessHashProvider = null)
        {
            if (snapshot == null) return StateHash.Invalid;

            unchecked
            {
                ulong hash = (ulong)HASH_SEED;
                hash = HashCombine(hash, (long)snapshot.Version);
                hash = HashCombine(hash, (long)snapshot.Frame);
                hash = HashCombine(hash, snapshot.Timestamp);
                hash = HashCombine(hash, (long)snapshot.WorldFlags);

                if (businessHashProvider != null)
                {
                    ulong businessHash = businessHashProvider.GetAllBusinessEntityHashes();
                    hash = HashCombine(hash, (long)businessHash);
                }

                return new StateHash(hash);
            }
        }

        /// <summary>
        /// 仅比较两个哈希的数值；两个无效值相等时也返回真。
        /// </summary>
        public static bool ValidateHash(StateHash clientHash, StateHash serverHash)
        {
            return clientHash.Value == serverHash.Value;
        }

        private static ulong HashCombine(ulong hash, long value)
        {
            return hash ^ (ulong)value * 0x9E3779B97F4A7C15UL + (hash << 15) + (hash >> 2);
        }

        /// <summary>
        /// 对调用方提供的数据计算独立的 SHA-256 指纹；空引用或空数据返回空数组。
        /// 指纹用于比较，不提供真实性验证。
        /// </summary>
        public static byte[] ComputeFingerprint(byte[] data)
        {
            if (data == null || data.Length == 0) return Array.Empty<byte>();

            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(data);
        }
    }
}
