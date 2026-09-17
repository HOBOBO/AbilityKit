using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Deterministic;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Demo.Moba.Services
{
    public enum MobaSkillEconomyTransactionState
    {
        Reserved = 0,
        Committed = 1,
    }

    public readonly struct MobaSkillEconomyAvailability
    {
        public MobaSkillEconomyAvailability(bool available, string reason, int charges, long readyTimeMs)
        {
            Available = available;
            Reason = reason;
            Charges = charges;
            ReadyTimeMs = readyTimeMs;
        }

        public bool Available { get; }
        public string Reason { get; }
        public int Charges { get; }
        public long ReadyTimeMs { get; }
    }

    internal readonly struct MobaSkillEconomyTransactionSnapshot
    {
        public MobaSkillEconomyTransactionSnapshot(
            in MobaSkillCastRuntimeHandle handle, int actorId, int skillId, int skillSlot,
            int resourceType, long resourceAmountRaw, int chargeCost, bool refundBeforeCommit,
            int cooldownMs, int cooldownGroupId, int sharedCooldownMs, int globalCooldownMs,
            MobaSkillEconomyTransactionState state)
        {
            Handle = handle;
            ActorId = actorId;
            SkillId = skillId;
            SkillSlot = skillSlot;
            ResourceType = resourceType;
            ResourceAmountRaw = resourceAmountRaw;
            ChargeCost = chargeCost;
            RefundBeforeCommit = refundBeforeCommit;
            CooldownMs = cooldownMs;
            CooldownGroupId = cooldownGroupId;
            SharedCooldownMs = sharedCooldownMs;
            GlobalCooldownMs = globalCooldownMs;
            State = state;
        }

        public MobaSkillCastRuntimeHandle Handle { get; }
        public int ActorId { get; }
        public int SkillId { get; }
        public int SkillSlot { get; }
        public int ResourceType { get; }
        public long ResourceAmountRaw { get; }
        public int ChargeCost { get; }
        public bool RefundBeforeCommit { get; }
        public int CooldownMs { get; }
        public int CooldownGroupId { get; }
        public int SharedCooldownMs { get; }
        public int GlobalCooldownMs { get; }
        public MobaSkillEconomyTransactionState State { get; }
    }

    internal readonly struct MobaSkillEconomyCooldownSnapshot
    {
        public MobaSkillEconomyCooldownSnapshot(int actorId, int groupId, long endTimeMs)
        {
            ActorId = actorId;
            GroupId = groupId;
            EndTimeMs = endTimeMs;
        }

        public int ActorId { get; }
        public int GroupId { get; }
        public long EndTimeMs { get; }
    }

    internal readonly struct MobaSkillEconomyServiceSnapshot
    {
        public MobaSkillEconomyServiceSnapshot(
            MobaSkillEconomyTransactionSnapshot[] transactions,
            MobaSkillEconomyCooldownSnapshot[] cooldowns,
            MobaSkillEconomyCooldownSnapshot[] globalCooldowns)
        {
            Transactions = transactions ?? Array.Empty<MobaSkillEconomyTransactionSnapshot>();
            Cooldowns = cooldowns ?? Array.Empty<MobaSkillEconomyCooldownSnapshot>();
            GlobalCooldowns = globalCooldowns ?? Array.Empty<MobaSkillEconomyCooldownSnapshot>();
        }

        public MobaSkillEconomyTransactionSnapshot[] Transactions { get; }
        public MobaSkillEconomyCooldownSnapshot[] Cooldowns { get; }
        public MobaSkillEconomyCooldownSnapshot[] GlobalCooldowns { get; }
    }

    [WorldService(typeof(MobaSkillEconomyService))]
    public sealed class MobaSkillEconomyService : IService, IMobaSkillRuntimeLifecycleHook
    {
        private readonly struct ActorGroupKey : IEquatable<ActorGroupKey>
        {
            public ActorGroupKey(int actorId, int groupId)
            {
                ActorId = actorId;
                GroupId = groupId;
            }

            public int ActorId { get; }
            public int GroupId { get; }
            public bool Equals(ActorGroupKey other) => ActorId == other.ActorId && GroupId == other.GroupId;
            public override bool Equals(object obj) => obj is ActorGroupKey other && Equals(other);
            public override int GetHashCode() => (ActorId * 397) ^ GroupId;
        }

        private sealed class Transaction
        {
            public MobaSkillCastRuntimeHandle Handle;
            public int ActorId;
            public int SkillId;
            public int SkillSlot;
            public int SkillLevel;
            public int CastSequence;
            public long DiagnosticCommandId;
            public ResourceType ResourceType;
            public Fixed64 ResourceAmount;
            public long ResourceBeforeRaw;
            public long ResourceAfterRaw;
            public int ChargeCost;
            public bool RefundBeforeCommit;
            public int CooldownMs;
            public int CooldownGroupId;
            public int SharedCooldownMs;
            public int GlobalCooldownMs;
            public MobaSkillEconomyTransactionState State;
        }

        private readonly MobaSkillCastRuntimeService _runtimes;
        private readonly MobaActorLookupService _actors;
        private readonly IFrameTime _time;
        private readonly IMobaBattleDiagnosticEventSink _diagnosticEvents;
        private readonly Dictionary<long, Transaction> _transactions = new Dictionary<long, Transaction>();
        private readonly Dictionary<ActorGroupKey, long> _cooldownGroups = new Dictionary<ActorGroupKey, long>();
        private readonly Dictionary<int, long> _globalCooldowns = new Dictionary<int, long>();

        public MobaSkillEconomyService(
            MobaSkillCastRuntimeService runtimes,
            MobaActorLookupService actors,
            IFrameTime time)
            : this(runtimes, actors, time, null)
        {
        }

        public MobaSkillEconomyService(
            MobaSkillCastRuntimeService runtimes,
            MobaActorLookupService actors,
            IFrameTime time,
            IMobaBattleDiagnosticEventSink diagnosticEvents)
        {
            _runtimes = runtimes ?? throw new ArgumentNullException(nameof(runtimes));
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _diagnosticEvents = diagnosticEvents;
            _runtimes.LifecycleHooks.Register(this);
        }

        public int PendingTransactionCount => _transactions.Count;

        public bool TryReserve(
            SkillPipelineContext context,
            SkillEconomyPhaseDTO specification,
            out string failure)
        {
            failure = null;
            if (context == null || specification == null || !context.TryGetSkillRuntimeHandle(out var handle))
            {
                failure = "Skill economy reservation requires a live cast runtime.";
                return false;
            }

            if (_transactions.TryGetValue(handle.RuntimeId, out var existing))
            {
                if (existing.Handle.Equals(handle)) return true;
                failure = "Skill economy reservation rejected a stale runtime handle.";
                return false;
            }

            if (!MobaSkillRuntimeAccess.TryGetActiveSkill(
                    _actors, context.CasterActorId, context.SkillSlot, context.SkillId, out var skill))
            {
                failure = "Active skill runtime is unavailable.";
                return false;
            }

            var nowMs = NowMs;
            ConfigureAndRefreshCharges(skill, specification.MaxCharges, specification.ChargeRecoveryMs, nowMs);
            var usesCharges = specification.MaxCharges > 1 || specification.ChargeRecoveryMs > 0;
            var chargeCost = usesCharges ? Math.Max(1, specification.ChargeCost) : 0;
            var cooldownGroupId = StableId(specification.CooldownGroup);
            var resourceType = specification.ResourceType > 0
                ? (ResourceType)specification.ResourceType
                : context.ResolvedConfiguration.ResourceType;
            var amount = specification.UseResolvedResourceCost
                ? context.ResolvedConfiguration.ResourceCost
                : specification.ResourceAmount;
            var resourceAmount = amount > 0f ? MobaResourceFixedConvert.ToFixed(amount) : Fixed64.Zero;
            skill.CooldownGroupId = cooldownGroupId;
            skill.IgnoreGlobalCooldown = specification.IgnoreGlobalCooldown;
            var availability = GetAvailability(
                context.CasterActorId, skill, chargeCost, cooldownGroupId,
                specification.IgnoreGlobalCooldown, nowMs);
            if (!availability.Available)
            {
                failure = availability.Reason;
                TryCollectEconomy(context, specification,
                    BattleDiagnosticSkillExecutionStage.EconomyRejected,
                    resourceType, resourceAmount.RawValue, 0L, 0L, chargeCost,
                    BattleDiagnosticEventOutcome.Failed, failure);
                return false;
            }

            if (!TryConsumeResource(context.CasterActorId, resourceType, resourceAmount,
                    out failure, out var resourceBeforeRaw, out var resourceAfterRaw))
            {
                TryCollectEconomy(context, specification,
                    BattleDiagnosticSkillExecutionStage.EconomyRejected,
                    resourceType, resourceAmount.RawValue, resourceBeforeRaw, resourceAfterRaw,
                    chargeCost, BattleDiagnosticEventOutcome.Failed, failure);
                return false;
            }

            if (chargeCost > 0) ConsumeCharges(skill, chargeCost, nowMs);
            var cooldownMs = specification.StartSkillCooldown
                ? Math.Max(0, specification.UseResolvedSkillCooldown
                    ? context.ResolvedConfiguration.CooldownMs
                    : specification.SkillCooldownMs)
                : 0;
            _transactions.Add(handle.RuntimeId, new Transaction
            {
                Handle = handle,
                ActorId = context.CasterActorId,
                SkillId = context.SkillId,
                SkillSlot = context.SkillSlot,
                SkillLevel = context.SkillLevel,
                CastSequence = context.CastSequence,
                DiagnosticCommandId = context.DiagnosticCommandId,
                ResourceType = resourceType,
                ResourceAmount = resourceAmount,
                ResourceBeforeRaw = resourceBeforeRaw,
                ResourceAfterRaw = resourceAfterRaw,
                ChargeCost = chargeCost,
                RefundBeforeCommit = specification.RefundBeforeCommit,
                CooldownMs = cooldownMs,
                CooldownGroupId = cooldownGroupId,
                SharedCooldownMs = Math.Max(0, specification.SharedCooldownMs),
                GlobalCooldownMs = Math.Max(0, specification.GlobalCooldownMs),
                State = MobaSkillEconomyTransactionState.Reserved,
            });
            TryCollectEconomy(context, specification,
                BattleDiagnosticSkillExecutionStage.EconomyReserved,
                resourceType, resourceAmount.RawValue, resourceBeforeRaw, resourceAfterRaw,
                chargeCost, BattleDiagnosticEventOutcome.Succeeded, "reserved");
            return true;
        }

        public bool TryConsumeResource(
            SkillPipelineContext context,
            SkillEconomyPhaseDTO specification,
            out string failure)
        {
            failure = null;
            if (context == null || specification == null)
            {
                failure = "Skill economy resource consumption requires a cast context.";
                return false;
            }

            var resourceType = specification.ResourceType > 0
                ? (ResourceType)specification.ResourceType
                : context.ResolvedConfiguration.ResourceType;
            var amount = specification.UseResolvedResourceCost
                ? context.ResolvedConfiguration.ResourceCost
                : specification.ResourceAmount;
            var succeeded = TryConsumeResource(
                context.CasterActorId,
                resourceType,
                amount > 0f ? MobaResourceFixedConvert.ToFixed(amount) : Fixed64.Zero,
                out failure,
                out var resourceBeforeRaw,
                out var resourceAfterRaw);
            var resourceAmountRaw = amount > 0f
                ? MobaResourceFixedConvert.ToFixed(amount).RawValue
                : 0L;
            TryCollectEconomy(context, specification,
                succeeded
                    ? BattleDiagnosticSkillExecutionStage.ResourceConsumed
                    : BattleDiagnosticSkillExecutionStage.EconomyRejected,
                resourceType, resourceAmountRaw, resourceBeforeRaw, resourceAfterRaw, 0,
                succeeded ? BattleDiagnosticEventOutcome.Succeeded : BattleDiagnosticEventOutcome.Failed,
                succeeded ? "resource consumed" : failure);
            return succeeded;
        }

        public bool Commit(in MobaSkillCastRuntimeHandle handle, string commitId = null)
        {
            if (!TryGetTransaction(in handle, out var transaction)) return false;
            if (transaction.State == MobaSkillEconomyTransactionState.Committed) return true;

            var nowMs = NowMs;
            if (transaction.CooldownMs > 0 && MobaSkillRuntimeAccess.TryGetActiveSkill(
                    _actors, transaction.ActorId, transaction.SkillSlot, transaction.SkillId, out var skill))
            {
                skill.CooldownDurationMs = transaction.CooldownMs;
                skill.CooldownEndTimeMs = nowMs + transaction.CooldownMs;
            }

            if (transaction.CooldownGroupId != 0 && transaction.SharedCooldownMs > 0)
                _cooldownGroups[new ActorGroupKey(transaction.ActorId, transaction.CooldownGroupId)] = nowMs + transaction.SharedCooldownMs;
            if (transaction.GlobalCooldownMs > 0)
                _globalCooldowns[transaction.ActorId] = nowMs + transaction.GlobalCooldownMs;

            transaction.State = MobaSkillEconomyTransactionState.Committed;
            TryCollectEconomy(transaction,
                BattleDiagnosticSkillExecutionStage.EconomyCommitted,
                BattleDiagnosticEventOutcome.Succeeded,
                string.IsNullOrEmpty(commitId) ? "committed" : commitId);
            return true;
        }

        public MobaSkillEconomyAvailability GetAvailability(
            int actorId,
            ActiveSkillRuntime skill,
            int chargeCost,
            string cooldownGroup,
            bool ignoreGlobalCooldown)
        {
            var nowMs = NowMs;
            if (skill != null) RefreshCharges(skill, nowMs);
            return GetAvailability(actorId, skill, chargeCost, StableId(cooldownGroup), ignoreGlobalCooldown, nowMs);
        }

        public long GetCooldownGroupEndTimeMs(int actorId, string cooldownGroup)
        {
            var key = new ActorGroupKey(actorId, StableId(cooldownGroup));
            return key.ActorId > 0 && key.GroupId != 0 && _cooldownGroups.TryGetValue(key, out var end) ? end : 0L;
        }

        public long GetCooldownGroupEndTimeMs(int actorId, int cooldownGroupId)
        {
            var key = new ActorGroupKey(actorId, cooldownGroupId);
            return key.ActorId > 0 && key.GroupId != 0 && _cooldownGroups.TryGetValue(key, out var end) ? end : 0L;
        }

        public long GetGlobalCooldownEndTimeMs(int actorId)
        {
            return actorId > 0 && _globalCooldowns.TryGetValue(actorId, out var end) ? end : 0L;
        }

        public void RemoveActor(int actorId)
        {
            if (actorId <= 0) return;
            _globalCooldowns.Remove(actorId);
            var keys = new List<ActorGroupKey>();
            foreach (var pair in _cooldownGroups)
            {
                if (pair.Key.ActorId == actorId) keys.Add(pair.Key);
            }
            for (var i = 0; i < keys.Count; i++) _cooldownGroups.Remove(keys[i]);

            var runtimeIds = new List<long>();
            foreach (var pair in _transactions)
            {
                if (pair.Value.ActorId == actorId) runtimeIds.Add(pair.Key);
            }
            for (var i = 0; i < runtimeIds.Count; i++) _transactions.Remove(runtimeIds[i]);
        }

        public void OnSkillRuntimeLifecycle(in MobaSkillRuntimeLifecycleEvent lifecycleEvent)
        {
            if (lifecycleEvent.Kind != MobaSkillRuntimeLifecycleEventKind.Finalizing || lifecycleEvent.Runtime == null) return;
            var handle = lifecycleEvent.RuntimeHandle;
            if (!TryGetTransaction(in handle, out var transaction)) return;

            if (lifecycleEvent.Reason == MobaSkillRuntimeEndReason.PipelineCompleted)
            {
                Commit(in handle);
            }
            else if (transaction.State == MobaSkillEconomyTransactionState.Reserved && transaction.RefundBeforeCommit)
            {
                Refund(transaction, out var refundBeforeRaw, out var refundAfterRaw);
                transaction.ResourceBeforeRaw = refundBeforeRaw;
                transaction.ResourceAfterRaw = refundAfterRaw;
                TryCollectEconomy(transaction,
                    BattleDiagnosticSkillExecutionStage.EconomyRefunded,
                    BattleDiagnosticEventOutcome.Succeeded,
                    lifecycleEvent.Reason.ToString());
            }

            _transactions.Remove(handle.RuntimeId);
        }

        public void Dispose()
        {
            _runtimes.LifecycleHooks.Unregister(this);
            _transactions.Clear();
            _cooldownGroups.Clear();
            _globalCooldowns.Clear();
        }

        internal MobaSkillEconomyServiceSnapshot CaptureRollbackSnapshot()
        {
            var transactions = new List<MobaSkillEconomyTransactionSnapshot>(_transactions.Count);
            foreach (var pair in _transactions)
            {
                var value = pair.Value;
                transactions.Add(new MobaSkillEconomyTransactionSnapshot(
                    in value.Handle, value.ActorId, value.SkillId, value.SkillSlot,
                    (int)value.ResourceType, value.ResourceAmount.RawValue, value.ChargeCost,
                    value.RefundBeforeCommit, value.CooldownMs, value.CooldownGroupId,
                    value.SharedCooldownMs, value.GlobalCooldownMs, value.State));
            }
            transactions.Sort((left, right) => left.Handle.RuntimeId.CompareTo(right.Handle.RuntimeId));

            var cooldowns = new List<MobaSkillEconomyCooldownSnapshot>(_cooldownGroups.Count);
            foreach (var pair in _cooldownGroups)
                cooldowns.Add(new MobaSkillEconomyCooldownSnapshot(pair.Key.ActorId, pair.Key.GroupId, pair.Value));
            cooldowns.Sort(CompareCooldowns);

            var globals = new List<MobaSkillEconomyCooldownSnapshot>(_globalCooldowns.Count);
            foreach (var pair in _globalCooldowns)
                globals.Add(new MobaSkillEconomyCooldownSnapshot(pair.Key, 0, pair.Value));
            globals.Sort(CompareCooldowns);
            return new MobaSkillEconomyServiceSnapshot(transactions.ToArray(), cooldowns.ToArray(), globals.ToArray());
        }

        internal void RestoreRollbackSnapshot(in MobaSkillEconomyServiceSnapshot snapshot)
        {
            _transactions.Clear();
            _cooldownGroups.Clear();
            _globalCooldowns.Clear();
            var transactions = snapshot.Transactions ?? Array.Empty<MobaSkillEconomyTransactionSnapshot>();
            var cooldowns = snapshot.Cooldowns ?? Array.Empty<MobaSkillEconomyCooldownSnapshot>();
            var globalCooldowns = snapshot.GlobalCooldowns ?? Array.Empty<MobaSkillEconomyCooldownSnapshot>();
            for (var i = 0; i < transactions.Length; i++)
            {
                var value = transactions[i];
                if (!value.Handle.IsValid) continue;
                _transactions[value.Handle.RuntimeId] = new Transaction
                {
                    Handle = value.Handle,
                    ActorId = value.ActorId,
                    SkillId = value.SkillId,
                    SkillSlot = value.SkillSlot,
                    ResourceType = (ResourceType)value.ResourceType,
                    ResourceAmount = Fixed64.FromRaw(value.ResourceAmountRaw),
                    ChargeCost = value.ChargeCost,
                    RefundBeforeCommit = value.RefundBeforeCommit,
                    CooldownMs = value.CooldownMs,
                    CooldownGroupId = value.CooldownGroupId,
                    SharedCooldownMs = value.SharedCooldownMs,
                    GlobalCooldownMs = value.GlobalCooldownMs,
                    State = value.State,
                };
            }
            for (var i = 0; i < cooldowns.Length; i++)
            {
                var value = cooldowns[i];
                _cooldownGroups[new ActorGroupKey(value.ActorId, value.GroupId)] = value.EndTimeMs;
            }
            for (var i = 0; i < globalCooldowns.Length; i++)
                _globalCooldowns[globalCooldowns[i].ActorId] = globalCooldowns[i].EndTimeMs;
        }

        private long NowMs => MobaSkillRuntimeAccess.GetCurrentTimeMs(_time);

        private MobaSkillEconomyAvailability GetAvailability(
            int actorId, ActiveSkillRuntime skill, int chargeCost, int cooldownGroupId,
            bool ignoreGlobalCooldown, long nowMs)
        {
            if (skill == null) return new MobaSkillEconomyAvailability(false, "Active skill runtime is unavailable.", 0, 0L);
            RefreshCharges(skill, nowMs);
            if (skill.CooldownEndTimeMs > nowMs)
                return new MobaSkillEconomyAvailability(false, "Skill is cooling down.", skill.CurrentCharges, skill.CooldownEndTimeMs);
            if (chargeCost > 0 && skill.CurrentCharges < chargeCost)
                return new MobaSkillEconomyAvailability(false, "Skill has no available charges.", skill.CurrentCharges, skill.NextChargeRecoveryTimeMs);
            if (cooldownGroupId != 0 && _cooldownGroups.TryGetValue(new ActorGroupKey(actorId, cooldownGroupId), out var groupEnd) && groupEnd > nowMs)
                return new MobaSkillEconomyAvailability(false, "Shared cooldown group is active.", skill.CurrentCharges, groupEnd);
            if (!ignoreGlobalCooldown && _globalCooldowns.TryGetValue(actorId, out var globalEnd) && globalEnd > nowMs)
                return new MobaSkillEconomyAvailability(false, "Global cooldown is active.", skill.CurrentCharges, globalEnd);
            return new MobaSkillEconomyAvailability(true, null, skill.CurrentCharges, nowMs);
        }

        private bool TryConsumeResource(
            int actorId,
            ResourceType resourceType,
            Fixed64 amount,
            out string failure,
            out long beforeRaw,
            out long afterRaw)
        {
            failure = null;
            beforeRaw = 0L;
            afterRaw = 0L;
            if (amount <= Fixed64.Zero) return true;
            if (resourceType == ResourceType.None || !_actors.TryGetActorEntity(actorId, out var actor) ||
                actor == null || !actor.hasResourceContainer || actor.resourceContainer.Value?.Map == null ||
                !actor.resourceContainer.Value.Map.TryGetValue(resourceType, out var resource) || resource == null)
            {
                failure = "Required skill resource is unavailable.";
                return false;
            }
            beforeRaw = resource.Current.RawValue;
            afterRaw = beforeRaw;
            if (resource.Current < amount)
            {
                failure = "Insufficient skill resource.";
                return false;
            }
            resource.Current -= amount;
            afterRaw = resource.Current.RawValue;
            return true;
        }

        private void Refund(Transaction transaction, out long beforeRaw, out long afterRaw)
        {
            beforeRaw = 0L;
            afterRaw = 0L;
            if (transaction.ResourceAmount > Fixed64.Zero &&
                _actors.TryGetActorEntity(transaction.ActorId, out var actor) && actor != null &&
                actor.hasResourceContainer && actor.resourceContainer.Value?.Map != null &&
                actor.resourceContainer.Value.Map.TryGetValue(transaction.ResourceType, out var resource) && resource != null)
            {
                beforeRaw = resource.Current.RawValue;
                resource.Current += transaction.ResourceAmount;
                if (resource.LastMax > Fixed64.Zero && resource.Current > resource.LastMax) resource.Current = resource.LastMax;
                afterRaw = resource.Current.RawValue;
            }

            if (transaction.ChargeCost > 0 && MobaSkillRuntimeAccess.TryGetActiveSkill(
                    _actors, transaction.ActorId, transaction.SkillSlot, transaction.SkillId, out var skill))
            {
                skill.CurrentCharges = Math.Min(Math.Max(1, skill.MaxCharges), skill.CurrentCharges + transaction.ChargeCost);
                if (skill.CurrentCharges >= skill.MaxCharges) skill.NextChargeRecoveryTimeMs = 0L;
            }
        }

        private void TryCollectEconomy(
            SkillPipelineContext context,
            SkillEconomyPhaseDTO specification,
            BattleDiagnosticSkillExecutionStage stage,
            ResourceType resourceType,
            long resourceAmountRaw,
            long resourceBeforeRaw,
            long resourceAfterRaw,
            int chargeCost,
            BattleDiagnosticEventOutcome outcome,
            string detail)
        {
            try
            {
                if (context == null || specification == null) return;
                var data = new BattleDiagnosticSkillExecutionPayload(
                    context.DiagnosticCommandId, stage, context.SkillSlot, context.SkillLevel,
                    context.CastSequence, resourceType: (int)resourceType,
                    resourceAmountRaw: resourceAmountRaw,
                    resourceBeforeRaw: resourceBeforeRaw,
                    resourceAfterRaw: resourceAfterRaw,
                    chargeCost: chargeCost,
                    cooldownMs: specification.StartSkillCooldown
                        ? Math.Max(0, specification.UseResolvedSkillCooldown
                            ? context.ResolvedConfiguration.CooldownMs
                            : specification.SkillCooldownMs)
                        : 0,
                    sharedCooldownMs: Math.Max(0, specification.SharedCooldownMs),
                    globalCooldownMs: Math.Max(0, specification.GlobalCooldownMs),
                    detail: detail);
                var runtimeHandle = context.RuntimeHandle;
                TryCollectEconomy(context.CasterActorId, context.TargetActorId, context.SkillId,
                    in runtimeHandle, in data, outcome);
            }
            catch
            {
                // Diagnostics must never change economy transaction semantics.
            }
        }

        private void TryCollectEconomy(
            Transaction transaction,
            BattleDiagnosticSkillExecutionStage stage,
            BattleDiagnosticEventOutcome outcome,
            string detail)
        {
            try
            {
                if (transaction == null) return;
                var data = new BattleDiagnosticSkillExecutionPayload(
                    transaction.DiagnosticCommandId, stage, transaction.SkillSlot,
                    transaction.SkillLevel, transaction.CastSequence,
                    resourceType: (int)transaction.ResourceType,
                    resourceAmountRaw: transaction.ResourceAmount.RawValue,
                    resourceBeforeRaw: transaction.ResourceBeforeRaw,
                    resourceAfterRaw: transaction.ResourceAfterRaw,
                    chargeCost: transaction.ChargeCost,
                    cooldownMs: transaction.CooldownMs,
                    sharedCooldownMs: transaction.SharedCooldownMs,
                    globalCooldownMs: transaction.GlobalCooldownMs,
                    detail: detail);
                TryCollectEconomy(transaction.ActorId, 0, transaction.SkillId,
                    in transaction.Handle, in data, outcome);
            }
            catch
            {
                // Diagnostics must never change economy transaction semantics.
            }
        }

        private void TryCollectEconomy(
            int actorId,
            int targetActorId,
            int skillId,
            in MobaSkillCastRuntimeHandle runtimeHandle,
            in BattleDiagnosticSkillExecutionPayload data,
            BattleDiagnosticEventOutcome outcome)
        {
            try
            {
                var sink = _diagnosticEvents;
                if (sink == null || !sink.IsEnabled(BattleDiagnosticEventChannel.Skill)) return;
                var payload = BattleDiagnosticEventPayload.FromSkillExecution(in data);
                var runtime = runtimeHandle.IsValid
                    ? new BattleDiagnosticRuntimeHandle(runtimeHandle.RuntimeId, runtimeHandle.Generation)
                    : default;
                var draft = new MobaBattleDiagnosticEventDraft(
                    BattleDiagnosticEventKind.SkillEconomy,
                    BattleDiagnosticEventChannel.Skill,
                    outcome,
                    sourceActorId: actorId,
                    targetActorId: targetActorId,
                    configId: skillId,
                    rootContextId: runtimeHandle.RootTraceContextId,
                    contextId: runtimeHandle.RootTraceContextId,
                    skillRuntime: runtime,
                    payloadVersion: BattleDiagnosticSkillExecutionPayload.CurrentSchemaVersion,
                    summary: $"{data.Stage} resource={data.ResourceBeforeRaw}->{data.ResourceAfterRaw} " +
                             $"cooldown={data.CooldownMs}/{data.SharedCooldownMs}/{data.GlobalCooldownMs}",
                    payload: payload);
                sink.TryCollect(in draft);
            }
            catch
            {
                // Diagnostics must never change economy transaction semantics.
            }
        }

        private bool TryGetTransaction(in MobaSkillCastRuntimeHandle handle, out Transaction transaction)
        {
            transaction = null;
            return handle.IsValid && _transactions.TryGetValue(handle.RuntimeId, out transaction) &&
                   transaction != null && transaction.Handle.Equals(handle);
        }

        private static void ConfigureAndRefreshCharges(ActiveSkillRuntime skill, int maxCharges, int recoveryMs, long nowMs)
        {
            var configuredMax = Math.Max(1, maxCharges);
            if (!skill.ChargesConfigured || skill.MaxCharges <= 0)
            {
                skill.MaxCharges = configuredMax;
                skill.CurrentCharges = configuredMax;
                skill.ChargesConfigured = true;
            }
            else if (skill.MaxCharges != configuredMax)
            {
                skill.MaxCharges = configuredMax;
                skill.CurrentCharges = Math.Min(skill.CurrentCharges, configuredMax);
            }
            skill.ChargeRecoveryMs = Math.Max(0, recoveryMs);
            RefreshCharges(skill, nowMs);
        }

        private static void ConsumeCharges(ActiveSkillRuntime skill, int chargeCost, long nowMs)
        {
            skill.CurrentCharges = Math.Max(0, skill.CurrentCharges - chargeCost);
            if (skill.CurrentCharges < skill.MaxCharges && skill.ChargeRecoveryMs > 0 && skill.NextChargeRecoveryTimeMs <= 0L)
                skill.NextChargeRecoveryTimeMs = nowMs + skill.ChargeRecoveryMs;
        }

        public static void RefreshCharges(ActiveSkillRuntime skill, long nowMs)
        {
            if (skill == null || skill.MaxCharges <= 0) return;
            skill.CurrentCharges = Math.Min(skill.MaxCharges, Math.Max(0, skill.CurrentCharges));
            if (skill.CurrentCharges >= skill.MaxCharges)
            {
                skill.NextChargeRecoveryTimeMs = 0L;
                return;
            }
            if (skill.ChargeRecoveryMs <= 0) return;
            if (skill.NextChargeRecoveryTimeMs <= 0L) skill.NextChargeRecoveryTimeMs = nowMs + skill.ChargeRecoveryMs;
            if (nowMs < skill.NextChargeRecoveryTimeMs) return;

            var recovered = 1L + (nowMs - skill.NextChargeRecoveryTimeMs) / skill.ChargeRecoveryMs;
            skill.CurrentCharges = Math.Min(skill.MaxCharges, skill.CurrentCharges + (int)Math.Min(int.MaxValue, recovered));
            skill.NextChargeRecoveryTimeMs = skill.CurrentCharges >= skill.MaxCharges
                ? 0L
                : skill.NextChargeRecoveryTimeMs + recovered * skill.ChargeRecoveryMs;
        }

        private static int StableId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? 0 : StableStringId.Get("skill.cooldown.group:" + value.Trim());
        }

        private static int CompareCooldowns(MobaSkillEconomyCooldownSnapshot left, MobaSkillEconomyCooldownSnapshot right)
        {
            var actor = left.ActorId.CompareTo(right.ActorId);
            return actor != 0 ? actor : left.GroupId.CompareTo(right.GroupId);
        }
    }
}
