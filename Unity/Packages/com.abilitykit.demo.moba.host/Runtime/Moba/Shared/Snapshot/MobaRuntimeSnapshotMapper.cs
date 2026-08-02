using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;

namespace AbilityKit.Ability.Host.Extensions.Moba.Snapshot
{
    public readonly struct MobaRuntimeSnapshotContext
    {
        public readonly int FrameIndex;
        public readonly double Timestamp;

        public MobaRuntimeSnapshotContext(int frameIndex, double timestamp)
        {
            FrameIndex = frameIndex;
            Timestamp = timestamp;
        }
    }

    public interface IMobaRuntimeSnapshotMapper<TOutput>
    {
        int OpCode { get; }

        bool TryMap(in WorldStateSnapshot snapshot, in MobaRuntimeSnapshotContext context, out TOutput output);
    }

    public sealed class MobaRuntimeSnapshotMapperRegistry<TOutput>
    {
        private readonly Dictionary<int, IMobaRuntimeSnapshotMapper<TOutput>> _mappers = new Dictionary<int, IMobaRuntimeSnapshotMapper<TOutput>>();

        public int Count => _mappers.Count;

        public bool TryRegister(IMobaRuntimeSnapshotMapper<TOutput> mapper)
        {
            if (mapper == null || mapper.OpCode == 0)
            {
                return false;
            }

            _mappers[mapper.OpCode] = mapper;
            return true;
        }

        public bool TryMap(in WorldStateSnapshot snapshot, in MobaRuntimeSnapshotContext context, out TOutput output)
        {
            if (!_mappers.TryGetValue(snapshot.OpCode, out var mapper))
            {
                output = default;
                return false;
            }

            return mapper.TryMap(in snapshot, in context, out output);
        }
    }

    public static class MobaRuntimeSnapshotMapperRegistryBuilder
    {
        public static MobaRuntimeSnapshotMapperRegistry<TOutput> FromMappers<TOutput>(params IMobaRuntimeSnapshotMapper<TOutput>[] mappers)
        {
            var registry = new MobaRuntimeSnapshotMapperRegistry<TOutput>();
            if (mappers == null || mappers.Length == 0)
            {
                return registry;
            }

            for (int i = 0; i < mappers.Length; i++)
            {
                registry.TryRegister(mappers[i]);
            }

            return registry;
        }

        public static MobaRuntimeSnapshotMapperRegistry<TOutput> FromTypes<TOutput>(IEnumerable<Type> mapperTypes, Action<string> warning = null)
        {
            var registry = new MobaRuntimeSnapshotMapperRegistry<TOutput>();
            if (mapperTypes == null)
            {
                return registry;
            }

            var mapperContract = typeof(IMobaRuntimeSnapshotMapper<TOutput>);
            foreach (var type in mapperTypes)
            {
                if (type == null || type.IsAbstract || type.IsInterface || !mapperContract.IsAssignableFrom(type))
                {
                    continue;
                }

                try
                {
                    if (Activator.CreateInstance(type) is IMobaRuntimeSnapshotMapper<TOutput> mapper)
                    {
                        registry.TryRegister(mapper);
                    }
                }
                catch (Exception ex)
                {
                    warning?.Invoke($"Failed to create snapshot mapper {type.FullName}: {ex.Message}");
                }
            }

            return registry;
        }
    }
}
