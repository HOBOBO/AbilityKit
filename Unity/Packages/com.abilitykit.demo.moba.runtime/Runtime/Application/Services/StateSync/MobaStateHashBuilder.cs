using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Rollback;

namespace AbilityKit.Demo.Moba.Services.StateSync
{
    public struct MobaStateHashBuilder
    {
        private uint _hash;

        public MobaStateHashBuilder(uint seed)
        {
            _hash = seed == 0u ? 2166136261u : seed;
        }

        public uint Value => _hash;

        public void AddBool(bool value)
        {
            AddByte(value ? (byte)1 : (byte)0);
        }

        public void AddByte(byte value)
        {
            _hash ^= value;
            _hash *= 16777619u;
        }

        public void AddInt(int value)
        {
            unchecked
            {
                AddUInt((uint)value);
            }
        }

        public void AddLong(long value)
        {
            unchecked
            {
                AddUInt((uint)value);
                AddUInt((uint)(value >> 32));
            }
        }

        public void AddUInt(uint value)
        {
            AddByte((byte)(value & 0xFF));
            AddByte((byte)((value >> 8) & 0xFF));
            AddByte((byte)((value >> 16) & 0xFF));
            AddByte((byte)((value >> 24) & 0xFF));
        }

        public void AddFloat(float value)
        {
            AddInt(BitConverter.SingleToInt32Bits(value));
        }
    }

    /// <summary>
    /// Computes the authoritative state projection shared by snapshot producers and prediction clients.
    /// Instances own their scratch buffer and must not be used concurrently.
    /// </summary>
    public sealed class MobaAuthoritativeStateHashCalculator
    {
        private static readonly Comparison<StateHashEntry> CompareEntriesByActorId =
            (left, right) => left.ActorId.CompareTo(right.ActorId);

        private readonly List<StateHashEntry> _entries = new List<StateHashEntry>(16);

        public uint Compute(bool inGame, AbilityKit.Demo.Moba.Services.MobaActorRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            var hash = new MobaStateHashBuilder(2166136261u);
            hash.AddBool(inGame);

            _entries.Clear();
            try
            {
                foreach (var pair in registry.Entries)
                {
                    var entity = pair.Value;
                    if (entity == null || !entity.hasTransform) continue;

                    var hp = 0f;
                    if (entity.hasAttributeGroup && entity.attributeGroup.Group != null)
                    {
                        hp = entity.attributeGroup.Group.GetValue(MobaAttributeIds.HP);
                    }

                    _entries.Add(new StateHashEntry(pair.Key, entity.transform.Value, hp));
                }

                _entries.Sort(CompareEntriesByActorId);
                hash.AddInt(MobaActorTransformRollbackProvider.DefaultKey);
                hash.AddInt(_entries.Count);

                for (var index = 0; index < _entries.Count; index++)
                {
                    var entry = _entries[index];
                    hash.AddInt(entry.ActorId);
                    hash.AddFloat(entry.X);
                    hash.AddFloat(entry.Y);
                    hash.AddFloat(entry.Z);
                    hash.AddFloat(entry.RotationX);
                    hash.AddFloat(entry.RotationY);
                    hash.AddFloat(entry.RotationZ);
                    hash.AddFloat(entry.RotationW);
                    hash.AddFloat(entry.ScaleX);
                    hash.AddFloat(entry.ScaleY);
                    hash.AddFloat(entry.ScaleZ);
                    hash.AddFloat(entry.Hp);
                }

                return hash.Value;
            }
            finally
            {
                _entries.Clear();
                if (_entries.Capacity > 256)
                {
                    _entries.Capacity = 16;
                }
            }
        }

        private readonly struct StateHashEntry
        {
            public StateHashEntry(
                int actorId,
                in AbilityKit.Core.Mathematics.Transform3 transform,
                float hp)
            {
                ActorId = actorId;
                X = transform.Position.X;
                Y = transform.Position.Y;
                Z = transform.Position.Z;
                RotationX = transform.Rotation.X;
                RotationY = transform.Rotation.Y;
                RotationZ = transform.Rotation.Z;
                RotationW = transform.Rotation.W;
                ScaleX = transform.Scale.X;
                ScaleY = transform.Scale.Y;
                ScaleZ = transform.Scale.Z;
                Hp = hp;
            }

            public int ActorId { get; }
            public float X { get; }
            public float Y { get; }
            public float Z { get; }
            public float RotationX { get; }
            public float RotationY { get; }
            public float RotationZ { get; }
            public float RotationW { get; }
            public float ScaleX { get; }
            public float ScaleY { get; }
            public float ScaleZ { get; }
            public float Hp { get; }
        }
    }
}
