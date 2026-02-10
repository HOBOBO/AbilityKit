using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan.Json;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaOngoingTriggerPlanService : IService
    {
        private readonly TriggerPlanJsonDatabase _db;
        private readonly TriggerRunner<AbilityKit.Ability.World.DI.IWorldResolver> _runner;

        private readonly Dictionary<int, TriggerPlanJsonDatabase.Record> _byTriggerId = new Dictionary<int, TriggerPlanJsonDatabase.Record>();
        private readonly Dictionary<long, List<IDisposable>> _regsByOwnerKey = new Dictionary<long, List<IDisposable>>();

        public bool ContainsOwnerKey(long ownerKey)
        {
            return ownerKey != 0 && _regsByOwnerKey.ContainsKey(ownerKey);
        }

        public void CopyActiveOwnerKeys(List<long> dest)
        {
            if (dest == null) return;
            dest.Clear();
            foreach (var kv in _regsByOwnerKey) dest.Add(kv.Key);
        }

        public MobaOngoingTriggerPlanService(TriggerPlanJsonDatabase db, TriggerRunner<AbilityKit.Ability.World.DI.IWorldResolver> runner)
        {
            _db = db;
            _runner = runner;

            try
            {
                var records = _db?.Records;
                if (records != null)
                {
                    for (int i = 0; i < records.Count; i++)
                    {
                        var r = records[i];
                        if (r.TriggerId <= 0) continue;
                        _byTriggerId[r.TriggerId] = r;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[MobaOngoingTriggerPlanService] build triggerId map failed");
            }
        }

        public void StartTriggers(IReadOnlyList<int> triggerIds, long ownerKey)
        {
            if (ownerKey == 0) return;
            if (triggerIds == null || triggerIds.Count == 0) return;
            if (_db == null || _runner == null) return;

            if (!_regsByOwnerKey.TryGetValue(ownerKey, out var regs) || regs == null)
            {
                regs = new List<IDisposable>(triggerIds.Count);
                _regsByOwnerKey[ownerKey] = regs;
            }

            for (int i = 0; i < triggerIds.Count; i++)
            {
                var triggerId = triggerIds[i];
                if (triggerId <= 0) continue;
                if (!_byTriggerId.TryGetValue(triggerId, out var record))
                {
                    Log.Warning($"[MobaOngoingTriggerPlanService] triggerId not found in plan db: {triggerId}");
                    continue;
                }

                if (record.EventId == 0)
                {
                    Log.Warning($"[MobaOngoingTriggerPlanService] triggerId has empty eventId: {triggerId}");
                    continue;
                }

                try
                {
                    var key = new EventKey<object>(record.EventId);
                    var reg = _runner.RegisterPlan<object, AbilityKit.Ability.World.DI.IWorldResolver>(key, record.Plan);
                    regs.Add(reg);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[MobaOngoingTriggerPlanService] register plan failed. triggerId={triggerId}");
                }
            }
        }

        public void Stop(long ownerKey)
        {
            if (ownerKey == 0) return;
            if (!_regsByOwnerKey.TryGetValue(ownerKey, out var regs) || regs == null) return;

            _regsByOwnerKey.Remove(ownerKey);

            for (int i = 0; i < regs.Count; i++)
            {
                try
                {
                    regs[i]?.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[MobaOngoingTriggerPlanService] dispose reg failed. ownerKey={ownerKey}");
                }
            }
        }

        public void Dispose()
        {
            var keys = new List<long>(_regsByOwnerKey.Keys);
            for (int i = 0; i < keys.Count; i++) Stop(keys[i]);
        }
    }
}
