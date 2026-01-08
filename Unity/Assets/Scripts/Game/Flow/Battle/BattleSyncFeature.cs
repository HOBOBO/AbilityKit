using System;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
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

        public void OnAttach(in GamePhaseContext ctx)
        {
            ctx.Root.TryGetComponent(out _ctx);
            _world = _ctx?.EntityWorld;
            _lookup = _ctx?.EntityLookup;
            _factory = _ctx?.EntityFactory;
            _node = _ctx != null ? _ctx.EntityNode : default;

            if (_ctx?.Session != null)
            {
                _ctx.Session.FrameReceived += OnFrame;
            }

            _localActorId = 0;
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            if (_ctx?.Session != null)
            {
                _ctx.Session.FrameReceived -= OnFrame;
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

        private void OnFrame(FramePacket packet)
        {
            if (!packet.Snapshot.HasValue) return;

            var snap = packet.Snapshot.Value;
            if (snap.OpCode == (int)MobaOpCode.EnterGameSnapshot)
            {
                ApplyEnterGameSnapshot(snap.Payload);
            }
            else if (snap.OpCode == (int)MobaOpCode.ActorTransformSnapshot)
            {
                ApplyTransformSnapshot(snap.Payload);
            }
            else if (snap.OpCode == (int)MobaOpCode.LobbySnapshot)
            {
                ApplyLobbySnapshot(snap.Payload);
            }
            else if (snap.OpCode == (int)MobaOpCode.StateHashSnapshot)
            {
                ApplyStateHashSnapshot(snap.Payload);
            }
        }

        private void ApplyLobbySnapshot(byte[] payload)
        {
            if (!_node.IsValid) return;
            if (payload == null || payload.Length == 0) return;

            var snap = MobaLobbyCodec.DeserializeSnapshot(payload);

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

        private void ApplyStateHashSnapshot(byte[] payload)
        {
            if (!_node.IsValid) return;
            if (payload == null || payload.Length == 0) return;

            var p = MobaStateHashSnapshotCodec.Deserialize(payload);

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

        private void ApplyEnterGameSnapshot(byte[] payload)
        {
            if (_world == null || _lookup == null || _factory == null) return;
            if (payload == null || payload.Length == 0) return;

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

            var res = EnterMobaGameCodec.DeserializeRes(payload);
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

        private void ApplyTransformSnapshot(byte[] payload)
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

            var entries = MobaActorTransformSnapshotCodec.Deserialize(payload);
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
