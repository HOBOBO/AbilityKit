using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.Core;

namespace AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering
{
    /// <summary>
    /// 收敛持续触发计划的所有者绑定与失效清理逻辑，避免调度系统直接编排网关细节。
    /// </summary>
    [WorldService(typeof(MobaTriggerPlanReconcileService))]
    public sealed class MobaTriggerPlanReconcileService : IService, IDisposable
    {
        [WorldInject(required: false)] private MobaTriggerExecutionGateway _triggers = null;

        // The hash is only a fast rejection check. Keep the exact ids as well so
        // a 32-bit collision cannot leave an outdated owner subscription active.
        private readonly Dictionary<long, TriggerPlanBindingSnapshot> _bindingByOwnerKey = new Dictionary<long, TriggerPlanBindingSnapshot>();
        private readonly HashSet<long> _desiredKeys = new HashSet<long>();
        private readonly List<long> _tmpKeys = new List<long>(64);

        public void Reconcile(IReadOnlyList<OngoingTriggerPlanEntry> activePlans)
        {
            if (_triggers == null) return;

            _desiredKeys.Clear();

            if (activePlans != null && activePlans.Count > 0)
            {
                for (int i = 0; i < activePlans.Count; i++)
                {
                    var entry = activePlans[i];
                    if (entry == null) continue;

                    var ownerKey = entry.OwnerKey;
                    if (ownerKey == 0) continue;

                    var triggerIds = entry.TriggerIds;
                    _desiredKeys.Add(ownerKey);

                    if (triggerIds == null || triggerIds.Length == 0)
                    {
                        _triggers.StopOwnerBoundTriggers(ownerKey, "ongoing.reconcile.empty");
                        _bindingByOwnerKey.Remove(ownerKey);
                        continue;
                    }

                    var hash = ComputeHash(triggerIds);
                    if (!_bindingByOwnerKey.TryGetValue(ownerKey, out var previous)
                        || previous.Hash != hash
                        || !SequenceEqual(previous.TriggerIds, triggerIds))
                    {
                        _triggers.ApplyOwnerBoundTriggers(triggerIds, ownerKey, "ongoing.reconcile.apply");
                        _bindingByOwnerKey[ownerKey] = new TriggerPlanBindingSnapshot(hash, triggerIds);
                    }
                }
            }

            _tmpKeys.Clear();
            foreach (var ownerKey in _bindingByOwnerKey.Keys)
            {
                if (!_desiredKeys.Contains(ownerKey)) _tmpKeys.Add(ownerKey);
            }

            for (int i = 0; i < _tmpKeys.Count; i++)
            {
                var ownerKey = _tmpKeys[i];
                _triggers.StopOwnerBoundTriggers(ownerKey, "ongoing.reconcile.stale");
                _bindingByOwnerKey.Remove(ownerKey);
            }
        }

        public void Dispose()
        {
            _bindingByOwnerKey.Clear();
            _desiredKeys.Clear();
            _tmpKeys.Clear();
        }

        private sealed class TriggerPlanBindingSnapshot
        {
            public readonly int Hash;
            public readonly int[] TriggerIds;

            public TriggerPlanBindingSnapshot(int hash, IReadOnlyList<int> triggerIds)
            {
                Hash = hash;
                TriggerIds = new int[triggerIds.Count];
                for (var i = 0; i < triggerIds.Count; i++) TriggerIds[i] = triggerIds[i];
            }
        }

        private static bool SequenceEqual(int[] left, IReadOnlyList<int> right)
        {
            if (left == null || right == null || left.Length != right.Count) return false;
            for (var i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i]) return false;
            }

            return true;
        }

        private static int ComputeHash(int[] ids)
        {
            unchecked
            {
                var h = 17;
                for (int i = 0; i < ids.Length; i++) h = h * 31 + ids[i];
                return h;
            }
        }
    }
}
