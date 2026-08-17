#nullable enable

using System;
using System.IO;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Serialization;
using MemoryPack;

namespace AbilityKit.Protocol.Shooter
{
    public static class ShooterPureStateSnapshotKinds
    {
        public const int FullBaseline = 1;
        public const int Delta = 2;
        public const int LowFrequency = 3;
        public const int VisibilityHint = 4;
    }

    public static class ShooterPureStateEntityLayers
    {
        public const int KeyInteraction = 1;
        public const int Combat = 2;
        public const int Decorative = 3;
    }

    public static class ShooterPureStateDeltaKinds
    {
        public const int None = 0;
        public const int Spawn = 1;
        public const int Despawn = 2;
        public const int Update = 3;
        public const int OwnerChange = 4;
        public const int VisibilityChange = 5;
    }

    public static class ShooterPureStateEntityFlags
    {
        public const byte Alive = 1 << 0;
        public const byte Visible = 1 << 1;
        public const byte PredictedLocal = 1 << 2;
        public const byte LowFrequency = 1 << 3;
    }

    [MemoryPackable]
    public partial struct ShooterPureStateSyncSettings
    {
        [MemoryPackOrder(0)] public int MaxEntityCount;
        [MemoryPackOrder(1)] public int ActiveSyncBudget;
        [MemoryPackOrder(2)] public int BaselineIntervalFrames;
        [MemoryPackOrder(3)] public int DeltaIntervalFrames;
        [MemoryPackOrder(4)] public int LowFrequencyIntervalFrames;
        [MemoryPackOrder(5)] public int InterpolationDelayFrames;

        public ShooterPureStateSyncSettings(
            int maxEntityCount,
            int activeSyncBudget,
            int baselineIntervalFrames,
            int deltaIntervalFrames,
            int lowFrequencyIntervalFrames,
            int interpolationDelayFrames)
        {
            MaxEntityCount = maxEntityCount;
            ActiveSyncBudget = activeSyncBudget;
            BaselineIntervalFrames = baselineIntervalFrames;
            DeltaIntervalFrames = deltaIntervalFrames;
            LowFrequencyIntervalFrames = lowFrequencyIntervalFrames;
            InterpolationDelayFrames = interpolationDelayFrames;
        }

        public static ShooterPureStateSyncSettings Default => new ShooterPureStateSyncSettings(
            10000,
            512,
            60,
            2,
            15,
            3);
    }

    [MemoryPackable]
    public partial struct ShooterPureStateEntityDelta
    {
        [MemoryPackOrder(0)] public int EntityId;
        [MemoryPackOrder(1)] public int EntityKind;
        [MemoryPackOrder(2)] public int EntityLayer;
        [MemoryPackOrder(3)] public int DeltaKind;
        [MemoryPackOrder(4)] public int OwnerId;
        [MemoryPackOrder(5)] public int QuantizedX;
        [MemoryPackOrder(6)] public int QuantizedY;
        [MemoryPackOrder(7)] public int QuantizedVelocityX;
        [MemoryPackOrder(8)] public int QuantizedVelocityY;
        [MemoryPackOrder(9)] public int Hp;
        [MemoryPackOrder(10)] public int Score;
        [MemoryPackOrder(11)] public int RemainingFrames;
        [MemoryPackOrder(12)] public byte Flags;

        public ShooterPureStateEntityDelta(
            int entityId,
            int entityKind,
            int entityLayer,
            int deltaKind,
            int ownerId,
            int quantizedX,
            int quantizedY,
            int quantizedVelocityX,
            int quantizedVelocityY,
            int hp,
            int score,
            int remainingFrames,
            byte flags)
        {
            EntityId = entityId;
            EntityKind = entityKind;
            EntityLayer = entityLayer;
            DeltaKind = deltaKind;
            OwnerId = ownerId;
            QuantizedX = quantizedX;
            QuantizedY = quantizedY;
            QuantizedVelocityX = quantizedVelocityX;
            QuantizedVelocityY = quantizedVelocityY;
            Hp = hp;
            Score = score;
            RemainingFrames = remainingFrames;
            Flags = flags;
        }
    }

