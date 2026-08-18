using System;
using AbilityKit.Ability.Behavior;
using AbilityKit.Core.Mathematics;
using AbilityKit.Moba.Behavior;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Behavior;

public sealed class MobaWorldQueryBoundaryTests
{
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
}
