using System;
using System.Collections.Generic;
using System.Text;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Triggering.Blackboard;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

    /// <summary>Explicit actor ownership and roster evidence for bounded membership restoration.</summary>
    public interface IMobaActorOwnerKeySource : IMobaOwnerKeySource
    {
        void CopyActorIds(List<int> destination);
    }

    /// <summary>
    /// Bridges the owner Blackboard snapshot contract into the MOBA rollback registry.
    /// Legacy payloads require identical membership. Versioned payloads can reconcile
    /// actor-only owners when the actor roster and all other lifecycle sources are stable.
    /// </summary>
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaOwnerBlackboardRollbackProvider : IRollbackStateProvider, IRollbackStatePreflightProvider
    {
        public const int DefaultKey = 10008;

        private readonly IOwnerBlackboardSnapshotStore _store;
        private readonly List<IMobaOwnerKeySource> _ownerKeySources;
        private readonly IMobaActorOwnerKeySource _actorSource;

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
                if (source == null || _ownerKeySources.Contains(source)) continue;
                if (source is IMobaActorOwnerKeySource actors)
                {
                    if (_actorSource != null) throw new ArgumentException("Multiple actor owner sources.", nameof(ownerKeySources));
                    _actorSource = actors;
                }
                _ownerKeySources.Add(source);
            }
        }

        public int Key => DefaultKey;

        public void ValidateImport(FrameIndex frame, byte[] payload)
        {
            if (TryReadMembershipPayload(payload, out var membership))
            {
                PrepareMembershipRestore(membership);
                return;
            }
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
            if (_actorSource == null) return Encoding.UTF8.GetBytes(snapshotSet.ToJson());
            var state = new MobaOwnerBlackboardMembershipPayload
            {
                Snapshot = snapshotSet,
                ActorIds = CaptureActorIds(),
                Sources = CaptureSources(),
            };
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(state));
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            if (TryReadMembershipPayload(payload, out var membership))
            {
                var plan = PrepareMembershipRestore(membership);
                if (!plan.TryApply(out var membershipError)) throw new InvalidOperationException(membershipError);
                if (!_store.TryCaptureSnapshotSet(out var actual, out var captureError) ||
                    !ValidateOwnerKeySources(actual, out captureError))
                    throw new InvalidOperationException("Restored owner Blackboard lifecycle mismatch. " + captureError);
                return;
            }
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

        private static bool TryReadMembershipPayload(byte[] payload, out MobaOwnerBlackboardMembershipPayload state)
        {
            if (payload == null || payload.Length == 0) throw new InvalidOperationException("Owner Blackboard payload is empty.");
            state = null;
            try
            {
                var json = Encoding.UTF8.GetString(payload);
                var root = JObject.Parse(json);
                if (root["Format"] == null) return false;
                if ((string)root["Format"] != MobaOwnerBlackboardMembershipPayload.FormatName || (int?)root["Version"] != 1)
                    throw new InvalidOperationException("Unsupported owner Blackboard membership payload.");
                state = JsonConvert.DeserializeObject<MobaOwnerBlackboardMembershipPayload>(json);
                if (state?.Snapshot == null || state.ActorIds == null || state.Sources == null)
                    throw new InvalidOperationException("Incomplete owner Blackboard membership payload.");
                return true;
            }
            catch (Exception ex) when (ex is JsonException || ex is FormatException || ex is InvalidCastException || ex is OverflowException)
            {
                throw new InvalidOperationException("Owner Blackboard snapshot payload is invalid.", ex);
            }
        }

        private int[] CaptureActorIds()
        {
            var ids = new List<int>();
            _actorSource.CopyActorIds(ids);
            ids.Sort();
            for (var i = 0; i < ids.Count; i++)
                if (ids[i] <= 0 || i > 0 && ids[i - 1] == ids[i])
                    throw new InvalidOperationException("Invalid actor roster for owner Blackboard restore.");
            return ids.ToArray();
        }

        private MobaBlackboardOwnerSourceEntry[] CaptureSources()
        {
            var entries = new List<MobaBlackboardOwnerSourceEntry>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in _ownerKeySources)
            {
                if (string.IsNullOrWhiteSpace(source.Name) || !names.Add(source.Name))
                    throw new InvalidOperationException("Duplicate or invalid Blackboard owner source name.");
                var keys = new List<long>();
                source.CopyActiveOwnerKeys(keys);
                keys.Sort();
                for (var i = 0; i < keys.Count; i++)
                    if (keys[i] == 0 || i > 0 && keys[i - 1] == keys[i])
                        throw new InvalidOperationException("Duplicate or invalid Blackboard owner source key.");
                entries.Add(new MobaBlackboardOwnerSourceEntry { Name = source.Name, OwnerKeys = keys.ToArray() });
            }
            entries.Sort((left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));
            return entries.ToArray();
        }

        private IOwnerBlackboardMembershipRestorePlan PrepareMembershipRestore(MobaOwnerBlackboardMembershipPayload state)
        {
            if (_actorSource == null || !(_store is IOwnerBlackboardMembershipSnapshotStore membershipStore))
                throw new InvalidOperationException("Actor Blackboard membership restore is unavailable.");
            var currentRoster = CaptureActorIds();
            if (!SameKeys(currentRoster, state.ActorIds))
                throw new InvalidOperationException("Actor roster changed; Blackboard membership restore requires the same roster.");
            if (!_store.TryCaptureSnapshotSet(out var current, out var error) || !ValidateOwnerKeySources(current, out error))
                throw new InvalidOperationException("Current Blackboard lifecycle is invalid. " + error);

            var currentSources = CaptureSources();
            if (currentSources.Length != state.Sources.Length)
                throw new InvalidOperationException("Blackboard owner source set changed.");
            var allowed = new HashSet<long>();
            foreach (var actorId in currentRoster) allowed.Add(actorId);
            var reported = new HashSet<long>();
            long[] capturedActorKeys = null;
            for (var i = 0; i < currentSources.Length; i++)
            {
                var incoming = state.Sources[i];
                var live = currentSources[i];
                if (incoming == null || incoming.Name != live.Name || incoming.OwnerKeys == null)
                    throw new InvalidOperationException("Invalid or changed Blackboard owner source classification.");
                var unique = new HashSet<long>();
                foreach (var key in incoming.OwnerKeys)
                {
                    if (key == 0 || !unique.Add(key)) throw new InvalidOperationException("Invalid Blackboard owner source keys.");
                    reported.Add(key);
                }
                if (incoming.Name == _actorSource.Name) capturedActorKeys = incoming.OwnerKeys;
                else
                {
                    if (!SameKeys(live.OwnerKeys, incoming.OwnerKeys))
                        throw new InvalidOperationException($"Non-actor Blackboard owner source '{live.Name}' changed.");
                    foreach (var key in live.OwnerKeys) allowed.Remove(key);
                }
            }

            var snapshotOwners = new HashSet<long>();
            var expectedActorKeys = new List<long>();
            var rosterKeys = new HashSet<int>(currentRoster);
            foreach (var owner in state.Snapshot.Owners ?? new List<BlackboardSnapshot>())
            {
                if (owner == null || owner.OwnerKey == 0 || !snapshotOwners.Add(owner.OwnerKey))
                    throw new InvalidOperationException("Duplicate or invalid owner snapshot.");
                if (owner.OwnerKey <= int.MaxValue && owner.OwnerKey > 0 && rosterKeys.Contains((int)owner.OwnerKey))
                    expectedActorKeys.Add(owner.OwnerKey);
            }
            expectedActorKeys.Sort();
            if (!snapshotOwners.SetEquals(reported) || capturedActorKeys == null ||
                !SameKeys(expectedActorKeys.ToArray(), capturedActorKeys))
                throw new InvalidOperationException("Snapshot Blackboard owner source classification does not match its owners.");
            if (!membershipStore.TryPrepareMembershipRestore(state.Snapshot, allowed, out var plan, out error))
                throw new InvalidOperationException("Owner Blackboard membership preflight failed. " + error);
            return plan;
        }

        private static bool SameKeys<T>(T[] left, T[] right) where T : IEquatable<T>
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (var i = 0; i < left.Length; i++) if (!left[i].Equals(right[i])) return false;
            return true;
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

    internal sealed class MobaOwnerBlackboardMembershipPayload
    {
        public const string FormatName = "moba-owner-blackboard";
        public string Format = FormatName;
        public int Version = 1;
        public BlackboardSnapshotSet Snapshot;
        public int[] ActorIds;
        public MobaBlackboardOwnerSourceEntry[] Sources;
    }

    internal sealed class MobaBlackboardOwnerSourceEntry
    {
        public string Name;
        public long[] OwnerKeys;
    }
}