    [MemoryPackable]
    public partial struct ShooterPureStateVisibilityHint
    {
        [MemoryPackOrder(0)] public int EntityId;
        [MemoryPackOrder(1)] public int EntityKind;
        [MemoryPackOrder(2)] public int EntityLayer;
        [MemoryPackOrder(3)] public byte Flags;
        [MemoryPackOrder(4)] public int Priority;

        public ShooterPureStateVisibilityHint(int entityId, int entityKind, int entityLayer, byte flags, int priority)
        {
            EntityId = entityId;
            EntityKind = entityKind;
            EntityLayer = entityLayer;
            Flags = flags;
            Priority = priority;
        }
    }

    [MemoryPackable]
    public partial struct ShooterPureStateSnapshotPayload
    {
        [MemoryPackOrder(0)] public int Version;
        [MemoryPackOrder(1)] public ulong WorldId;
        [MemoryPackOrder(2)] public int Frame;
        [MemoryPackOrder(3)] public long ServerTick;
        [MemoryPackOrder(4)] public int SnapshotKind;
        [MemoryPackOrder(5)] public int BaselineFrame;
        [MemoryPackOrder(6)] public uint BaselineHash;
        [MemoryPackOrder(7)] public uint StateHash;
        [MemoryPackOrder(8)] public ShooterPureStateSyncSettings Settings;
        [MemoryPackOrder(9)] public ShooterPureStateEntityDelta[] Entities;
        [MemoryPackOrder(10)] public ShooterPureStateVisibilityHint[] VisibilityHints;
        [MemoryPackOrder(11)] public ShooterCommandAcknowledgement[] AcknowledgedCommands;
        [MemoryPackIgnore] private int _entityCount;
        [MemoryPackIgnore] private int _visibilityHintCount;
        [MemoryPackIgnore] private int _acknowledgedCommandCount;

        [MemoryPackConstructor]
        public ShooterPureStateSnapshotPayload(
            int version,
            ulong worldId,
            int frame,
            long serverTick,
            int snapshotKind,
            int baselineFrame,
            uint baselineHash,
            uint stateHash,
            ShooterPureStateSyncSettings settings,
            ShooterPureStateEntityDelta[] entities,
            ShooterPureStateVisibilityHint[] visibilityHints,
            ShooterCommandAcknowledgement[]? acknowledgedCommands = null)
        {
            Version = version;
            WorldId = worldId;
            Frame = frame;
            ServerTick = serverTick;
            SnapshotKind = snapshotKind;
            BaselineFrame = baselineFrame;
            BaselineHash = baselineHash;
            StateHash = stateHash;
            Settings = settings;
            Entities = entities ?? Array.Empty<ShooterPureStateEntityDelta>();
            VisibilityHints = visibilityHints ?? Array.Empty<ShooterPureStateVisibilityHint>();
            AcknowledgedCommands = acknowledgedCommands ?? Array.Empty<ShooterCommandAcknowledgement>();
            _entityCount = Entities.Length;
            _visibilityHintCount = VisibilityHints.Length;
            _acknowledgedCommandCount = AcknowledgedCommands.Length;
        }

        [MemoryPackIgnore]
        public int EffectiveEntityCount => ClampCount(_entityCount, Entities);

        [MemoryPackIgnore]
        public int EffectiveVisibilityHintCount => ClampCount(_visibilityHintCount, VisibilityHints);

        [MemoryPackIgnore]
        public int EffectiveAcknowledgedCommandCount => ClampCount(_acknowledgedCommandCount, AcknowledgedCommands);

        /// <summary>Sets the valid prefixes for capacity-backed transient arrays.</summary>
        public void SetTransientCounts(int entityCount, int visibilityHintCount, int acknowledgedCommandCount = -1)
        {
            _entityCount = ClampCount(entityCount, Entities);
            _visibilityHintCount = ClampCount(visibilityHintCount, VisibilityHints);
            _acknowledgedCommandCount = acknowledgedCommandCount < 0
                ? (AcknowledgedCommands?.Length ?? 0)
                : ClampCount(acknowledgedCommandCount, AcknowledgedCommands);
        }

        private static int ClampCount<T>(int count, T[]? values)
        {
            if (values == null || values.Length == 0)
            {
                return 0;
            }

            return Math.Max(0, Math.Min(count, values.Length));
        }

