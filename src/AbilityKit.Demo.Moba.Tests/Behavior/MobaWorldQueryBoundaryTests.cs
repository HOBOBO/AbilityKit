using System;
using System.Collections.Generic;
using AbilityKit.Ability.Behavior;
using AbilityKit.Core.Mathematics;
using AbilityKit.Moba.Behavior;
using AbilityKit.Demo.Moba.Services;
using moba_luban;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Behavior;

public sealed class MobaWorldQueryBoundaryTests
{
    [Fact]
    public void Channeling_uses_cast_gate_instead_of_control_immunity_gate()
    {
        var owner = new BehaviorEntityId(1);
        var decision = MobaBehaviorDecisions.CreateChannelingDecision((_, _, _) => true);
        var protectedWorld = new MobaWorldQuery(new EntityManager(), new BuffManager(), new AttributeSystem(),
            tagGate: (_, gateId) => gateId == MobaTagBehaviorGate.Controlled);
        Assert.Equal(DecisionKind.Continue, decision.Decide(new Context(owner, protectedWorld), protectedWorld).Kind);

        var blockedWorld = new MobaWorldQuery(new EntityManager(), new BuffManager(), new AttributeSystem(),
            tagGate: (_, gateId) => gateId == MobaTagBehaviorGate.Cast);
        Assert.Equal(DecisionKind.Interrupt, decision.Decide(new Context(owner, blockedWorld), blockedWorld).Kind);
    }

    [Fact]
    public void Read_only_query_rejects_decision_side_world_mutations()
    {
        var query = new MobaWorldQuery(
            new EntityManager(),
            new BuffManager(),
            new AttributeSystem(),
            allowMutations: false);
        var actor = new BehaviorEntityId(1);

        Assert.Throws<InvalidOperationException>(() => query.SetPosition(actor, Vec3.Zero));
        Assert.Throws<InvalidOperationException>(() => query.SetForward(actor, Vec3.Forward));
        Assert.Throws<InvalidOperationException>(() => query.SetData(actor, "key", 1));
        Assert.Equal(Vec3.Zero, query.GetPosition(actor));
    }

    private sealed class EntityManager : MobaWorldQuery.IEntityManager
    {
        public bool Exists(long entityId) => entityId == 1;
        public Vec3 GetPosition(long entityId) => Vec3.Zero;
        public void SetPosition(long entityId, Vec3 position) { }
        public Vec3 GetForward(long entityId) => Vec3.Forward;
        public void SetForward(long entityId, Vec3 forward) { }
    }

    private sealed class BuffManager : MobaWorldQuery.IBuffManager
    {
        public bool HasBuff(long entityId, string buffId) => false;
        public bool HasTag(long entityId, string tag) => false;
    }

    private sealed class AttributeSystem : MobaWorldQuery.IAttributeSystem
    {
        public float GetAttribute(long entityId, string attributeId) => 0f;
        public bool IsAlive(long entityId) => true;
        public int GetTeam(long entityId) => 1;
    }

    private sealed class Context : IBehaviorContext
    {
        public Context(BehaviorEntityId owner, IWorldQuery world) { OwnerId = owner; World = world; }
        public long InstanceId => 1;
        public string BehaviorKind => "Channeling";
        public long SourceContextId => 0;
        public BehaviorEntityId OwnerId { get; }
        public BehaviorEntityId? TargetId => null;
        public long CurrentFrame => 0;
        public float ElapsedSeconds => 0;
        public float? DurationSeconds => null;
        public BehaviorPhase Phase => BehaviorPhase.Running;
        public IWorldQuery World { get; }
        public IBehaviorState State => null!;
        public IReadOnlyDictionary<string, object> Config => new Dictionary<string, object>();
        public T GetConfig<T>(string key, T defaultValue = default) => defaultValue;
        public bool TryGetConfig<T>(string key, out T value) { value = default!; return false; }
        public Vec3? GetTargetPosition() => null;
    }
}
