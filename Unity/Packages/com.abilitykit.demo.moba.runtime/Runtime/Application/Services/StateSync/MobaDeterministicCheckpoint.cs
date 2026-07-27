using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Demo.Moba.Services.StateSync
{
    public readonly struct MobaDeterministicCheckpoint
    {
        public const int CurrentSchemaVersion = 1;

        public MobaDeterministicCheckpoint(
            int schemaVersion,
            string worldId,
            string worldType,
            int tickRate,
            int frame,
            uint stateHash,
            MobaStateRecoveryEntry[] entries)
        {
            SchemaVersion = schemaVersion;
            WorldId = worldId ?? string.Empty;
            WorldType = worldType ?? string.Empty;
            TickRate = tickRate;
            Frame = frame;
            StateHash = stateHash;
            Entries = entries ?? Array.Empty<MobaStateRecoveryEntry>();
        }

        public int SchemaVersion { get; }
        public string WorldId { get; }
        public string WorldType { get; }
        public int TickRate { get; }
        public int Frame { get; }
        public uint StateHash { get; }
        public MobaStateRecoveryEntry[] Entries { get; }
    }

    public sealed class MobaDeterministicCheckpointCoordinator
    {
        private const uint HashSeed = 2166136261u;

        private readonly string _worldId;
        private readonly string _worldType;
        private readonly int _tickRate;
        private readonly IMobaStateRecoveryProvider[] _providers;
        private readonly Dictionary<int, IMobaStateRecoveryProvider> _providersByKey;

        public MobaDeterministicCheckpointCoordinator(
            string worldId,
            string worldType,
            int tickRate,
            IEnumerable<IMobaStateRecoveryProvider> providers)
        {
            if (string.IsNullOrWhiteSpace(worldId)) throw new ArgumentException("World id is required.", nameof(worldId));
            if (string.IsNullOrWhiteSpace(worldType)) throw new ArgumentException("World type is required.", nameof(worldType));
            if (tickRate <= 0) throw new ArgumentOutOfRangeException(nameof(tickRate));
            if (providers == null) throw new ArgumentNullException(nameof(providers));

            _worldId = worldId;
            _worldType = worldType;
            _tickRate = tickRate;

            var ordered = new List<IMobaStateRecoveryProvider>();
            _providersByKey = new Dictionary<int, IMobaStateRecoveryProvider>();
            foreach (var provider in providers)
            {
                if (provider == null) throw new ArgumentException("Checkpoint providers cannot contain null.", nameof(providers));
                if (!_providersByKey.TryAdd(provider.Key, provider))
                {
                    throw new ArgumentException($"Duplicate checkpoint provider key: {provider.Key}.", nameof(providers));
                }

                ordered.Add(provider);
            }

            ordered.Sort((left, right) => left.Key.CompareTo(right.Key));
            _providers = ordered.ToArray();
        }

        public MobaDeterministicCheckpoint Capture(FrameIndex frame)
        {
            var entries = CaptureEntries(frame);
            return new MobaDeterministicCheckpoint(
                MobaDeterministicCheckpoint.CurrentSchemaVersion,
                _worldId,
                _worldType,
                _tickRate,
                frame.Value,
                ComputeStateHash(frame),
                entries);
        }

        public void Restore(in MobaDeterministicCheckpoint checkpoint)
        {
            Validate(checkpoint);

            var frame = new FrameIndex(checkpoint.Frame);
            var rollback = CaptureEntries(frame);
            var imported = 0;
            try
            {
                for (; imported < checkpoint.Entries.Length; imported++)
                {
                    var entry = checkpoint.Entries[imported];
                    _providersByKey[entry.Key].ImportState(frame, entry.Payload);
                }

                var actualHash = ComputeStateHash(frame);
                if (actualHash != checkpoint.StateHash)
                {
                    throw new InvalidOperationException(
                        $"Checkpoint state hash mismatch after restore. Expected {checkpoint.StateHash}, actual {actualHash}.");
                }
            }
            catch (Exception restoreFailure)
            {
                Exception rollbackFailure = null;
                for (var index = Math.Min(imported, rollback.Length - 1); index >= 0; index--)
                {
                    var entry = rollback[index];
                    try
                    {
                        _providersByKey[entry.Key].ImportState(frame, entry.Payload);
                    }
                    catch (Exception ex)
                    {
                        rollbackFailure ??= ex;
                    }
                }

                if (rollbackFailure != null)
                {
                    throw new AggregateException("Checkpoint restore and rollback both failed.", restoreFailure, rollbackFailure);
                }

                throw;
            }
        }

        public uint ComputeStateHash(FrameIndex frame)
        {
            var hash = new MobaStateHashBuilder(HashSeed);
            hash.AddInt(MobaDeterministicCheckpoint.CurrentSchemaVersion);
            AddString(ref hash, _worldId);
            AddString(ref hash, _worldType);
            hash.AddInt(_tickRate);
            hash.AddInt(frame.Value);
            hash.AddInt(_providers.Length);

            foreach (var provider in _providers)
            {
                hash.AddInt(provider.Key);
                provider.AddStateHash(frame, ref hash);
            }

            return hash.Value;
        }

        private MobaStateRecoveryEntry[] CaptureEntries(FrameIndex frame)
        {
            var entries = new MobaStateRecoveryEntry[_providers.Length];
            for (var index = 0; index < _providers.Length; index++)
            {
                var provider = _providers[index];
                entries[index] = new MobaStateRecoveryEntry(provider.Key, provider.ExportState(frame));
            }

            return entries;
        }

        private void Validate(in MobaDeterministicCheckpoint checkpoint)
        {
            if (checkpoint.SchemaVersion != MobaDeterministicCheckpoint.CurrentSchemaVersion)
            {
                throw new InvalidOperationException($"Unsupported checkpoint schema version: {checkpoint.SchemaVersion}.");
            }

            if (!string.Equals(checkpoint.WorldId, _worldId, StringComparison.Ordinal) ||
                !string.Equals(checkpoint.WorldType, _worldType, StringComparison.Ordinal) ||
                checkpoint.TickRate != _tickRate)
            {
                throw new InvalidOperationException("Checkpoint world identity or tick rate does not match the restore target.");
            }

            var entries = checkpoint.Entries ?? Array.Empty<MobaStateRecoveryEntry>();
            if (entries.Length != _providers.Length)
            {
                throw new InvalidOperationException("Checkpoint provider set does not match the restore target.");
            }

            for (var index = 0; index < entries.Length; index++)
            {
                if (!_providersByKey.ContainsKey(entries[index].Key))
                {
                    throw new InvalidOperationException($"Checkpoint provider key is not registered: {entries[index].Key}.");
                }

                if (index > 0 && entries[index - 1].Key >= entries[index].Key)
                {
                    throw new InvalidOperationException("Checkpoint provider entries must be unique and sorted by key.");
                }
            }
        }

        private static void AddString(ref MobaStateHashBuilder hash, string value)
        {
            value ??= string.Empty;
            hash.AddInt(value.Length);
            for (var index = 0; index < value.Length; index++)
            {
                hash.AddInt(value[index]);
            }
        }
    }
}
