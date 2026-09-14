using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateSync;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    public sealed class MobaSkillEconomyRollbackProvider : IRollbackStateProvider, IMobaStateRecoveryProvider
    {
        public const int DefaultKey = 10014;
        private readonly MobaSkillEconomyService _economy;

        public MobaSkillEconomyRollbackProvider(MobaSkillEconomyService economy)
        {
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
        }

        public int Key => DefaultKey;
        public string Name => "SkillEconomy";
        public byte[] Export(FrameIndex frame) => ExportState(frame);
        public void Import(FrameIndex frame, byte[] payload) => ImportState(frame, payload);

        public byte[] ExportState(FrameIndex frame)
        {
            var snapshot = _economy.CaptureRollbackSnapshot();
            var transactions = new MobaSkillEconomyTransactionRollbackEntry[snapshot.Transactions.Length];
            for (var i = 0; i < transactions.Length; i++)
            {
                var value = snapshot.Transactions[i];
                transactions[i] = new MobaSkillEconomyTransactionRollbackEntry(
                    value.Handle.RuntimeId, value.Handle.Generation, value.Handle.RootTraceContextId,
                    value.ActorId, value.SkillId, value.SkillSlot, value.ResourceType,
                    value.ResourceAmountRaw, value.ChargeCost, value.RefundBeforeCommit,
                    value.CooldownMs, value.CooldownGroupId, value.SharedCooldownMs,
                    value.GlobalCooldownMs, (int)value.State);
            }
            var cooldowns = ToEntries(snapshot.Cooldowns);
            var globalCooldowns = ToEntries(snapshot.GlobalCooldowns);
            return MemoryPackSerializer.Serialize(new MobaSkillEconomyRollbackPayload(1, transactions, cooldowns, globalCooldowns));
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                _economy.RestoreRollbackSnapshot(default);
                return;
            }
            var state = MemoryPackSerializer.Deserialize<MobaSkillEconomyRollbackPayload>(payload);
            if (state.Version != 1) throw new InvalidOperationException($"Unsupported skill economy rollback payload version '{state.Version}'.");
            var source = state.Transactions ?? Array.Empty<MobaSkillEconomyTransactionRollbackEntry>();
            var transactions = new MobaSkillEconomyTransactionSnapshot[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                var value = source[i];
                var handle = new MobaSkillCastRuntimeHandle(value.RuntimeId, value.Generation, value.RootTraceContextId);
                transactions[i] = new MobaSkillEconomyTransactionSnapshot(
                    in handle, value.ActorId, value.SkillId, value.SkillSlot, value.ResourceType,
                    value.ResourceAmountRaw, value.ChargeCost, value.RefundBeforeCommit,
                    value.CooldownMs, value.CooldownGroupId, value.SharedCooldownMs,
                    value.GlobalCooldownMs, (MobaSkillEconomyTransactionState)value.State);
            }
            _economy.RestoreRollbackSnapshot(new MobaSkillEconomyServiceSnapshot(
                transactions, FromEntries(state.Cooldowns), FromEntries(state.GlobalCooldowns)));
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            var payload = ExportState(frame);
            hash.AddInt(Key);
            hash.AddInt(payload.Length);
            for (var i = 0; i < payload.Length; i++) hash.AddByte(payload[i]);
        }

        private static MobaSkillEconomyCooldownRollbackEntry[] ToEntries(MobaSkillEconomyCooldownSnapshot[] source)
        {
            source ??= Array.Empty<MobaSkillEconomyCooldownSnapshot>();
            var result = new MobaSkillEconomyCooldownRollbackEntry[source.Length];
            for (var i = 0; i < source.Length; i++)
                result[i] = new MobaSkillEconomyCooldownRollbackEntry(source[i].ActorId, source[i].GroupId, source[i].EndTimeMs);
            return result;
        }

        private static MobaSkillEconomyCooldownSnapshot[] FromEntries(MobaSkillEconomyCooldownRollbackEntry[] source)
        {
            source ??= Array.Empty<MobaSkillEconomyCooldownRollbackEntry>();
            var result = new MobaSkillEconomyCooldownSnapshot[source.Length];
            for (var i = 0; i < source.Length; i++)
                result[i] = new MobaSkillEconomyCooldownSnapshot(source[i].ActorId, source[i].GroupId, source[i].EndTimeMs);
            return result;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaSkillEconomyRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly MobaSkillEconomyTransactionRollbackEntry[] Transactions;
        [MemoryPackOrder(2)] public readonly MobaSkillEconomyCooldownRollbackEntry[] Cooldowns;
        [MemoryPackOrder(3)] public readonly MobaSkillEconomyCooldownRollbackEntry[] GlobalCooldowns;
        [MemoryPackConstructor]
        public MobaSkillEconomyRollbackPayload(int version, MobaSkillEconomyTransactionRollbackEntry[] transactions, MobaSkillEconomyCooldownRollbackEntry[] cooldowns, MobaSkillEconomyCooldownRollbackEntry[] globalCooldowns)
        { Version=version;Transactions=transactions;Cooldowns=cooldowns;GlobalCooldowns=globalCooldowns; }
    }

    [MemoryPackable]
    public readonly partial struct MobaSkillEconomyTransactionRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly long RuntimeId; [MemoryPackOrder(1)] public readonly int Generation;
        [MemoryPackOrder(2)] public readonly long RootTraceContextId; [MemoryPackOrder(3)] public readonly int ActorId;
        [MemoryPackOrder(4)] public readonly int SkillId; [MemoryPackOrder(5)] public readonly int SkillSlot;
        [MemoryPackOrder(6)] public readonly int ResourceType; [MemoryPackOrder(7)] public readonly long ResourceAmountRaw;
        [MemoryPackOrder(8)] public readonly int ChargeCost; [MemoryPackOrder(9)] public readonly bool RefundBeforeCommit;
        [MemoryPackOrder(10)] public readonly int CooldownMs; [MemoryPackOrder(11)] public readonly int CooldownGroupId;
        [MemoryPackOrder(12)] public readonly int SharedCooldownMs; [MemoryPackOrder(13)] public readonly int GlobalCooldownMs;
        [MemoryPackOrder(14)] public readonly int State;
        public MobaSkillEconomyTransactionRollbackEntry(long runtimeId,int generation,long rootTraceContextId,int actorId,int skillId,int skillSlot,int resourceType,long resourceAmountRaw,int chargeCost,bool refundBeforeCommit,int cooldownMs,int cooldownGroupId,int sharedCooldownMs,int globalCooldownMs,int state)
        {RuntimeId=runtimeId;Generation=generation;RootTraceContextId=rootTraceContextId;ActorId=actorId;SkillId=skillId;SkillSlot=skillSlot;ResourceType=resourceType;ResourceAmountRaw=resourceAmountRaw;ChargeCost=chargeCost;RefundBeforeCommit=refundBeforeCommit;CooldownMs=cooldownMs;CooldownGroupId=cooldownGroupId;SharedCooldownMs=sharedCooldownMs;GlobalCooldownMs=globalCooldownMs;State=state;}
    }

    [MemoryPackable]
    public readonly partial struct MobaSkillEconomyCooldownRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly int ActorId;
        [MemoryPackOrder(1)] public readonly int GroupId;
        [MemoryPackOrder(2)] public readonly long EndTimeMs;
        public MobaSkillEconomyCooldownRollbackEntry(int actorId, int groupId, long endTimeMs)
        { ActorId=actorId;GroupId=groupId;EndTimeMs=endTimeMs; }
    }
}
