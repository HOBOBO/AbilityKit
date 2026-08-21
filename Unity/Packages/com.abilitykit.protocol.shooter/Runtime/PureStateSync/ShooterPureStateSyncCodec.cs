#nullable enable

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        [MemoryPackOrder(6)] public int NearLodIntervalFrames;
        [MemoryPackOrder(7)] public int MidLodIntervalFrames;
        [MemoryPackOrder(8)] public int FarLodIntervalFrames;

        public ShooterPureStateSyncSettings(
            int maxEntityCount,
            int activeSyncBudget,
            int baselineIntervalFrames,
            int deltaIntervalFrames,
            int lowFrequencyIntervalFrames,
            int interpolationDelayFrames,
            int nearLodIntervalFrames = 1,
            int midLodIntervalFrames = 1,
            int farLodIntervalFrames = 1)
        {
            MaxEntityCount = maxEntityCount;
            ActiveSyncBudget = activeSyncBudget;
            BaselineIntervalFrames = baselineIntervalFrames;
            DeltaIntervalFrames = deltaIntervalFrames;
            LowFrequencyIntervalFrames = lowFrequencyIntervalFrames;
            InterpolationDelayFrames = interpolationDelayFrames;
            NearLodIntervalFrames = nearLodIntervalFrames;
            MidLodIntervalFrames = midLodIntervalFrames;
            FarLodIntervalFrames = farLodIntervalFrames;
        }

        public static ShooterPureStateSyncSettings Default => new ShooterPureStateSyncSettings(
            10000,
            512,
            60,
            2,
            15,
            3,
            1,
            1,
            1);
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
    public partial struct ShooterPureStateFrameSample
    {
        [MemoryPackOrder(0)] public int Frame;
        [MemoryPackOrder(1)] public long ServerTick;
        [MemoryPackOrder(2)] public int TransformOffset;
        [MemoryPackOrder(3)] public int TransformCount;

        public ShooterPureStateFrameSample(int frame, long serverTick, int transformOffset, int transformCount)
        {
            Frame = frame;
            ServerTick = serverTick;
            TransformOffset = transformOffset;
            TransformCount = transformCount;
        }
    }

    [MemoryPackable]
    public partial struct ShooterPureStateTransformSample
    {
        [MemoryPackOrder(0)] public int EntityId;
        [MemoryPackOrder(1)] public int EntityKind;
        [MemoryPackOrder(2)] public int QuantizedX;
        [MemoryPackOrder(3)] public int QuantizedY;
        [MemoryPackOrder(4)] public int QuantizedVelocityX;
        [MemoryPackOrder(5)] public int QuantizedVelocityY;
        [MemoryPackOrder(6)] public byte Flags;

        public ShooterPureStateTransformSample(
            int entityId,
            int entityKind,
            int quantizedX,
            int quantizedY,
            int quantizedVelocityX,
            int quantizedVelocityY,
            byte flags)
        {
            EntityId = entityId;
            EntityKind = entityKind;
            QuantizedX = quantizedX;
            QuantizedY = quantizedY;
            QuantizedVelocityX = quantizedVelocityX;
            QuantizedVelocityY = quantizedVelocityY;
            Flags = flags;
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
        [MemoryPackOrder(12)] public ShooterPureStateFrameSample[] FrameSamples;
        [MemoryPackOrder(13)] public ShooterPureStateTransformSample[] TransformSamples;
        [MemoryPackIgnore] private int _entityCount;
        [MemoryPackIgnore] private int _visibilityHintCount;
        [MemoryPackIgnore] private int _acknowledgedCommandCount;
        [MemoryPackIgnore] private int _frameSampleCount;
        [MemoryPackIgnore] private int _transformSampleCount;

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
            ShooterCommandAcknowledgement[]? acknowledgedCommands = null,
            ShooterPureStateFrameSample[]? frameSamples = null,
            ShooterPureStateTransformSample[]? transformSamples = null)
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
            FrameSamples = frameSamples ?? Array.Empty<ShooterPureStateFrameSample>();
            TransformSamples = transformSamples ?? Array.Empty<ShooterPureStateTransformSample>();
            _entityCount = Entities.Length;
            _visibilityHintCount = VisibilityHints.Length;
            _acknowledgedCommandCount = AcknowledgedCommands.Length;
            _frameSampleCount = FrameSamples.Length;
            _transformSampleCount = TransformSamples.Length;
        }

        [MemoryPackIgnore]
        public int EffectiveEntityCount => ClampCount(_entityCount, Entities);

        [MemoryPackIgnore]
        public int EffectiveVisibilityHintCount => ClampCount(_visibilityHintCount, VisibilityHints);

        [MemoryPackIgnore]
        public int EffectiveAcknowledgedCommandCount => ClampCount(_acknowledgedCommandCount, AcknowledgedCommands);

        [MemoryPackIgnore]
        public int EffectiveFrameSampleCount => ClampCount(_frameSampleCount, FrameSamples);

        [MemoryPackIgnore]
        public int EffectiveTransformSampleCount => ClampCount(_transformSampleCount, TransformSamples);

        /// <summary>Sets the valid prefixes for capacity-backed transient arrays.</summary>
        public void SetTransientCounts(
            int entityCount,
            int visibilityHintCount,
            int acknowledgedCommandCount = -1,
            int frameSampleCount = -1,
            int transformSampleCount = -1)
        {
            _entityCount = ClampCount(entityCount, Entities);
            _visibilityHintCount = ClampCount(visibilityHintCount, VisibilityHints);
            _acknowledgedCommandCount = acknowledgedCommandCount < 0
                ? (AcknowledgedCommands?.Length ?? 0)
                : ClampCount(acknowledgedCommandCount, AcknowledgedCommands);
            _frameSampleCount = frameSampleCount < 0
                ? (FrameSamples?.Length ?? 0)
                : ClampCount(frameSampleCount, FrameSamples);
            _transformSampleCount = transformSampleCount < 0
                ? (TransformSamples?.Length ?? 0)
                : ClampCount(transformSampleCount, TransformSamples);
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
    internal partial struct ShooterLegacyPureStateSyncSettings
    {
        [MemoryPackOrder(0)] public int MaxEntityCount;
        [MemoryPackOrder(1)] public int ActiveSyncBudget;
        [MemoryPackOrder(2)] public int BaselineIntervalFrames;
        [MemoryPackOrder(3)] public int DeltaIntervalFrames;
        [MemoryPackOrder(4)] public int LowFrequencyIntervalFrames;
        [MemoryPackOrder(5)] public int InterpolationDelayFrames;

        public ShooterPureStateSyncSettings ToCurrent()
        {
            return new ShooterPureStateSyncSettings(
                MaxEntityCount,
                ActiveSyncBudget,
                BaselineIntervalFrames,
                DeltaIntervalFrames,
                LowFrequencyIntervalFrames,
                InterpolationDelayFrames);
        }
    }

    [MemoryPackable]
    internal partial struct ShooterLegacyAcknowledgedPureStateSnapshotPayload
    {
        [MemoryPackOrder(0)] public int Version;
        [MemoryPackOrder(1)] public ulong WorldId;
        [MemoryPackOrder(2)] public int Frame;
        [MemoryPackOrder(3)] public long ServerTick;
        [MemoryPackOrder(4)] public int SnapshotKind;
        [MemoryPackOrder(5)] public int BaselineFrame;
        [MemoryPackOrder(6)] public uint BaselineHash;
        [MemoryPackOrder(7)] public uint StateHash;
        [MemoryPackOrder(8)] public ShooterLegacyPureStateSyncSettings Settings;
        [MemoryPackOrder(9)] public ShooterPureStateEntityDelta[] Entities;
        [MemoryPackOrder(10)] public ShooterPureStateVisibilityHint[] VisibilityHints;
        [MemoryPackOrder(11)] public ShooterCommandAcknowledgement[] AcknowledgedCommands;
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
        [MemoryPackOrder(12)] public ShooterPureStateFrameSample[] FrameSamples = Array.Empty<ShooterPureStateFrameSample>();
        [MemoryPackOrder(13)] public ShooterPureStateTransformSample[] TransformSamples = Array.Empty<ShooterPureStateTransformSample>();
    }

    public sealed class ShooterPureStateSyncDecodeBuffer
    {
        private ShooterPureStateEntityDelta[] _entities = Array.Empty<ShooterPureStateEntityDelta>();
        private ShooterPureStateVisibilityHint[] _visibilityHints = Array.Empty<ShooterPureStateVisibilityHint>();
        private ShooterCommandAcknowledgement[] _acknowledgedCommands = Array.Empty<ShooterCommandAcknowledgement>();
        private ShooterPureStateFrameSample[] _frameSamples = Array.Empty<ShooterPureStateFrameSample>();
        private ShooterPureStateTransformSample[] _transformSamples = Array.Empty<ShooterPureStateTransformSample>();

        public ShooterPureStateSnapshotPayload Decode(ReadOnlySpan<byte> payload)
        {
            if (payload.Length == 0)
            {
                return ShooterPureStateSnapshotPayload.Empty();
            }

            var memberCount = ShooterPureStateSyncCodec.ReadObjectMemberCount(payload);
            if (memberCount == 14)
            {
                return DecodeCurrent(payload);
            }

            if (memberCount == 12)
            {
                try
                {
                    return DecodeCurrent(payload);
                }
                catch (Exception exception) when (IsLegacyPayloadCandidate(exception))
                {
                    return DecodeLegacy(payload);
                }
            }

            if (memberCount == 11)
            {
                return DecodeLegacy(payload);
            }

            throw new MemoryPackSerializationException("Unexpected pure-state snapshot member count.");
        }

        private ShooterPureStateSnapshotPayload DecodeCurrent(ReadOnlySpan<byte> payload)
        {
            using var state = MemoryPackReaderOptionalStatePool.Rent(MemoryPackSerializerOptions.Default);
            var reader = new MemoryPackReader(payload, state);
            try
            {
                if (!reader.TryReadObjectHeader(out var memberCount) || (memberCount != 12 && memberCount != 14))
                {
                    throw new EndOfStreamException("Unexpected pure-state snapshot member count.");
                }

                reader.ReadUnmanaged(
                    out int version,
                    out ulong worldId,
                    out int frame,
                    out long serverTick,
                    out int snapshotKind,
                    out int baselineFrame,
                    out uint baselineHash,
                    out uint stateHash,
                    out ShooterPureStateSyncSettings settings);
                ReadUnmanagedPrefix(ref reader, ref _entities, out var entityCount);
                ReadUnmanagedPrefix(ref reader, ref _visibilityHints, out var visibilityHintCount);
                ReadUnmanagedPrefix(ref reader, ref _acknowledgedCommands, out var acknowledgedCommandCount);
                var frameSampleCount = 0;
                var transformSampleCount = 0;
                if (memberCount == 14)
                {
                    ReadUnmanagedPrefix(ref reader, ref _frameSamples, out frameSampleCount);
                    ReadUnmanagedPrefix(ref reader, ref _transformSamples, out transformSampleCount);
                    ValidateFrameSamples(_frameSamples, frameSampleCount, transformSampleCount, frame);
                }
                if (reader.Remaining != 0)
                {
                    throw new EndOfStreamException("Unexpected trailing pure-state snapshot data.");
                }

                var value = new ShooterPureStateSnapshotPayload(
                    version,
                    worldId,
                    frame,
                    serverTick,
                    snapshotKind <= 0 ? ShooterPureStateSnapshotKinds.FullBaseline : snapshotKind,
                    baselineFrame,
                    baselineHash,
                    stateHash,
                    settings.MaxEntityCount <= 0 ? ShooterPureStateSyncSettings.Default : settings,
                    _entities,
                    _visibilityHints,
                    _acknowledgedCommands,
                    _frameSamples,
                    _transformSamples);
                value.SetTransientCounts(
                    entityCount,
                    visibilityHintCount,
                    acknowledgedCommandCount,
                    frameSampleCount,
                    transformSampleCount);
                return value;
            }
            finally
            {
                reader.Dispose();
            }
        }

        private static void ReadUnmanagedPrefix<T>(
            ref MemoryPackReader reader,
            ref T[] buffer,
            out int count)
            where T : unmanaged
        {
            if (!reader.TryReadCollectionHeader(out count) || count <= 0)
            {
                count = 0;
                return;
            }

            var byteCount = checked(count * Unsafe.SizeOf<T>());
            if (byteCount > reader.Remaining)
            {
                throw new EndOfStreamException("Unmanaged collection exceeds the remaining payload.");
            }

            EnsureCapacity(ref buffer, count);
            var destination = MemoryMarshal.AsBytes(buffer.AsSpan(0, count));
            var source = MemoryMarshal.CreateReadOnlySpan(ref reader.GetSpanReference(byteCount), byteCount);
            source.CopyTo(destination);
            reader.Advance(byteCount);
        }

        private static void EnsureCapacity<T>(ref T[] buffer, int count)
        {
            if (buffer.Length >= count) return;
            var capacity = Math.Max(4, buffer.Length);
            while (capacity < count)
            {
                capacity = checked(capacity * 2);
            }

            buffer = new T[capacity];
        }

        internal static void ValidateFrameSamples(
            ShooterPureStateFrameSample[] samples,
            int sampleCount,
            int transformCount,
            int authoritativeFrame)
        {
            var previousFrame = int.MinValue;
            var previousEnd = 0;
            for (var i = 0; i < sampleCount; i++)
            {
                ref readonly var sample = ref samples[i];
                if (sample.Frame <= previousFrame || sample.Frame > authoritativeFrame ||
                    sample.TransformOffset < previousEnd || sample.TransformCount < 0 ||
                    sample.TransformOffset > transformCount - sample.TransformCount)
                {
                    throw new MemoryPackSerializationException("Invalid pure-state frame sample layout.");
                }

                previousFrame = sample.Frame;
                previousEnd = sample.TransformOffset + sample.TransformCount;
            }
        }

        private static ShooterPureStateSnapshotPayload DecodeLegacy(ReadOnlySpan<byte> payload)
        {
            try
            {
                var legacy = MemoryPackSerializer.Deserialize<ShooterLegacyAcknowledgedPureStateSnapshotPayload>(payload);
                var settings = legacy.Settings.ToCurrent();
                return new ShooterPureStateSnapshotPayload(
                    legacy.Version,
                    legacy.WorldId,
                    legacy.Frame,
                    legacy.ServerTick,
                    legacy.SnapshotKind <= 0 ? ShooterPureStateSnapshotKinds.FullBaseline : legacy.SnapshotKind,
                    legacy.BaselineFrame,
                    legacy.BaselineHash,
                    legacy.StateHash,
                    settings.MaxEntityCount <= 0 ? ShooterPureStateSyncSettings.Default : settings,
                    legacy.Entities ?? Array.Empty<ShooterPureStateEntityDelta>(),
                    legacy.VisibilityHints ?? Array.Empty<ShooterPureStateVisibilityHint>(),
                    legacy.AcknowledgedCommands ?? Array.Empty<ShooterCommandAcknowledgement>());
            }
            catch (Exception exception) when (IsLegacyPayloadCandidate(exception))
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

        private static bool IsLegacyPayloadCandidate(Exception exception)
        {
            return exception is EndOfStreamException || exception is MemoryPackSerializationException;
        }
    }

    public static class ShooterPureStateSyncCodec
    {
        public const int CurrentVersion = 2;

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
                CopyPrefix(snapshot.AcknowledgedCommands, snapshot.EffectiveAcknowledgedCommandCount),
                CopyPrefix(snapshot.FrameSamples, snapshot.EffectiveFrameSampleCount),
                CopyPrefix(snapshot.TransformSamples, snapshot.EffectiveTransformSampleCount));
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

            var memberCount = ReadObjectMemberCount(payload);
            if (memberCount == 14)
            {
                var value = MemoryPackSerializer.Deserialize<ShooterPureStateSnapshotPayload>(payload);
                NormalizeDeserializedPayload(ref value);
                return value;
            }

            if (memberCount == 12)
            {
                try
                {
                    var value = MemoryPackSerializer.Deserialize<ShooterPureStateSnapshotPayload>(payload);
                    NormalizeDeserializedPayload(ref value);
                    return value;
                }
                catch (Exception exception) when (IsLegacyPayloadCandidate(exception))
                {
                    return DeserializeLegacy(payload);
                }
            }

            if (memberCount == 11)
            {
                return DeserializeLegacy(payload);
            }

            throw new MemoryPackSerializationException("Unexpected pure-state snapshot member count.");
        }

        internal static int ReadObjectMemberCount(ReadOnlySpan<byte> payload)
        {
            using var state = MemoryPackReaderOptionalStatePool.Rent(MemoryPackSerializerOptions.Default);
            var reader = new MemoryPackReader(payload, state);
            try
            {
                return reader.TryReadObjectHeader(out var memberCount) ? memberCount : -1;
            }
            finally
            {
                reader.Dispose();
            }
        }

        private static ShooterPureStateSnapshotPayload DeserializeLegacy(byte[] payload)
        {
            try
            {
                var legacy = MemoryPackSerializer.Deserialize<ShooterLegacyAcknowledgedPureStateSnapshotPayload>(payload);
                var settings = legacy.Settings.ToCurrent();
                return new ShooterPureStateSnapshotPayload(
                    legacy.Version,
                    legacy.WorldId,
                    legacy.Frame,
                    legacy.ServerTick,
                    legacy.SnapshotKind <= 0 ? ShooterPureStateSnapshotKinds.FullBaseline : legacy.SnapshotKind,
                    legacy.BaselineFrame,
                    legacy.BaselineHash,
                    legacy.StateHash,
                    settings.MaxEntityCount <= 0 ? ShooterPureStateSyncSettings.Default : settings,
                    legacy.Entities ?? Array.Empty<ShooterPureStateEntityDelta>(),
                    legacy.VisibilityHints ?? Array.Empty<ShooterPureStateVisibilityHint>(),
                    legacy.AcknowledgedCommands ?? Array.Empty<ShooterCommandAcknowledgement>());
            }
            catch (Exception exception) when (IsLegacyPayloadCandidate(exception))
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

        private static bool IsLegacyPayloadCandidate(Exception exception)
        {
            return exception is EndOfStreamException || exception is MemoryPackSerializationException;
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
            value.FrameSamples ??= Array.Empty<ShooterPureStateFrameSample>();
            value.TransformSamples ??= Array.Empty<ShooterPureStateTransformSample>();
            value.SetTransientCounts(
                value.Entities.Length,
                value.VisibilityHints.Length,
                value.AcknowledgedCommands.Length,
                value.FrameSamples.Length,
                value.TransformSamples.Length);
            ShooterPureStateSyncDecodeBuffer.ValidateFrameSamples(
                value.FrameSamples,
                value.FrameSamples.Length,
                value.TransformSamples.Length,
                value.Frame);
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
                14,
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
            writer.WriteUnmanagedSpan((snapshot.FrameSamples ?? Array.Empty<ShooterPureStateFrameSample>()).AsSpan(0, snapshot.EffectiveFrameSampleCount));
            writer.WriteUnmanagedSpan((snapshot.TransformSamples ?? Array.Empty<ShooterPureStateTransformSample>()).AsSpan(0, snapshot.EffectiveTransformSampleCount));
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