        public static ShooterPureStateSnapshotPayload Empty(int frame = 0)
        {
            return new ShooterPureStateSnapshotPayload(
                ShooterPureStateSyncCodec.CurrentVersion,
                0,
                frame,
                0,
                ShooterPureStateSnapshotKinds.FullBaseline,
                0,
                0,
                0,
                ShooterPureStateSyncSettings.Default,
                Array.Empty<ShooterPureStateEntityDelta>(),
                Array.Empty<ShooterPureStateVisibilityHint>());
        }
    }

    [MemoryPackable]
    internal partial struct ShooterLegacyPureStateSnapshotPayload
    {
        [MemoryPackOrder(0)] public int Version;
        [MemoryPackOrder(1)] public ulong WorldId;
        [MemoryPackOrder(2)] public int Frame;
        [MemoryPackOrder(3)] public long ServerTick;
        [MemoryPackOrder(4)] public int SnapshotKind;
        [MemoryPackOrder(5)] public int BaselineFrame;
        [MemoryPackOrder(6)] public uint BaselineHash;
        [MemoryPackOrder(7)] public uint StateHash;
        [MemoryPackOrder(8)] public ShooterPureStateSyncSettings Settings;
        [MemoryPackOrder(9)] public ShooterPureStateEntityDelta[] Entities;
        [MemoryPackOrder(10)] public ShooterPureStateVisibilityHint[] VisibilityHints;
    }

    [MemoryPackable]
    internal partial class ShooterReusablePureStateSnapshotPayload
    {
        [MemoryPackOrder(0)] public int Version;
        [MemoryPackOrder(1)] public ulong WorldId;
        [MemoryPackOrder(2)] public int Frame;
        [MemoryPackOrder(3)] public long ServerTick;
        [MemoryPackOrder(4)] public int SnapshotKind;
        [MemoryPackOrder(5)] public int BaselineFrame;
        [MemoryPackOrder(6)] public uint BaselineHash;
        [MemoryPackOrder(7)] public uint StateHash;
        [MemoryPackOrder(8)] public ShooterPureStateSyncSettings Settings;
        [MemoryPackOrder(9)] public ShooterPureStateEntityDelta[] Entities = Array.Empty<ShooterPureStateEntityDelta>();
        [MemoryPackOrder(10)] public ShooterPureStateVisibilityHint[] VisibilityHints = Array.Empty<ShooterPureStateVisibilityHint>();
        [MemoryPackOrder(11)] public ShooterCommandAcknowledgement[] AcknowledgedCommands = Array.Empty<ShooterCommandAcknowledgement>();
    }

    public sealed class ShooterPureStateSyncDecodeBuffer
    {
        private ShooterReusablePureStateSnapshotPayload? _buffer = new ShooterReusablePureStateSnapshotPayload();

        public ShooterPureStateSnapshotPayload Decode(ReadOnlySpan<byte> payload)
        {
            if (payload.Length == 0)
            {
                return ShooterPureStateSnapshotPayload.Empty();
            }

            try
            {
                MemoryPackSerializer.Deserialize(payload, ref _buffer);
                var value = _buffer ?? new ShooterReusablePureStateSnapshotPayload();
                _buffer = value;
                return new ShooterPureStateSnapshotPayload(
                    value.Version,
                    value.WorldId,
                    value.Frame,
                    value.ServerTick,
                    value.SnapshotKind <= 0 ? ShooterPureStateSnapshotKinds.FullBaseline : value.SnapshotKind,
                    value.BaselineFrame,
                    value.BaselineHash,
                    value.StateHash,
                    value.Settings.MaxEntityCount <= 0 ? ShooterPureStateSyncSettings.Default : value.Settings,
                    value.Entities ?? Array.Empty<ShooterPureStateEntityDelta>(),
                    value.VisibilityHints ?? Array.Empty<ShooterPureStateVisibilityHint>(),
                    value.AcknowledgedCommands ?? Array.Empty<ShooterCommandAcknowledgement>());
            }
            catch (EndOfStreamException)
            {
                var legacy = MemoryPackSerializer.Deserialize<ShooterLegacyPureStateSnapshotPayload>(payload);
                return new ShooterPureStateSnapshotPayload(
                    legacy.Version,
                    legacy.WorldId,
                    legacy.Frame,
                    legacy.ServerTick,
                    legacy.SnapshotKind <= 0 ? ShooterPureStateSnapshotKinds.FullBaseline : legacy.SnapshotKind,
                    legacy.BaselineFrame,
                    legacy.BaselineHash,
                    legacy.StateHash,
                    legacy.Settings.MaxEntityCount <= 0 ? ShooterPureStateSyncSettings.Default : legacy.Settings,
                    legacy.Entities ?? Array.Empty<ShooterPureStateEntityDelta>(),
                    legacy.VisibilityHints ?? Array.Empty<ShooterPureStateVisibilityHint>());
            }
        }
    }

