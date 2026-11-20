using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed partial class MobaSkillLifecycleRollbackPreparationTests
{
    [Theory]
    [InlineData(10000, 10009, 10011)]
    [InlineData(10000, 10011, 10009)]
    [InlineData(10009, 10000, 10011)]
    [InlineData(10009, 10011, 10000)]
    [InlineData(10011, 10000, 10009)]
    [InlineData(10011, 10009, 10000)]
    public void Battle_explicit_structure_plan_restores_all_input_permutations_and_locomotion(int a, int b, int c)
    {
        Assert.True(Get<MobaActorRegistry>().TryGet(1, out var actor));
        var original = OwnedLocomotion(1);
        original.Configure(37.5f, AbilityKit.Combat.MotionSystem.Core.MotionInputSpace.Local, 17);
        original.SetInput(.4f, -.6f);
        var coordinator = new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(_world), new RollbackSnapshotRingBuffer(2));
        var target = coordinator.Capture(_frame);
        var canonical = WorldRollbackSnapshotCodec.Serialize(target);
        var keys = new Queue<int>(new[] { a, b, c });
        var structures = target.Entries.Where(e => IsBattleStructureKey(e.Key)).ToDictionary(e => e.Key);
        var entries = target.Entries.Select(e => IsBattleStructureKey(e.Key) ? structures[keys.Dequeue()] : e).ToArray();
        var inputKeys = entries.Select(e => e.Key).ToArray();
        var reordered = new WorldRollbackSnapshot(target.Version, target.Frame, entries);
        RetireLocomotion(actor);
        actor.motion.Pipeline.Policy.SetSuppressedGroups(993321, 1);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            Assert.True(coordinator.TryRestore(reordered, out var result), result.ToString());
            Assert.Equal(target.Entries.Length, result.ProviderCount);
            Assert.Equal(canonical, WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame)));
            Assert.Equal(inputKeys, reordered.Entries.Select(e => e.Key));
            var source = OwnedLocomotion(1);
            Assert.NotSame(original, source);
            Assert.Equal(37.5f, source.Speed);
            Assert.Equal(original.Input, source.Input);
        }
    }

    [Theory]
    [InlineData(10000, false)] [InlineData(10000, true)]
    [InlineData(10009, false)] [InlineData(10009, true)]
    [InlineData(10011, false)] [InlineData(10011, true)]
    public void Battle_requires_exactly_one_payload_for_each_structure_step_before_import(int key, bool duplicate)
    {
        var coordinator = new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(_world), new RollbackSnapshotRingBuffer(2));
        var target = coordinator.Capture(_frame);
        Assert.True(Get<MobaActorRegistry>().TryGet(1, out var actor));
        actor.motion.Pipeline.Policy.SetSuppressedGroups(993322, 1);
        var before = WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame));
        var entries = duplicate ? target.Entries.Append(target.Entries.Single(e => e.Key == key)).ToArray()
            : target.Entries.Where(e => e.Key != key).ToArray();
        var corrupt = new WorldRollbackSnapshot(target.Version, target.Frame, entries);

        Assert.False(coordinator.TryRestore(corrupt, out var result));
        Assert.Equal(key, result.ProviderKey);
        Assert.Equal(0, result.ProviderCount);
        Assert.Equal(before, WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame)));
    }

    [Fact]
    public void Lifecycle_rejects_wrong_explicit_effective_order_even_with_canonical_input()
    {
        var registry = CopyRegistryWithoutStructureOrder(MobaRollbackRegistryBuilder.Create(_world));
        registry.SetStructureRestoreOrder(10009, 10000, 10011);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
        var target = coordinator.Capture(_frame);
        var before = WorldRollbackSnapshotCodec.Serialize(target);

        Assert.False(coordinator.TryRestore(target, out var result));
        Assert.Equal(MobaSkillLifecycleRollbackProvider.DefaultKey, result.ProviderKey);
        Assert.Equal(0, result.ProviderCount);
        Assert.Equal(before, WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame)));
    }

    private static bool IsBattleStructureKey(int key) => key is 10000 or 10009 or 10011;

    private static RollbackRegistry CopyRegistryWithoutStructureOrder(RollbackRegistry source)
    {
        var registry = new RollbackRegistry();
        foreach (var provider in source.Providers) registry.Register(provider);
        foreach (var key in source.RequiredStateKeys) registry.RequireState(key);
        return registry;
    }
}
