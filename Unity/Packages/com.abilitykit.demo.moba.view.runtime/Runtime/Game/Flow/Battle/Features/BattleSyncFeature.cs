using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Common.SnapshotRouting;
using AbilityKit.Ability.Host.Extensions.Moba.Room;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.World.ECS;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleSyncFeature : IGamePhaseFeature
    {
        private BattleContext _ctx;

        private EC.IECWorld _world;
        private BattleEntityLookup _lookup;
        private BattleEntityFactory _factory;
        private EC.IEntity _node;

        private int _localActorId;

        private IDisposable _subActorTransform;
        private IDisposable _subStateHash;

        public void OnAttach(in GamePhaseContext ctx)
        {
            ctx.Root.TryGetRef(out _ctx);
            _world = _ctx?.EntityWorld;
            _lookup = _ctx?.EntityLookup;
            _factory = _ctx?.EntityFactory;
            _node = _ctx != null ? _ctx.EntityNode : default;

            var syncMode = _ctx != null ? _ctx.Plan.SyncMode : BattleSyncMode.Lockstep;

            if (_ctx?.FrameSnapshots != null)
            {
                switch (syncMode)
                {
                    case BattleSyncMode.SnapshotAuthority:
                    case BattleSyncMode.Lockstep:
                    case BattleSyncMode.HybridPredictReconcile:
                    default:
                        _subActorTransform = _ctx.FrameSnapshots.Subscribe<(int actorId, float x, float y, float z)[]>((int)MobaOpCode.ActorTransformSnapshot, OnActorTransformSnapshot);
                        _subStateHash = _ctx.FrameSnapshots.Subscribe<MobaStateHashSnapshotCodec.SnapshotPayload>((int)MobaOpCode.StateHashSnapshot, OnStateHashSnapshot);
                        break;
                }
            }

            _localActorId = 0;

            if (_ctx != null)
            {
                _ctx.RuntimeWorldId = default;
                _ctx.HasRuntimeWorldId = false;
            }
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            if (_ctx?.FrameSnapshots != null)
            {
                _subActorTransform?.Dispose();
                _subStateHash?.Dispose();
            }

            _subActorTransform = null;
            _subStateHash = null;

            if (_ctx != null)
            {
                _ctx.RuntimeWorldId = default;
                _ctx.HasRuntimeWorldId = false;
            }

            _ctx = null;
            _world = null;
            _lookup = null;
            _factory = null;
            _node = default;
            _localActorId = 0;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }

        private void OnStateHashSnapshot(ISnapshotEnvelope packet, MobaStateHashSnapshotCodec.SnapshotPayload snap)
        {
            ApplyStateHashSnapshot(snap);

            if (_ctx != null)
            {
                _ctx.RuntimeWorldId = packet.WorldId;
                _ctx.HasRuntimeWorldId = true;
            }

            var target = _ctx?.PredictionReconcileTarget;
            if (target != null)
            {
                target.OnAuthoritativeStateHash(packet.WorldId, new FrameIndex(snap.Frame), new AbilityKit.Ability.FrameSync.Rollback.WorldStateHash(snap.Hash));
            }
        }

        private void OnActorTransformSnapshot(ISnapshotEnvelope packet, (int actorId, float x, float y, float z)[] entries)
        {
            if (_ctx != null)
            {
                _ctx.RuntimeWorldId = packet.WorldId;
                _ctx.HasRuntimeWorldId = true;
            }
            ApplyTransformSnapshot(entries);
        }

        private void ApplyStateHashSnapshot(MobaStateHashSnapshotCodec.SnapshotPayload p)
        {
            if (!_node.IsValid) return;

            var comp = _node.TryGetRef(out BattleStateHashSnapshotComponent existing) ? existing : null;
            if (comp == null)
            {
                comp = new BattleStateHashSnapshotComponent();
                _node.WithRef(comp);
            }

            comp.Version = p.Version;
            comp.Frame = p.Frame;
            comp.Hash = p.Hash;
        }

        private void ApplyTransformSnapshot((int actorId, float x, float y, float z)[] entries)
        {
            if (_world == null || _lookup == null || _factory == null) return;

            var dirty = _ctx != null ? _ctx.DirtyEntities : null;
            if (dirty == null)
            {
                dirty = new System.Collections.Generic.List<EC.IEntityId>(64);
                if (_ctx != null) _ctx.DirtyEntities = dirty;
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

                if (!_lookup.TryResolve(_world, netId, out var e))
                {
                    continue;
                }

                if (!e.TryGetRef(out BattleTransformComponent t) || t == null)
                {
                    t = new BattleTransformComponent();
                    e.WithRef(t);
                }

                t.Position.x = en.x;
                t.Position.y = en.y;
                t.Position.z = en.z;
                if (t.Forward == default) t.Forward = Vector3.forward;

                dirty.Add(e.Id);
            }
        }
    }
}