    public static class ShooterPureStateSyncCodec
    {
        public const int CurrentVersion = 1;

        public static byte[] Serialize(in ShooterPureStateSnapshotPayload snapshot)
        {
#if UNITY_5_3_OR_NEWER
            var compatibleSnapshot = CreateUnityCompatibleSnapshot(in snapshot);
            return MemoryPackSerializer.Serialize(compatibleSnapshot);
#else
            return MemoryPackSerializer.Serialize(new ShooterPureStateSnapshotTransient(in snapshot));
#endif
        }

        /// <summary>
        /// Serializes into a reusable exact-length array. Consume the returned payload before the
        /// next serialization on <paramref name="buffer"/>.
        /// </summary>
        public static byte[] SerializeTransient(
            in ShooterPureStateSnapshotPayload snapshot,
            ReusableMemoryPackSerializationBuffer buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
#if UNITY_5_3_OR_NEWER
            var compatibleSnapshot = CreateUnityCompatibleSnapshot(in snapshot);
            return buffer.SerializeTransient(in compatibleSnapshot);
#else
            return buffer.SerializeTransient(new ShooterPureStateSnapshotTransient(in snapshot));
#endif
        }

        /// <summary>
        /// Serializes into a reusable backing buffer without an exact-length payload copy.
        /// Consume the returned segment before the next serialization on <paramref name="buffer"/>.
        /// </summary>
        public static ArraySegment<byte> SerializeTransientSegment(
            in ShooterPureStateSnapshotPayload snapshot,
            ReusableMemoryPackSerializationBuffer buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
#if UNITY_5_3_OR_NEWER
            var compatibleSnapshot = CreateUnityCompatibleSnapshot(in snapshot);
            return buffer.SerializeTransientSegment(in compatibleSnapshot);
#else
            return buffer.SerializeTransientSegment(new ShooterPureStateSnapshotTransient(in snapshot));
#endif
        }

#if UNITY_5_3_OR_NEWER
        private static ShooterPureStateSnapshotPayload CreateUnityCompatibleSnapshot(
            in ShooterPureStateSnapshotPayload snapshot)
        {
            return new ShooterPureStateSnapshotPayload(
                snapshot.Version,
                snapshot.WorldId,
                snapshot.Frame,
                snapshot.ServerTick,
                snapshot.SnapshotKind,
                snapshot.BaselineFrame,
                snapshot.BaselineHash,
                snapshot.StateHash,
                snapshot.Settings,
                CopyPrefix(snapshot.Entities, snapshot.EffectiveEntityCount),
                CopyPrefix(snapshot.VisibilityHints, snapshot.EffectiveVisibilityHintCount),
                CopyPrefix(snapshot.AcknowledgedCommands, snapshot.EffectiveAcknowledgedCommandCount));
        }

        private static T[] CopyPrefix<T>(T[]? values, int count)
        {
            if (values == null || count <= 0)
            {
                return Array.Empty<T>();
            }

            count = Math.Min(count, values.Length);
            if (count == values.Length)
            {
                return values;
            }

            var copy = new T[count];
            Array.Copy(values, copy, count);
            return copy;
        }
#endif

        public static ShooterPureStateSnapshotPayload Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return ShooterPureStateSnapshotPayload.Empty();
            }

