using AbilityKit.Triggering.Blackboard;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class OwnerBlackboardMembershipRestoreTests
{
    private static OwnerBlackboardStore CreateStore()
    {
        var store = new OwnerBlackboardStore();
        store.Configure(new[] { new BlackboardInitializationPlan
        {
            BoardId = 1, Scope = BlackboardInitializationScopes.Owner,
            Keys = new List<BlackboardInitializationKey> { new() { KeyId = 2, Type = BlackboardKeyType.Int, IntValue = 3 } },
        } });
        return store;
    }

    [Fact]
    public void Restores_members_and_values_without_replacing_surviving_references()
    {
        var store = CreateStore();
        var surviving = store.GetOrCreate(11);
        Assert.True(surviving.TryResolve(1, out var board));
        board.SetInt(2, 7);
        store.GetOrCreate(12);
        Assert.True(store.TryCaptureSnapshotSet(out var snapshot, out _));
        store.Release(12);
        store.GetOrCreate(13);
        board.SetInt(2, 90);
        Assert.False(store.TryRestoreSnapshotSet(snapshot, out _)); // legacy contract remains strict
        Assert.True(store.TryPrepareMembershipRestore(snapshot, new long[] { 11, 12, 13 }, out var plan, out var error), error);
        Assert.False(store.TryGet(12, out _));
        Assert.True(store.TryGet(13, out _));
        Assert.True(board.TryGetInt(2, out var before) && before == 90);
        Assert.True(plan.TryApply(out error), error);
        Assert.Same(surviving, store.GetOrCreate(11));
        Assert.True(surviving.TryResolve(1, out var restoredBoard));
        Assert.Same(board, restoredBoard);
        Assert.True(board.TryGetInt(2, out var value) && value == 7);
        Assert.True(store.TryGet(12, out _));
        Assert.False(store.TryGet(13, out _));
        Assert.False(plan.TryApply(out _));
        Assert.True(store.TryPrepareMembershipRestore(snapshot, new long[] { 11, 12, 13 }, out var repeated, out error), error);
        Assert.True(repeated.TryApply(out error), error);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Protected_membership_changes_are_rejected_without_mutation(bool create)
    {
        var store = CreateStore();
        store.GetOrCreate(11);
        Assert.True(store.TryCaptureSnapshotSet(out var snapshot, out _));
        if (create) store.Release(11);
        else store.GetOrCreate(12);
        Assert.True(store.TryCaptureSnapshotSet(out var before, out _));
        Assert.False(store.TryPrepareMembershipRestore(snapshot, Array.Empty<long>(), out _, out _));
        Assert.True(store.TryCaptureSnapshotSet(out var after, out _));
        Assert.Equal(before.ToJson(), after.ToJson());
    }

    [Theory]
    [InlineData("duplicate-owner")]
    [InlineData("duplicate-board")]
    [InlineData("wrong-type")]
    [InlineData("wrong-template")]
    [InlineData("zero-owner")]
    [InlineData("version")]
    public void Invalid_snapshots_cannot_create_live_owners(string corruption)
    {
        var source = CreateStore();
        source.GetOrCreate(11);
        Assert.True(source.TryCaptureSnapshotSet(out var snapshot, out _));
        var owner = snapshot.Owners[0];
        switch (corruption)
        {
            case "duplicate-owner": snapshot.Owners.Add(owner); break;
            case "duplicate-board": owner.Boards.Add(owner.Boards[0]); break;
            case "wrong-type":
                var entry = owner.Boards[0].Entries[0];
                entry.Type = BlackboardKeyType.String;
                owner.Boards[0].Entries[0] = entry;
                break;
            case "wrong-template": owner.Boards[0].BoardId = 42; break;
            case "zero-owner": owner.OwnerKey = 0; break;
            case "version": snapshot.Version++; break;
        }
        var store = CreateStore();
        Assert.False(store.TryPrepareMembershipRestore(snapshot, new long[] { 11 }, out _, out _));
        Assert.False(store.TryGet(11, out _));
    }

    [Fact]
    public void Failed_apply_compensates_earlier_value_writes_and_preserves_membership()
    {
        var store = CreateStore();
        var first = store.GetOrCreate(11);
        var second = store.GetOrCreate(12);
        Assert.True(first.TryResolve(1, out var firstBoard));
        Assert.True(second.TryResolve(1, out var secondBoard));
        Assert.True(store.TryCaptureSnapshotSet(out var snapshot, out _));
        Assert.True(store.TryPrepareMembershipRestore(snapshot, new long[] { 11, 12, 13 }, out var plan, out _));
        firstBoard.SetInt(2, 80);
        secondBoard.SetInt(2, 90);
        // Fail on the second owner after the first owner's values have been restored.
        ((DictionaryBlackboard)secondBoard).DefineKey(5, BlackboardKeyType.Int);
        secondBoard.SetInt(5, 100);
        Assert.True(store.TryCaptureSnapshotSet(out var before, out _));
        Assert.False(plan.TryApply(out var error));
        Assert.Contains("previous state restored", error);
        Assert.True(store.TryCaptureSnapshotSet(out var after, out _));
        Assert.Equal(before.ToJson(), after.ToJson());
        Assert.Same(first, store.GetOrCreate(11));
        Assert.Same(second, store.GetOrCreate(12));
    }

    [Fact]
    public void In_place_template_changes_invalidate_prepared_plan()
    {
        var template = new BlackboardInitializationPlan
        {
            BoardId = 1, Scope = BlackboardInitializationScopes.Owner,
            Keys = new List<BlackboardInitializationKey> { new() { KeyId = 2, Type = BlackboardKeyType.Int } },
        };
        var store = new OwnerBlackboardStore();
        store.Configure(new[] { template });
        store.GetOrCreate(11);
        Assert.True(store.TryCaptureSnapshotSet(out var snapshot, out _));
        store.Release(11);
        Assert.True(store.TryPrepareMembershipRestore(snapshot, new long[] { 11 }, out var plan, out _));
        template.Keys[0].Type = BlackboardKeyType.Bool;
        Assert.False(plan.TryApply(out _));
        Assert.False(store.TryGet(11, out _));
    }

    [Fact]
    public void Empty_checkpoint_removes_only_explicitly_allowed_members()
    {
        var store = CreateStore();
        Assert.True(store.TryCaptureSnapshotSet(out var snapshot, out _));
        store.GetOrCreate(11);
        Assert.True(store.TryPrepareMembershipRestore(snapshot, new long[] { 11 }, out var plan, out var error), error);
        Assert.True(store.TryGet(11, out _));
        Assert.True(plan.TryApply(out error), error);
        Assert.False(store.TryGet(11, out _));
    }

    [Fact]
    public void Prepared_payload_is_detached_and_stale_membership_is_rejected()
    {
        var store = CreateStore();
        store.GetOrCreate(11);
        Assert.True(store.TryCaptureSnapshotSet(out var snapshot, out _));
        Assert.True(store.TryPrepareMembershipRestore(snapshot, new long[] { 11 }, out var detached, out _));
        snapshot.Owners.Clear();
        Assert.True(detached.TryApply(out var error), error);
        Assert.True(store.TryGet(11, out _));

        Assert.True(store.TryCaptureSnapshotSet(out snapshot, out _));
        Assert.True(store.TryPrepareMembershipRestore(snapshot, new long[] { 11 }, out var stale, out _));
        store.Release(11);
        store.GetOrCreate(11);
        Assert.False(stale.TryApply(out _));
    }
}
