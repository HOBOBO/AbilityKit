using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Graph;
using AbilityKit.HFSM.Runtime;
using AbilityKit.Deterministic;
using UnityEngine;

namespace AbilityKit.HFSM.Samples.CompleteHfsmShowcase.Editor
{
    public static class HfsmShowcaseDocuments
    {
        public static GraphAsset BuildPatrol()
        {
            var graph = ScriptableObject.CreateInstance<GraphAsset>();
            graph.GraphName = "PatrolAndBlock";
            var root = graph.CreateStateMachine("Patrol", new Vector2(0, 0));
            var idle = State(graph, root, "Idle", 0, 160);
            var walking = State(graph, root, "Walking", 220, 160, "showcase.patrol.walk");
            var blocked = State(graph, root, "Blocked", 440, 160, "showcase.patrol.block");
            root.DefaultStateId = idle.Id;
            Edge(graph, root, idle, walking, "showcase.walk", 20);
            Edge(graph, root, walking, idle, "showcase.idle", 10);
            Edge(graph, root, idle, blocked, "showcase.blocked", 100);
            Edge(graph, root, walking, blocked, "showcase.blocked", 100);
            Edge(graph, root, blocked, walking, "showcase.walk", 20);
            Edge(graph, root, blocked, idle, "showcase.idle", 10);
            return graph;
        }

        public static GraphAsset BuildCombat()
        {
            var graph = ScriptableObject.CreateInstance<GraphAsset>();
            graph.GraphName = "HierarchicalCombat";
            var life = graph.CreateStateMachine("Life", new Vector2(0, 0));
            var combat = graph.CreateStateMachine("Combat", new Vector2(20, 150));
            combat.RememberLastState = true;
            combat.ParentStateMachineId = life.Id;
            life.AddChildNode(combat.Id);
            var dead = State(graph, life, "Dead", 450, 150);
            life.DefaultStateId = combat.Id;
            Edge(graph, life, combat, dead, "showcase.dead", 100);
            var respawn = Edge(graph, life, dead, combat, "showcase.alive", 100);
            respawn.NextTriggerId = "respawn";

            var idle = State(graph, combat, "Idle", 0, 330);
            var chase = State(graph, combat, "Chase", 220, 330, "showcase.combat.chase");
            var strike = State(graph, combat, "Strike", 440, 330, "showcase.combat.strike");
            combat.DefaultStateId = idle.Id;
            Edge(graph, combat, idle, strike, "showcase.strike", 80);
            Edge(graph, combat, idle, chase, "showcase.chase", 40);
            Edge(graph, combat, chase, strike, "showcase.strike", 80);
            var follow = Edge(graph, combat, strike, chase, "showcase.chase", 40);
            follow.NextMinimumActiveDurationRaw = Fixed64.FromRatio(3, 10).RawValue;
            Edge(graph, combat, chase, idle, "showcase.no-target", 10);
            Edge(graph, combat, strike, idle, "showcase.no-target", 10);
            return graph;
        }

        public static BindingCatalog BuildCatalog()
        {
            var catalog = new BindingCatalog();
            catalog.Register(new BindingDescriptor(BindingKind.State, "showcase.state", "State events"));
            foreach (var key in new[] { "patrol.walk", "patrol.block", "combat.chase", "combat.strike" })
                catalog.Register(new BindingDescriptor(BindingKind.State, "showcase." + key, key));
            catalog.Register(new BindingDescriptor(BindingKind.Action, "showcase.transition", "Transition events"));
            foreach (var key in new[] { "blocked", "walk", "idle", "dead", "alive", "strike", "chase", "no-target" })
                catalog.Register(new BindingDescriptor(BindingKind.Condition, "showcase." + key, key));
            return catalog;
        }

        private static StateNode State(GraphAsset graph, StateMachineNode parent, string name, float x, float y,
            string behaviorKey = "showcase.state")
        {
            var state = graph.CreateState(name, new Vector2(x, y));
            state.ParentStateMachineId = parent.Id;
            state.NextBehaviorKey = behaviorKey;
            parent.AddChildNode(state.Id);
            return state;
        }

        private static TransitionEdge Edge(GraphAsset graph, StateMachineNode parent,
            NodeBase from, NodeBase to, string condition, int priority)
        {
            var edge = graph.CreateTransition(from.Id, to.Id);
            edge.NextConditionKey = condition;
            edge.NextActionKey = "showcase.transition";
            edge.Priority = priority;
            edge.ForceInstantly = true;
            parent.AddTransition(edge.Id);
            return edge;
        }
    }
}