            try
            {
                var value = MemoryPackSerializer.Deserialize<ShooterPureStateSnapshotPayload>(payload);
                NormalizeDeserializedPayload(ref value);
                return value;
            }
            catch (EndOfStreamException)
            {
                var legacy = MemoryPackSerializer.Deserialize<ShooterLegacyPureStateSnapshotPayload>(payload);
                return new ShooterPureStateSnapshotPayload(
                    legacy.Version,
                    legacy.WorldId,
                    legacy.Frame,
                    legacy.ServerTick,
                    legacy.SnapshotKind <= 0 ? ShooterPureStateSnapshotKinds.FullBaseline : legacy.SnapshotKind,
                    legacy.BaselineFrame,
                    legacy.BaselineHash,
                    legacy.StateHash,
                    legacy.Settings.MaxEntityCount <= 0 ? ShooterPureStateSyncSettings.Default : legacy.Settings,
                    legacy.Entities ?? Array.Empty<ShooterPureStateEntityDelta>(),
                    legacy.VisibilityHints ?? Array.Empty<ShooterPureStateVisibilityHint>());
            }
        }

        private static void NormalizeDeserializedPayload(ref ShooterPureStateSnapshotPayload value)
        {
            if (value.SnapshotKind <= 0)
            {
                value.SnapshotKind = ShooterPureStateSnapshotKinds.FullBaseline;
            }

            if (value.Settings.MaxEntityCount <= 0)
            {
                value.Settings = ShooterPureStateSyncSettings.Default;
            }

            value.Entities ??= Array.Empty<ShooterPureStateEntityDelta>();
            value.VisibilityHints ??= Array.Empty<ShooterPureStateVisibilityHint>();
            value.AcknowledgedCommands ??= Array.Empty<ShooterCommandAcknowledgement>();
            value.SetTransientCounts(
                value.Entities.Length,
                value.VisibilityHints.Length,
                value.AcknowledgedCommands.Length);
        }
    }

#if !UNITY_5_3_OR_NEWER
    internal readonly struct ShooterPureStateSnapshotTransient : IMemoryPackable<ShooterPureStateSnapshotTransient>
    {
        private readonly ShooterPureStateSnapshotPayload _snapshot;

        public ShooterPureStateSnapshotTransient(in ShooterPureStateSnapshotPayload snapshot)
        {
            _snapshot = snapshot;
        }

        public static void RegisterFormatter()
        {
            if (!MemoryPackFormatterProvider.IsRegistered<ShooterPureStateSnapshotTransient>())
            {
                MemoryPackFormatterProvider.Register(
                    new MemoryPack.Formatters.MemoryPackableFormatter<ShooterPureStateSnapshotTransient>());
            }
        }

        static void IMemoryPackable<ShooterPureStateSnapshotTransient>.Serialize<TBufferWriter>(
            ref MemoryPackWriter<TBufferWriter> writer,
            scoped ref ShooterPureStateSnapshotTransient value)
        {
            ref readonly var snapshot = ref value._snapshot;
            writer.WriteUnmanagedWithObjectHeader(
                12,
                snapshot.Version,
                snapshot.WorldId,
                snapshot.Frame,
                snapshot.ServerTick,
                snapshot.SnapshotKind,
                snapshot.BaselineFrame,
                snapshot.BaselineHash,
                snapshot.StateHash,
                snapshot.Settings);
            writer.WriteUnmanagedSpan((snapshot.Entities ?? Array.Empty<ShooterPureStateEntityDelta>()).AsSpan(0, snapshot.EffectiveEntityCount));
            writer.WriteUnmanagedSpan((snapshot.VisibilityHints ?? Array.Empty<ShooterPureStateVisibilityHint>()).AsSpan(0, snapshot.EffectiveVisibilityHintCount));
            writer.WriteUnmanagedSpan((snapshot.AcknowledgedCommands ?? Array.Empty<ShooterCommandAcknowledgement>()).AsSpan(0, snapshot.EffectiveAcknowledgedCommandCount));
        }

        static void IMemoryPackable<ShooterPureStateSnapshotTransient>.Deserialize(
            ref MemoryPackReader reader,
            scoped ref ShooterPureStateSnapshotTransient value)
        {
            throw new NotSupportedException("Transient pure-state views are serialization-only.");
        }
    }
#endif
}
