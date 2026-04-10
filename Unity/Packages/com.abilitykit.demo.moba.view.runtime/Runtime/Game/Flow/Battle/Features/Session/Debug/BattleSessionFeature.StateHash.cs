using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private static void AddByte(ref uint h, byte v)
        {
            h ^= v;
            h *= 16777619u;
        }

        private static void AddUInt(ref uint h, uint v)
        {
            AddByte(ref h, (byte)(v & 0xFF));
            AddByte(ref h, (byte)((v >> 8) & 0xFF));
            AddByte(ref h, (byte)((v >> 16) & 0xFF));
            AddByte(ref h, (byte)((v >> 24) & 0xFF));
        }

        private static void AddInt(ref uint h, int v)
        {
            unchecked
            {
                AddUInt(ref h, (uint)v);
            }
        }

        private static void AddFloat(ref uint h, float v)
        {
            var bits = BitConverter.SingleToInt32Bits(v);
            AddInt(ref h, bits);
        }

        private static uint ComputeStateHash(MobaGamePhaseService phase, MobaActorRegistry registry)
        {
            var entries = new List<(int actorId, float x, float y, float z)>(16);
            foreach (var kv in registry.Entries)
            {
                var actorId = kv.Key;
                var e = kv.Value;
                if (e == null) continue;
                if (!e.hasTransform) continue;
                var p = e.transform.Value.Position;
                entries.Add((actorId, p.X, p.Y, p.Z));
            }

            entries.Sort((a, b) => a.actorId.CompareTo(b.actorId));

            uint h = 2166136261u;
            AddByte(ref h, phase != null && phase.InGame ? (byte)1 : (byte)0);
            AddInt(ref h, entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                var it = entries[i];
                AddInt(ref h, it.actorId);
                AddFloat(ref h, it.x);
                AddFloat(ref h, it.y);
                AddFloat(ref h, it.z);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugForceClientHashMismatch)
            {
                h ^= 1u;
            }
#endif

            return h;
        }
    }
}
