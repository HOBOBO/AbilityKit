using System;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Flow.Snapshot;
using UnityEngine;
using EC = AbilityKit.Ability.EC;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleSyncFeature : IGamePhaseFeature
    {
        private BattleContext _ctx;

        private EC.EntityWorld _world;
        private BattleEntityLookup _lookup;
        private BattleEntityFactory _factory;
        private EC.Entity _node;

        private int _localActorId;

        private IDisposable _subLobby;
        private IDisposable _subEnterGame;
        private IDisposable _subActorTransform;
        private IDisposable _subStateHash;

        public void OnAttach(in GamePhaseContext ctx)
        {
            ctx.Root.TryGetComponent(out _ctx);
            _world = _ctx?.EntityWorld;
            _lookup = _ctx?.EntityLookup;
            _factory = _ctx?.EntityFactory;
            _node = _ctx != null ? _ctx.EntityNode : default;

            if (_ctx?.FrameSnapshots != null)
            {
                _subLobby = _ctx.FrameSnapshots.Subscribe<LobbySnapshot>((int)MobaOpCode.LobbySnapshot, OnLobbySnapshot);
                _subEnterGame = _ctx.FrameSnapshots.Subscribe<EnterMobaGameRes>((int)MobaOpCode.EnterGameSnapshot, OnEnterGameSnapshot);
                _subActorTransform = _ctx.FrameSnapshots.Subscribe<(int actorId, float x, float y, float z)[]>((int)MobaOpCode.ActorTransformSnapshot, OnActorTransformSnapshot);
                _subStateHash = _ctx.FrameSnapshots.Subscribe<MobaStateHashSnapshotCodec.SnapshotPayload>((int)MobaOpCode.StateHashSnapshot, OnStateHashSnapshot);
            }

            _localActorId = 0;
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            if (_ctx?.FrameSnapshots != null)
            {
                _subLobby?.Dispose();
                _subEnterGame?.Dispose();
                _subActorTransform?.Dispose();
                _subStateHash?.Dispose();
            }

            _subLobby = null;
            _subEnterGame = null;
            _subActorTransform = null;
            _subStateHash = null;

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

        private void OnLobbySnapshot(FramePacket packet, LobbySnapshot snap)
        {
            ApplyLobbySnapshot(snap);
        }

        private void OnStateHashSnapshot(FramePacket packet, MobaStateHashSnapshotCodec.SnapshotPayload snap)
        {
            ApplyStateHashSnapshot(snap);
        }

        private void OnEnterGameSnapshot(FramePacket packet, EnterMobaGameRes res)
        {
            ApplyEnterGameSnapshot(res);
        }

        private void OnActorTransformSnapshot(FramePacket packet, (int actorId, float x, float y, float z)[] entries)
        {
            ApplyTransformSnapshot(entries);
        }

        private void ApplyLobbySnapshot(LobbySnapshot snap)
        {
            if (!_node.IsValid) return;

            var comp = _node.TryGetComponent(out BattleLobbySnapshotComponent existing) ? existing : null;
            if (comp == null)
            {
                comp = new BattleLobbySnapshotComponent();
                _node.AddComponent(comp);
            }

            comp.Started = snap.Started;
            comp.Version = snap.Version;
            comp.Players = snap.Players;
        }

        private void ApplyStateHashSnapshot(MobaStateHashSnapshotCodec.SnapshotPayload p)
        {
            if (!_node.IsValid) return;

            var comp = _node.TryGetComponent(out BattleStateHashSnapshotComponent existing) ? existing : null;
            if (comp == null)
            {
                comp = new BattleStateHashSnapshotComponent();
                _node.AddComponent(comp);
            }

            comp.Version = p.Version;
            comp.Frame = p.Frame;
            comp.Hash = p.Hash;
        }

        private void ApplyEnterGameSnapshot(EnterMobaGameRes res)
        {
            if (_world == null || _lookup == null || _factory == null) return;

            var dirty = _ctx != null ? _ctx.DirtyEntities : null;
            if (dirty == null)
            {
                dirty = new System.Collections.Generic.List<EC.EntityId>(8);
                if (_ctx != null) _ctx.DirtyEntities = dirty;
            }
            else
            {
                dirty.Clear();
            }

            _localActorId = res.LocalActorId;

            // Current demo uses payload as 3 floats x,y,z.
            Vector3 pos = default;
            if (res.Payload != null && res.Payload.Length >= 12)
            {
                var x = BitConverter.ToSingle(res.Payload, 0);
                var y = BitConverter.ToSingle(res.Payload, 4);
                var z = BitConverter.ToSingle(res.Payload, 8);
                pos = new Vector3(x, y, z);
            }

            var netId = new BattleNetId(res.LocalActorId);
            if (!_lookup.TryResolve(_world, netId, out var e))
            {
                e = _factory.CreateCharacter(netId);
            }

            if (!e.TryGetComponent(out BattleTransformComponent t) || t == null)
            {
                t = new BattleTransformComponent();
                e.AddComponent(t);
            }

            t.Position = pos;
            if (t.Forward == default) t.Forward = Vector3.forward;

            dirty.Add(e.Id);

            if (e.TryGetComponent(out BattleCharacterComponent c) && c != null)
            {
                // Optionally set team/model from config later.
            }
        }

        private void ApplyTransformSnapshot((int actorId, float x, float y, float z)[] entries)
        {
            if (_world == null || _lookup == null || _factory == null) return;

            var dirty = _ctx != null ? _ctx.DirtyEntities : null;
            if (dirty == null)
            {
                dirty = new System.Collections.Generic.List<EC.EntityId>(64);
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
                    // For now, default unknown actor to Character.
                    e = _factory.CreateCharacter(netId);
                }

                if (!e.TryGetComponent(out BattleTransformComponent t) || t == null)
                {
                    t = new BattleTransformComponent();
                    e.AddComponent(t);
                }

                t.Position = new Vector3(en.x, en.y, en.z);
                if (t.Forward == default) t.Forward = Vector3.forward;

                dirty.Add(e.Id);
            }
        }
    }
}
