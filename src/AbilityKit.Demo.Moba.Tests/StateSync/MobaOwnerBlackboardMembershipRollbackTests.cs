using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Triggering.Blackboard;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed class MobaOwnerBlackboardMembershipRollbackTests
{
    private static readonly FrameIndex Frame = new(12);

    [Fact]
    public void Actor_members_roundtrip_without_mutating_preflight_or_replacing_surviving_boards()
    {
        var store = CreateStore();
        var actors = new Actors(store, 11, 12, 13);
        var subscriptions = new Source("subscriptions", 900);
        var provider = new MobaOwnerBlackboardRollbackProvider(store, new IMobaOwnerKeySource[] { actors, subscriptions });
        var survivor = store.GetOrCreate(11);
        Assert.True(survivor.TryResolve(1, out var board));
        board.SetInt(2, 7);
        store.GetOrCreate(13);
        store.GetOrCreate(900);
        var snapshot = provider.Export(Frame);
        store.Release(13);
        store.GetOrCreate(12);
        board.SetInt(2, 99);
        var beforePreflight = provider.Export(Frame);

        provider.ValidateImport(Frame, snapshot);
        Assert.Equal(beforePreflight, provider.Export(Frame));
        provider.Import(Frame, snapshot);
        Assert.Same(survivor, store.GetOrCreate(11));
        Assert.True(survivor.TryResolve(1, out var restoredBoard));
        Assert.Same(board, restoredBoard);
        Assert.True(board.TryGetInt(2, out var restored) && restored == 7);
        Assert.True(store.TryGet(13, out _));
        Assert.False(store.TryGet(12, out _));
        Assert.Equal(snapshot, provider.Export(Frame));
        provider.Import(Frame, snapshot);
        Assert.Equal(snapshot, provider.Export(Frame));
    }

    [Theory]
    [InlineData("roster")]
    [InlineData("subscription")]
    [InlineData("overlap")]
    [InlineData("orphan")]
    public void Changed_or_ambiguous_lifecycle_is_rejected_before_values_change(string change)
    {
        var store = CreateStore();
        var actors = new Actors(store, 11, 12);
        var subscriptions = new Source("subscriptions");
        var provider = new MobaOwnerBlackboardRollbackProvider(store, new IMobaOwnerKeySource[] { actors, subscriptions });
        var owner = store.GetOrCreate(11);
        Assert.True(owner.TryResolve(1, out var board));
        var snapshot = provider.Export(Frame);
        board.SetInt(2, 99);
        switch (change)
        {
            case "roster": actors.Ids.Add(13); break;
            case "subscription": subscriptions.Keys.Add(900); store.GetOrCreate(900); break;
            case "overlap": subscriptions.Keys.Add(11); break;
            case "orphan": store.GetOrCreate(900); break;
        }
        Assert.Throws<InvalidOperationException>(() => provider.ValidateImport(Frame, snapshot));
        Assert.Throws<InvalidOperationException>(() => provider.Import(Frame, snapshot));
        Assert.True(board.TryGetInt(2, out var unchanged) && unchanged == 99);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("source")]
    [InlineData("duplicate-owner")]
    [InlineData("template")]
    public void Corrupt_versioned_payloads_cannot_create_owners(string change)
    {
        var store = CreateStore();
        var actors = new Actors(store, 11, 12);
        var provider = new MobaOwnerBlackboardRollbackProvider(store, new[] { actors });
        store.GetOrCreate(11);
        var json = JObject.Parse(Encoding.UTF8.GetString(provider.Export(Frame)));
        store.Release(11);
        switch (change)
        {
            case "version": json["Version"] = 99; break;
            case "source": json["Sources"]![0]!["OwnerKeys"] = new JArray(900); break;
            case "duplicate-owner":
                var owners = (JArray)json["Snapshot"]!["Owners"]!;
                owners.Add(owners[0].DeepClone());
                break;
            case "template": json["Snapshot"]!["Owners"]![0]!["Boards"]![0]!["BoardId"] = 42; break;
        }
        var payload = Encoding.UTF8.GetBytes(json.ToString());
        Assert.Throws<InvalidOperationException>(() => provider.ValidateImport(Frame, payload));
        Assert.False(store.TryGet(11, out _));
    }

    [Fact]
    public void Stable_shared_owner_is_preserved_but_cannot_be_recreated_as_actor_only()
    {
        var store = CreateStore();
        var actors = new Actors(store, 11);
        var subscriptions = new Source("subscriptions", 11);
        var provider = new MobaOwnerBlackboardRollbackProvider(store, new IMobaOwnerKeySource[] { actors, subscriptions });
        var resolver = store.GetOrCreate(11);
        var checkpoint = provider.Export(Frame);
        provider.Import(Frame, checkpoint);
        Assert.Same(resolver, store.GetOrCreate(11));
        store.Release(11);
        Assert.Throws<InvalidOperationException>(() => provider.ValidateImport(Frame, checkpoint));
        Assert.False(store.TryGet(11, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"Format\":\"unknown\",\"Version\":1}")]
    [InlineData("{\"Format\":\"moba-owner-blackboard\",\"Version\":\"bad\"}")]
    [InlineData("{\"Format\":\"moba-owner-blackboard\",\"Version\":1}")]
    public void Invalid_envelopes_fail_without_mutation(string json)
    {
        var store = CreateStore();
        var provider = new MobaOwnerBlackboardRollbackProvider(store, new[] { new Actors(store, 11) });
        store.GetOrCreate(11);
        var before = provider.Export(Frame);
        var invalid = Encoding.UTF8.GetBytes(json);
        Assert.Throws<InvalidOperationException>(() => provider.ValidateImport(Frame, invalid));
        Assert.Throws<InvalidOperationException>(() => provider.Import(Frame, invalid));
        Assert.Equal(before, provider.Export(Frame));
    }

    [Fact]
    public void Legacy_payload_retains_strict_membership_contract()
    {
        var store = CreateStore();
        store.GetOrCreate(11);
        Assert.True(store.TryCaptureSnapshotSet(out var snapshot, out _));
        var payload = Encoding.UTF8.GetBytes(snapshot.ToJson());
        var provider = new MobaOwnerBlackboardRollbackProvider(store, new[] { new Actors(store, 11, 12) });
        provider.Import(Frame, payload);
        store.GetOrCreate(12);
        Assert.Throws<InvalidOperationException>(() => provider.ValidateImport(Frame, payload));
        Assert.True(store.TryGet(12, out _));
    }

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

    private sealed class Actors : IMobaActorOwnerKeySource
    {
        private readonly OwnerBlackboardStore _store;
        public readonly List<int> Ids;
        public Actors(OwnerBlackboardStore store, params int[] ids) { _store = store; Ids = ids.ToList(); }
        public string Name => "actors";
        public void CopyActorIds(List<int> destination) { destination.Clear(); destination.AddRange(Ids); }
        public void CopyActiveOwnerKeys(List<long> destination)
        {
            destination.Clear();
            foreach (var id in Ids) if (_store.TryGet(id, out _)) destination.Add(id);
        }
    }

    private sealed class Source : IMobaOwnerKeySource
    {
        public Source(string name, params long[] keys) { Name = name; Keys = keys.ToList(); }
        public string Name { get; }
        public readonly List<long> Keys;
        public void CopyActiveOwnerKeys(List<long> destination) { destination.Clear(); destination.AddRange(Keys); }
    }
}
