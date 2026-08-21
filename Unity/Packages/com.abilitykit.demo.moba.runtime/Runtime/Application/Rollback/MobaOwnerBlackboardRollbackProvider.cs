using System;
using System.Collections.Generic;
using System.Text;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Triggering.Blackboard;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// Exposes the owner keys currently owned by a MOBA runtime subsystem.
    /// Implementations must only report live keys and must not create or release owners.
    /// </summary>
    public interface IMobaOwnerKeySource
    {
        string Name { get; }

        void CopyActiveOwnerKeys(List<long> destination);
    }

    /// <summary>
    /// Bridges the owner Blackboard snapshot contract into the MOBA rollback registry.
    /// Owner creation/destruction is deliberately not inferred here; a mismatched active
    /// owner set fails the provider import instead of silently creating or releasing owners.
    /// </summary>
    public sealed class MobaOwnerBlackboardRollbackProvider : IRollbackStateProvider, IRollbackStatePreflightProvider
    {
        public const int DefaultKey = 10008;

        private readonly IOwnerBlackboardSnapshotStore _store;
        private readonly List<IMobaOwnerKeySource> _ownerKeySources;

        public MobaOwnerBlackboardRollbackProvider(IOwnerBlackboardSnapshotStore store)
            : this(store, null)
        {
        }

        public MobaOwnerBlackboardRollbackProvider(
            IOwnerBlackboardSnapshotStore store,
            IEnumerable<IMobaOwnerKeySource> ownerKeySources)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _ownerKeySources = new List<IMobaOwnerKeySource>();
            if (ownerKeySources == null) return;

            foreach (var source in ownerKeySources)
            {
                if (source != null && !_ownerKeySources.Contains(source)) _ownerKeySources.Add(source);
            }
        }

        public int Key => DefaultKey;

        public void ValidateImport(FrameIndex frame, byte[] payload)
        {
            var snapshotSet = ParsePayload(frame, payload);
            if (!ValidateOwnerKeySources(snapshotSet, out var lifecycleError))
                throw new InvalidOperationException($"Owner Blackboard snapshot lifecycle check failed at frame={frame.Value}. {lifecycleError}");

            if (!(_store is IOwnerBlackboardSnapshotValidator validator))
                throw new InvalidOperationException("Owner Blackboard snapshot store does not support non-mutating preflight validation.");
            if (!validator.TryValidateSnapshotSet(snapshotSet, out var error))
                throw new InvalidOperationException($"Owner Blackboard snapshot preflight validation failed at frame={frame.Value}. {error}");
        }

        public byte[] Export(FrameIndex frame)
        {
            if (!_store.TryCaptureSnapshotSet(out var snapshotSet, out var error))
                throw new InvalidOperationException($"Owner Blackboard snapshot export failed at frame={frame.Value}. {error}");
            if (!ValidateOwnerKeySources(snapshotSet, out error))
                throw new InvalidOperationException($"Owner Blackboard snapshot export lifecycle check failed at frame={frame.Value}. {error}");
            return Encoding.UTF8.GetBytes(snapshotSet.ToJson());
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                throw new InvalidOperationException($"Owner Blackboard snapshot payload is empty at frame={frame.Value}.");

            var snapshotSet = ParsePayload(frame, payload);

            if (!ValidateOwnerKeySources(snapshotSet, out var lifecycleError))
                throw new InvalidOperationException($"Owner Blackboard snapshot lifecycle check failed at frame={frame.Value}. {lifecycleError}");

            if (!_store.TryRestoreSnapshotSet(snapshotSet, out var error))
                throw new InvalidOperationException($"Owner Blackboard snapshot restore failed at frame={frame.Value}. {error}");
        }

        private static BlackboardSnapshotSet ParsePayload(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                throw new InvalidOperationException($"Owner Blackboard snapshot payload is empty at frame={frame.Value}.");

            try
            {
                return BlackboardSnapshotSet.FromJson(Encoding.UTF8.GetString(payload));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Owner Blackboard snapshot payload is invalid at frame={frame.Value}.", ex);
            }
        }

        private bool ValidateOwnerKeySources(BlackboardSnapshotSet snapshotSet, out string error)
        {
            error = null;
            if (_ownerKeySources.Count == 0) return true;

            var snapshotOwners = new HashSet<long>();
            var owners = snapshotSet?.Owners;
            if (owners != null)
            {
                for (var i = 0; i < owners.Count; i++)
                {
                    var owner = owners[i];
                    if (owner != null && owner.OwnerKey != 0) snapshotOwners.Add(owner.OwnerKey);
                }
            }

            var reported = new List<long>();
            var lifecycleOwners = new HashSet<long>();
            for (var i = 0; i < _ownerKeySources.Count; i++)
            {
                var source = _ownerKeySources[i];
                reported.Clear();
                try
                {
                    source.CopyActiveOwnerKeys(reported);
                }
                catch (Exception ex)
                {
                    error = $"Owner key source '{source.Name}' failed while reporting active owners: {ex.Message}";
                    return false;
                }

                for (var j = 0; j < reported.Count; j++)
                {
                    var ownerKey = reported[j];
                    if (ownerKey == 0) continue;
                    lifecycleOwners.Add(ownerKey);
                    if (!snapshotOwners.Contains(ownerKey))
                    {
                        error = $"Owner key source '{source.Name}' reports ownerKey={ownerKey}, but the Blackboard snapshot does not contain it.";
                        return false;
                    }
                }
            }

            foreach (var ownerKey in snapshotOwners)
            {
                if (!lifecycleOwners.Contains(ownerKey))
                {
                    error = $"Blackboard snapshot contains ownerKey={ownerKey}, but no registered lifecycle source reports it.";
                    return false;
                }
            }

            return true;
        }
    }
}
