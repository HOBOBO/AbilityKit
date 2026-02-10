using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.Impl.Moba.Conponents;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Triggering
{
    [WorldSystem(order: MobaSystemOrder.OngoingTriggerPlansReconcile, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaOngoingTriggerPlansReconcileSystem : WorldSystemBase
    {
        private MobaOngoingTriggerPlanService _plans;
        private global::Entitas.IGroup<global::ActorEntity> _group;

        private readonly Dictionary<long, int> _hashByOwnerKey = new Dictionary<long, int>();
        private readonly HashSet<long> _desiredKeys = new HashSet<long>();
        private readonly List<long> _tmpKeys = new List<long>(64);

        public MobaOngoingTriggerPlansReconcileSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _plans);
            _group = Contexts.Actor().GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId, ActorComponentsLookup.OngoingTriggerPlans));
        }

        protected override void OnExecute()
        {
            if (_plans == null) return;

            _desiredKeys.Clear();

            var entities = _group.GetEntities();
            if (entities != null && entities.Length > 0)
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    if (e == null || !e.hasOngoingTriggerPlans) continue;

                    var comp = e.ongoingTriggerPlans;
                    var list = comp.Active;
                    if (list == null || list.Count == 0) continue;

                    for (int j = 0; j < list.Count; j++)
                    {
                        var entry = list[j];
                        if (entry == null) continue;

                        var ownerKey = entry.OwnerKey;
                        if (ownerKey == 0) continue;

                        var triggerIds = entry.TriggerIds;
                        if (triggerIds == null || triggerIds.Length == 0)
                        {
                            _desiredKeys.Add(ownerKey);
                            continue;
                        }

                        _desiredKeys.Add(ownerKey);

                        var hash = ComputeHash(triggerIds);
                        if (!_hashByOwnerKey.TryGetValue(ownerKey, out var oldHash) || oldHash != hash)
                        {
                            _plans.Stop(ownerKey);
                            _plans.StartTriggers(triggerIds, ownerKey);
                            _hashByOwnerKey[ownerKey] = hash;
                        }
                    }
                }
            }

            _plans.CopyActiveOwnerKeys(_tmpKeys);
            for (int i = 0; i < _tmpKeys.Count; i++)
            {
                var ownerKey = _tmpKeys[i];
                if (_desiredKeys.Contains(ownerKey)) continue;

                _plans.Stop(ownerKey);
                _hashByOwnerKey.Remove(ownerKey);
            }
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
