using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.SnapshotRouting;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private void EnsureConfirmedAuthorityViewSide(WorldId authWorldId)
        {
            // Build a dedicated view context for confirmed authority world and attach an extra view feature.
            // This context owns its own EC.EntityWorld and view binder mappings, isolated from the main battle context.
            if (_flow != null && _confirmedViewFeature == null && _plan.EnableConfirmedAuthorityWorld)
            {
                _confirmedViewCtx = BattleContext.Rent();
                _confirmedViewCtx.Plan = _ctx != null ? _ctx.Plan : default;
                _confirmedViewCtx.Session = null;

                var viewWorld = new AbilityKit.Ability.EC.EntityWorld();
                var lookup = new BattleEntityLookup();
                var node = viewWorld.Create("BattleEntity__confirmed");
                var entityFactory = new BattleEntityFactory(viewWorld, lookup, node);
                var query = new BattleEntityQuery(viewWorld, lookup);

                if (node.IsValid)
                {
                    node.AddComponent(lookup);
                    node.AddComponent(entityFactory);
                    node.AddComponent(query);
                }

                _confirmedViewCtx.EntityNode = node;
                _confirmedViewCtx.EntityWorld = viewWorld;
                _confirmedViewCtx.EntityLookup = lookup;
                _confirmedViewCtx.EntityFactory = entityFactory;
                _confirmedViewCtx.EntityQuery = query;
                _confirmedViewCtx.DirtyEntities = new List<AbilityKit.Ability.EC.EntityId>(128);

                _confirmedViewSnapshots = new FrameSnapshotDispatcher();
                _confirmedViewPipeline = new SnapshotPipeline(_confirmedViewCtx, _confirmedViewSnapshots);
                _confirmedViewCmdHandler = new SnapshotCmdHandler(_confirmedViewCtx, _confirmedViewSnapshots);
                AbilityKit.Game.Flow.Snapshot.BattleSnapshotRegistry.RegisterAll(_confirmedViewSnapshots, _confirmedViewPipeline, _confirmedViewPipeline, _confirmedViewCmdHandler);

                AbilityKit.Game.Flow.Snapshot.LobbySnapshotRegistry.RegisterAll(_confirmedViewSnapshots, _confirmedViewPipeline, _confirmedViewPipeline, _confirmedViewCmdHandler);

                AbilityKit.Game.Flow.Snapshot.SharedSnapshotRegistry.RegisterAll(_confirmedViewSnapshots, _confirmedViewPipeline, _confirmedViewPipeline, _confirmedViewCmdHandler);

                // Apply snapshots to confirmed view-side entity world (same logic as BattleSyncFeature subscriptions).
                _confirmedViewSubLobby = _confirmedViewSnapshots.Subscribe<LobbySnapshot>(
                    (int)MobaOpCode.LobbySnapshot,
                    (packet, snap) => ApplyConfirmedViewLobbySnapshot(snap));
                _confirmedViewSubActorTransform = _confirmedViewSnapshots.Subscribe<(int actorId, float x, float y, float z)[]>(
                    (int)MobaOpCode.ActorTransformSnapshot,
                    (packet, entries) => ApplyConfirmedViewTransformSnapshot(entries));
                _confirmedViewSubStateHash = _confirmedViewSnapshots.Subscribe<MobaStateHashSnapshotCodec.SnapshotPayload>(
                    (int)MobaOpCode.StateHashSnapshot,
                    (packet, snap) => ApplyConfirmedViewStateHashSnapshot(snap));

                _confirmedViewCtx.FrameSnapshots = _confirmedViewSnapshots;
                _confirmedViewCtx.SnapshotPipeline = _confirmedViewPipeline;
                _confirmedViewCtx.CmdHandler = _confirmedViewCmdHandler;

                _confirmedViewFeature = new ConfirmedBattleViewFeature(_confirmedViewCtx);
                _flow.Attach(_confirmedViewFeature);
            }

            BattleFlowDebugProvider.ConfirmedAuthorityWorldStats = new ConfirmedAuthorityWorldStatsSnapshot
            {
                WorldId = authWorldId.Value,
                ConfirmedFrame = 0,
                PredictedFrame = 0,
                AuthorityInputTargetFrame = 0,
                AuthorityDriveTargetFrame = 0,
                AuthorityLastTickedFrame = 0,
                ViewEventTotal = 0,
                RecentViewEvents = null,
            };
        }

        internal sealed class DebugBattleViewEventSink : IBattleViewEventSink
        {
            private readonly string[] _lines;
            private int _next;
            private int _count;

            public int Total { get; private set; }

            public DebugBattleViewEventSink(int maxLines)
            {
                if (maxLines <= 0) maxLines = 16;
                _lines = new string[maxLines];
            }

            public string[] GetRecentLines()
            {
                if (_count <= 0) return Array.Empty<string>();

                var n = Math.Min(_count, _lines.Length);
                var arr = new string[n];
                var start = (_next - n + _lines.Length) % _lines.Length;
                for (int i = 0; i < n; i++)
                {
                    arr[i] = _lines[(start + i) % _lines.Length];
                }
                return arr;
            }

            private void Push(string line)
            {
                if (string.IsNullOrWhiteSpace(line)) return;
                _lines[_next] = line;
                _next = (_next + 1) % _lines.Length;
                if (_count < _lines.Length) _count++;
                Total++;
            }

            public void OnTriggerEvent(in AbilityKit.Ability.Triggering.TriggerEvent evt)
            {
                var id = evt.Id != null ? evt.Id.ToString() : "<null>";
                Push($"Trigger:{id}");
            }

            public void OnEnterGameSnapshot(AbilityKit.Ability.Host.ISnapshotEnvelope packet, EnterMobaGameRes res)
            {
                Push($"EnterGame: tickRate={res.TickRate}");
            }

            public void OnActorTransformSnapshot(AbilityKit.Ability.Host.ISnapshotEnvelope packet, (int actorId, float x, float y, float z)[] entries)
            {
                if (entries == null) return;
                Push($"Transform: n={entries.Length}");
            }

            public void OnProjectileEventSnapshot(AbilityKit.Ability.Host.ISnapshotEnvelope packet, MobaProjectileEventSnapshotCodec.Entry[] entries)
            {
                if (entries == null) return;
                Push($"Projectile: n={entries.Length}");
            }

            public void OnAreaEventSnapshot(AbilityKit.Ability.Host.ISnapshotEnvelope packet, MobaAreaEventSnapshotCodec.Entry[] entries)
            {
                if (entries == null) return;
                Push($"Area: n={entries.Length}");
            }

            public void OnDamageEventSnapshot(AbilityKit.Ability.Host.ISnapshotEnvelope packet, MobaDamageEventSnapshotCodec.Entry[] entries)
            {
                if (entries == null) return;
                Push($"Damage: n={entries.Length}");
            }
        }

        private void ApplyConfirmedViewLobbySnapshot(LobbySnapshot snap)
        {
            if (_confirmedViewCtx == null) return;
            var node = _confirmedViewCtx.EntityNode;
            if (!node.IsValid) return;

            var comp = node.TryGetComponent(out BattleLobbySnapshotComponent existing) ? existing : null;
            if (comp == null)
            {
                comp = new BattleLobbySnapshotComponent();
                node.AddComponent(comp);
            }

            comp.Started = snap.Started;
            comp.Version = snap.Version;
            comp.Players = snap.Players;
        }

        private void ApplyConfirmedViewStateHashSnapshot(MobaStateHashSnapshotCodec.SnapshotPayload p)
        {
            if (_confirmedViewCtx == null) return;
            var node = _confirmedViewCtx.EntityNode;
            if (!node.IsValid) return;

            var comp = node.TryGetComponent(out BattleStateHashSnapshotComponent existing) ? existing : null;
            if (comp == null)
            {
                comp = new BattleStateHashSnapshotComponent();
                node.AddComponent(comp);
            }

            comp.Version = p.Version;
            comp.Frame = p.Frame;
            comp.Hash = p.Hash;
        }

        private void ApplyConfirmedViewTransformSnapshot((int actorId, float x, float y, float z)[] entries)
        {
            if (_confirmedViewCtx == null) return;

            var world = _confirmedViewCtx.EntityWorld;
            var lookup = _confirmedViewCtx.EntityLookup;
            var entityFactory = _confirmedViewCtx.EntityFactory;
            if (world == null || lookup == null || entityFactory == null) return;

            var dirty = _confirmedViewCtx.DirtyEntities;
            if (dirty == null)
            {
                dirty = new List<AbilityKit.Ability.EC.EntityId>(64);
                _confirmedViewCtx.DirtyEntities = dirty;
            }
            else
            {
                dirty.Clear();
            }

            if (entries == null || entries.Length == 0) return;
            for (int i = 0; i < entries.Length; i++)
            {
                var en = entries[i];
                var netId = new BattleNetId(en.actorId);

                if (!lookup.TryResolve(world, netId, out var e))
                {
                    continue;
                }

                if (!e.TryGetComponent(out BattleTransformComponent t) || t == null)
                {
                    t = new BattleTransformComponent();
                    e.AddComponent(t);
                }

                t.Position.x = en.x;
                t.Position.y = en.y;
                t.Position.z = en.z;
                if (t.Forward == default) t.Forward = UnityEngine.Vector3.forward;

                dirty.Add(e.Id);
            }
        }
    }
}
