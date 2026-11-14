namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>Exact, generation-checked binding made when an independent recast is prepared.</summary>
    public readonly struct MobaSkillChildRecastBinding
    {
        public MobaSkillChildRecastBinding(MobaSkillCastRuntimeHandle original, MobaSkillRuntimeChildRef child)
        { Original = original; Child = child; }
        public MobaSkillCastRuntimeHandle Original { get; }
        public MobaSkillRuntimeChildRef Child { get; }
    }

    public sealed partial class MobaSkillCastRuntimeService
    {
        // Cast blackboard is already captured by the authoritative runtime rollback provider.
        private static MobaSkillRuntimeBlackboardKey RecastKey(int id, string name, MobaSkillRuntimeValueKind kind) =>
            new MobaSkillRuntimeBlackboardKey(id, "skill.child." + name, kind, ownerModuleId: 1002);
        private static readonly MobaSkillRuntimeBlackboardKey ChildChannel = RecastKey(120, "channel", MobaSkillRuntimeValueKind.Int);
        private static readonly MobaSkillRuntimeBlackboardKey ChildTerminal = RecastKey(121, "terminal", MobaSkillRuntimeValueKind.Bool);
        private static readonly MobaSkillRuntimeBlackboardKey OriginalRuntime = RecastKey(122, "originalRuntime", MobaSkillRuntimeValueKind.Long);
        private static readonly MobaSkillRuntimeBlackboardKey OriginalGeneration = RecastKey(123, "originalGeneration", MobaSkillRuntimeValueKind.Int);
        private static readonly MobaSkillRuntimeBlackboardKey OriginalRoot = RecastKey(124, "originalRoot", MobaSkillRuntimeValueKind.Long);
        private static readonly MobaSkillRuntimeBlackboardKey BoundChildId = RecastKey(125, "id", MobaSkillRuntimeValueKind.Long);
        private static readonly MobaSkillRuntimeBlackboardKey BoundChildContext = RecastKey(126, "context", MobaSkillRuntimeValueKind.Long);
        private static readonly MobaSkillRuntimeBlackboardKey BoundChildConfig = RecastKey(127, "config", MobaSkillRuntimeValueKind.Int);
        private static readonly MobaSkillRuntimeBlackboardKey BoundChildKind = RecastKey(128, "kind", MobaSkillRuntimeValueKind.Int);
        private static readonly MobaSkillRuntimeBlackboardKey PendingTerminal = RecastKey(129, "pendingTerminal", MobaSkillRuntimeValueKind.Bool);
        private static readonly MobaSkillRuntimeBlackboardKey PendingProjectile = RecastKey(130, "pendingProjectile", MobaSkillRuntimeValueKind.Long);
        private static readonly MobaSkillRuntimeBlackboardKey PendingContext = RecastKey(131, "pendingContext", MobaSkillRuntimeValueKind.Long);

        public bool HasChildChannel(int ownerActorId, int channelId)
        {
            if (ownerActorId <= 0 || channelId <= 0) return false;
            foreach (var pair in _runtimes)
                if (IsInChannel(pair.Value, ownerActorId, channelId)) return true;
            return false;
        }

        /// <summary>Revoke owner-local recast capabilities on death, including casts whose input flow already ended.</summary>
        public void EndOwnerChildChannels(int ownerActorId, System.Action<MobaSkillRuntimeChildRef> despawnChild)
        {
            var ids = s_longListPool.Get();
            try
            {
                foreach (var pair in _runtimes)
                    if (pair.Value.CasterActorId == ownerActorId &&
                        pair.Value.Blackboard.TryGetInt(in ChildChannel, out var channel) && channel > 0)
                        ids.Add(pair.Key);
                ids.Sort();
                foreach (var id in ids)
                {
                    if (!TryGet(id, out var cast)) continue;
                    // Latch first: queued arrival/expiry cannot commit an explosion during cleanup.
                    cast.Blackboard.SetBool(in ChildTerminal, true);
                    var children = new System.Collections.Generic.List<MobaSkillRuntimeChildRef>();
                    cast.CopyChildrenTo(children);
                    try
                    {
                        foreach (var child in children) despawnChild?.Invoke(child);
                    }
                    finally { ForceTerminate(cast.Handle, MobaSkillRuntimeEndReason.Cancelled); }
                }
            }
            finally { s_longListPool.Release(ids); }
        }

        public bool TryBindChildChannel(in MobaSkillCastRuntimeHandle handle, int channelId)
        {
            if (channelId <= 0 || !TryGet(in handle, out var runtime) || !runtime.Handle.Equals(handle) || runtime.RootContextId != handle.RootContextId ||
                HasChildChannel(runtime.CasterActorId, channelId)) return false;
            return runtime.Blackboard.SetInt(in ChildChannel, channelId);
        }

        public bool TrySelectRecastArea(int ownerActorId, int channelId, out MobaSkillChildRecastBinding binding, out string error,
            int skillSlot = 0, int baseSkillId = 0, bool allowFlight = false)
        {
            binding = default;
            error = "No deployed child in channel.";
            MobaSkillCastRuntime original = null;
            foreach (var pair in _runtimes)
            {
                if (!IsInChannel(pair.Value, ownerActorId, channelId)) continue;
                if (original != null) { error = "Ambiguous child channel."; return false; }
                original = pair.Value;
            }
            if (original == null) return false;
            if (skillSlot > 0 && original.SkillSlot != skillSlot || baseSkillId > 0 && original.BaseSkillId != baseSkillId)
            { error = "Deployed child belongs to another skill slot."; return false; }
            var count = 0;
            var child = default(MobaSkillRuntimeChildRef);
            foreach (var candidate in original.Children)
                if (candidate.Kind == MobaSkillRuntimeChildKind.Area) { count++; child = candidate; }
            if (count == 0 && allowFlight)
                foreach (var candidate in original.Children)
                    if (candidate.Kind == MobaSkillRuntimeChildKind.Projectile) { count++; child = candidate; }
            if (count != 1) { error = count == 0 ? "Child is in flight or not deployed." : "Ambiguous deployed child."; return false; }
            binding = new MobaSkillChildRecastBinding(original.Handle, child);
            error = null;
            return true;
        }

        public bool BindRecastArea(in MobaSkillCastRuntimeHandle recastHandle, in MobaSkillChildRecastBinding binding)
        {
            var originalHandle = binding.Original;
            if (!TryGet(in recastHandle, out var recast) || !TryGet(in originalHandle, out var original) ||
                !recast.Handle.Equals(recastHandle) || recast.RootContextId != recastHandle.RootContextId ||
                !original.Handle.Equals(originalHandle) || original.RootContextId != originalHandle.RootContextId ||
                recast.CasterActorId != original.CasterActorId || recast.SkillSlot != original.SkillSlot ||
                recast.BaseSkillId != original.BaseSkillId || original.IsEnding || original.IsEnded ||
                original.Blackboard.TryGetBool(in ChildTerminal, out var terminal) && terminal ||
                (binding.Child.Kind != MobaSkillRuntimeChildKind.Area && binding.Child.Kind != MobaSkillRuntimeChildKind.Projectile) || !ContainsChild(original, binding.Child)) return false;
            var board = recast.Blackboard;
            board.SetLong(in OriginalRuntime, binding.Original.RuntimeId);
            board.SetInt(in OriginalGeneration, binding.Original.Generation);
            board.SetLong(in OriginalRoot, binding.Original.RootContextId);
            board.SetLong(in BoundChildId, binding.Child.ChildId);
            board.SetLong(in BoundChildContext, binding.Child.ContextId);
            board.SetInt(in BoundChildConfig, binding.Child.ConfigId);
            board.SetInt(in BoundChildKind, (int)binding.Child.Kind);
            return true;
        }

        public bool TryGetRecastArea(in MobaSkillCastRuntimeHandle recastHandle, out MobaSkillChildRecastBinding binding)
        {
            binding = default;
            if (!TryGet(in recastHandle, out var recast) || !recast.Handle.Equals(recastHandle) || recast.RootContextId != recastHandle.RootContextId) return false;
            var board = recast.Blackboard;
            if (!board.TryGetLong(in OriginalRuntime, out var runtimeId) || !board.TryGetInt(in OriginalGeneration, out var generation) ||
                !board.TryGetLong(in OriginalRoot, out var root) || !board.TryGetLong(in BoundChildId, out var childId) ||
                !board.TryGetLong(in BoundChildContext, out var context) || !board.TryGetInt(in BoundChildConfig, out var config)) return false;
            var originalHandle = new MobaSkillCastRuntimeHandle(runtimeId, generation, root);
            var kind = board.TryGetInt(in BoundChildKind, out var storedKind) ? (MobaSkillRuntimeChildKind)storedKind : MobaSkillRuntimeChildKind.Area;
            var child = new MobaSkillRuntimeChildRef(kind, childId, context, config);
            if (!TryGet(in originalHandle, out var original) || !original.Handle.Equals(originalHandle) || original.RootContextId != originalHandle.RootContextId ||
                original.CasterActorId != recast.CasterActorId || original.SkillSlot != recast.SkillSlot ||
                original.BaseSkillId != recast.BaseSkillId || original.IsEnding || original.IsEnded ||
                original.Blackboard.TryGetBool(in ChildTerminal, out var terminal) && terminal ||
                !ContainsChild(original, in child)) return false;
            binding = new MobaSkillChildRecastBinding(originalHandle, child);
            return true;
        }

        /// <summary>Queue once on the original cast, bound to the exact flying child.</summary>
        public bool TryQueueChildTerminal(in MobaSkillChildRecastBinding binding)
        {
            var handle = binding.Original;
            if (binding.Child.Kind != MobaSkillRuntimeChildKind.Projectile || !TryGet(in handle, out var original) ||
                !original.Handle.Equals(handle) || original.RootContextId != handle.RootContextId ||
                !ContainsChild(original, binding.Child) || original.IsEnding || original.IsEnded ||
                original.Blackboard.TryGetBool(in ChildTerminal, out var terminal) && terminal) return false;
            var board = original.Blackboard;
            if (board.TryGetBool(in PendingTerminal, out var pending) && pending)
                return board.TryGetLong(in PendingProjectile, out var id) && id == binding.Child.ChildId &&
                    board.TryGetLong(in PendingContext, out var context) && context == binding.Child.ContextId;
            board.SetLong(in PendingProjectile, binding.Child.ChildId);
            board.SetLong(in PendingContext, binding.Child.ContextId);
            board.SetBool(in PendingTerminal, true);
            return true;
        }

        /// <summary>Arrival handover runs while the retained flying child still belongs to this cast.</summary>
        public bool TryGetOwnedArea(in MobaSkillCastRuntimeHandle handle, out MobaSkillChildRecastBinding binding)
        {
            binding = default;
            if (!TryGet(in handle, out var original) || !original.Handle.Equals(handle) || original.RootContextId != handle.RootContextId ||
                original.IsEnding || original.IsEnded || original.Blackboard.TryGetBool(in ChildTerminal, out var terminal) && terminal) return false;
            var count = 0; var area = default(MobaSkillRuntimeChildRef);
            foreach (var child in original.Children)
                if (child.Kind == MobaSkillRuntimeChildKind.Area) { count++; area = child; }
            if (count != 1) return false;
            binding = new MobaSkillChildRecastBinding(handle, area); return true;
        }

        public bool TryGetPendingRecastArea(in MobaSkillCastRuntimeHandle handle, out MobaSkillChildRecastBinding binding)
        {
            binding = default;
            if (!TryGet(in handle, out var original) || !original.Handle.Equals(handle) || original.RootContextId != handle.RootContextId ||
                original.IsEnding || original.IsEnded || original.Blackboard.TryGetBool(in ChildTerminal, out var terminal) && terminal ||
                !original.Blackboard.TryGetBool(in PendingTerminal, out var pending) || !pending ||
                !original.Blackboard.TryGetLong(in PendingProjectile, out var projectile) ||
                !original.Blackboard.TryGetLong(in PendingContext, out var context)) return false;
            var hasProjectile = false; var count = 0; var area = default(MobaSkillRuntimeChildRef);
            foreach (var child in original.Children)
            {
                if (child.Kind == MobaSkillRuntimeChildKind.Projectile && child.ChildId == projectile && child.ContextId == context) hasProjectile = true;
                if (child.Kind == MobaSkillRuntimeChildKind.Area) { count++; area = child; }
            }
            if (!hasProjectile || count != 1) return false;
            binding = new MobaSkillChildRecastBinding(handle, area);
            return true;
        }

        public bool TryClaimChildTerminal(in MobaSkillChildRecastBinding binding)
        {
            var handle = binding.Original;
            if (!TryGet(in handle, out var original) || !original.Handle.Equals(handle) || original.RootContextId != handle.RootContextId || !ContainsChild(original, binding.Child) ||
                original.Blackboard.TryGetBool(in ChildTerminal, out var terminal) && terminal) return false;
            original.Blackboard.SetBool(in ChildTerminal, true);
            return true;
        }

        private static bool IsInChannel(MobaSkillCastRuntime cast, int owner, int channel) =>
            cast != null && !cast.IsEnded && !cast.IsEnding && cast.CasterActorId == owner && channel > 0 &&
            cast.Blackboard.TryGetInt(in ChildChannel, out var value) && value == channel &&
            !(cast.Blackboard.TryGetBool(in ChildTerminal, out var terminal) && terminal);

        private static bool ContainsChild(MobaSkillCastRuntime runtime, in MobaSkillRuntimeChildRef child)
        {
            foreach (var candidate in runtime.Children) if (candidate.Equals(child)) return true;
            return false;
        }
    }
}
