using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.Moba.Conponents;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.Share.Impl.Moba;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    [WorldSystem(order: MobaSystemOrder.EffectListeners, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaEffectListenerRegisterSystem : WorldSystemBase
    {
        private TriggerRunner _triggers;
        private TriggerRegistry _registry;
        private TriggerCompiler _compiler;

        private global::Entitas.IGroup<global::ActorEntity> _group;

        public MobaEffectListenerRegisterSystem(global::Entitas.IContexts contexts, IWorldServices services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryGet(out _triggers);
            Services.TryGet(out _registry);
            if (_registry != null)
            {
                _compiler = new TriggerCompiler(_registry);
            }

            _group = Contexts.Actor().GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId, ActorComponentsLookup.EffectListeners));
        }

        protected override void OnExecute()
        {
            if (_triggers == null || _compiler == null) return;

            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0) return;

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null || !e.hasActorId || !e.hasEffectListeners) continue;

                var list = e.effectListeners.Active;
                if (list == null || list.Count == 0) continue;

                var ownerActorId = e.actorId.Value;

                for (int j = 0; j < list.Count; j++)
                {
                    var l = list[j];
                    if (l == null) continue;

                    if (l.Sub != null) continue;

                    if (string.IsNullOrEmpty(l.EventId) || l.ExecuteEffectId <= 0)
                    {
                        continue;
                    }

                    l.OwnerActorId = ownerActorId;

                    try
                    {
                        var def = BuildTriggerDef(l);
                        var instance = _compiler.Compile(def);
                        l.Sub = _triggers.Register(instance);
                    }
                    catch
                    {
                        TryUnsubscribe(l);
                        throw;
                    }
                }
            }
        }

        private static TriggerDef BuildTriggerDef(EffectListenerRuntime l)
        {
            var conditions = new List<ConditionDef>(1);

            if (l.Scope == ListenScope.SelfOnly)
            {
                conditions.Add(BuildCasterEqOwnerCondition(l.OwnerActorId));
            }
            else if (l.Scope == ListenScope.OthersOnly)
            {
                var inner = BuildCasterEqOwnerCondition(l.OwnerActorId);
                conditions.Add(BuildNot(inner));
            }

            var actionArgs = new Dictionary<string, object>(2)
            {
                ["effectId"] = l.ExecuteEffectId,
                ["executeMode"] = (int)l.ExecuteMode,
            };
            var actions = new List<ActionDef>(1)
            {
                new ActionDef(type: TriggerActionTypes.EffectExecute, args: actionArgs),
            };

            return new TriggerDef(l.EventId, conditions, actions);
        }

        private static ConditionDef BuildCasterEqOwnerCondition(int ownerActorId)
        {
            var args = new Dictionary<string, object>(3)
            {
                ["key"] = MobaSkillTriggering.Args.CasterActorId,
                ["value_source"] = "const",
                ["value"] = ownerActorId,
            };
            return new ConditionDef(type: TriggerConditionTypes.ArgEq, args: args);
        }

        private static ConditionDef BuildNot(ConditionDef item)
        {
            var args = new Dictionary<string, object>(1)
            {
                [TriggerDefArgKeys.Item] = item,
            };
            return new ConditionDef(type: TriggerConditionTypes.Not, args: args);
        }

        private static void TryUnsubscribe(EffectListenerRuntime l)
        {
            if (l == null) return;
            var sub = l.Sub;
            if (sub == null) return;
            try
            {
                l.Sub = null;
                sub.Unsubscribe();
            }
            catch
            {
            }
        }

        protected override void OnCleanup()
        {
        }
    }
}

