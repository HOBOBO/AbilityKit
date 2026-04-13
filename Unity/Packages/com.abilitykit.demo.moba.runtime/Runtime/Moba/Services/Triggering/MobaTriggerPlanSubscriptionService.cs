using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Core.Common.Log;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Common.Event;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaTriggerPlanSubscriptionService : IService
    {
        private readonly TriggerPlanJsonDatabase _db;
        private readonly TriggerRunner<AbilityKit.Ability.World.DI.IWorldResolver> _runner;
        private readonly IWorldResolver _services;

        private readonly Dictionary<int, TriggerPlanJsonDatabase.Record> _byTriggerId = new Dictionary<int, TriggerPlanJsonDatabase.Record>();
        private readonly Dictionary<long, List<IDisposable>> _regsByOwnerKey = new Dictionary<long, List<IDisposable>>();

        private static readonly MethodInfo RegisterTypedMethod = typeof(MobaTriggerPlanSubscriptionService).GetMethod(nameof(RegisterTyped), BindingFlags.NonPublic | BindingFlags.Static);

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

        public MobaTriggerPlanSubscriptionService(TriggerPlanJsonDatabase db, TriggerRunner<AbilityKit.Ability.World.DI.IWorldResolver> runner, IWorldResolver services)
        {
            _db = db;
            _runner = runner;
            _services = services;

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
                Log.Exception(ex, "[MobaTriggerPlanSubscriptionService] build triggerId map failed");
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
                    Log.Warning($"[MobaTriggerPlanSubscriptionService] triggerId not found in plan db: {triggerId}");
                    continue;
                }

                if (record.EventId == 0)
                {
                    Log.Warning($"[MobaTriggerPlanSubscriptionService] triggerId has empty eventId: {triggerId}");
                    continue;
                }

                try
                {
                    var subReg = TryRegisterTyped(record);
                    if (subReg != null)
                    {
                        regs.Add(subReg);
                        continue;
                    }

                    var key = new EventKey<object>(record.EventId);
                    var reg = _runner.RegisterPlan<object, AbilityKit.Ability.World.DI.IWorldResolver>(key, record.Plan);
                    regs.Add(reg);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[MobaTriggerPlanSubscriptionService] register plan failed. triggerId={triggerId}");
                }
            }
        }

        private IDisposable TryRegisterTyped(in TriggerPlanJsonDatabase.Record record)
        {
            try
            {
                if (string.IsNullOrEmpty(record.EventName)) return null;
                if (_services == null) return null;
                if (!_services.TryResolve<MobaEventSubscriptionRegistry>(out var reg) || reg == null) return null;
                if (!reg.TryGetArgsType(record.EventName, out var argsType) || argsType == null) return null;

                if (RegisterTypedMethod == null)
                {
                    Log.Warning("[MobaTriggerPlanSubscriptionService] RegisterTyped method not found; fallback to object channel");
                    return null;
                }

                var mi = RegisterTypedMethod.MakeGenericMethod(argsType);
                return (IDisposable)mi.Invoke(null, new object[] { _runner, record.EventId, record.Plan });
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[MobaTriggerPlanSubscriptionService] typed register failed. eventName={record.EventName} eid={record.EventId}");
                return null;
            }
        }

        private static IDisposable RegisterTyped<TArgs>(TriggerRunner<IWorldResolver> runner, int eid, TriggerPlan<object> planObj)
        {
            if (runner == null) return null;
            if (eid == 0) return null;

            var plan = ConvertPlan<TArgs>(in planObj);
            var key = new EventKey<TArgs>(eid);
            return runner.RegisterPlan<TArgs, IWorldResolver>(key, in plan);
        }

        private static TriggerPlan<TArgs> ConvertPlan<TArgs>(in TriggerPlan<object> src)
        {
            var actions = src.Actions;

            if (!src.HasPredicate || src.PredicateKind == EPredicateKind.None)
            {
                return new TriggerPlan<TArgs>(src.Phase, src.Priority, src.TriggerId, src.InterruptPriority, actions);
            }

            if (src.PredicateKind == EPredicateKind.Expr)
            {
                return new TriggerPlan<TArgs>(src.Phase, src.Priority, src.TriggerId, src.PredicateExpr, src.InterruptPriority, actions);
            }

            switch (src.PredicateArity)
            {
                case 0:
                    return new TriggerPlan<TArgs>(src.Phase, src.Priority, src.TriggerId, src.PredicateId, src.InterruptPriority, actions);
                case 1:
                    return new TriggerPlan<TArgs>(src.Phase, src.Priority, src.TriggerId, src.PredicateId, src.PredicateArg0, src.InterruptPriority, actions);
                case 2:
                    return new TriggerPlan<TArgs>(src.Phase, src.Priority, src.TriggerId, src.PredicateId, src.PredicateArg0, src.PredicateArg1, src.InterruptPriority, actions);
                default:
                    return new TriggerPlan<TArgs>(src.Phase, src.Priority, src.TriggerId, src.InterruptPriority, actions);
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
                    Log.Exception(ex, $"[MobaTriggerPlanSubscriptionService] dispose reg failed. ownerKey={ownerKey}");
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
