using System;
using System.Collections.Generic;
using AbilityKit.Ability.Server;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using UnityEngine;
using EC = AbilityKit.Ability.EC;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleViewFeature : IGamePhaseFeature
    {
        private BattleContext _ctx;
        private IBattleEntityQuery _query;
        private readonly Dictionary<EC.EntityId, GameObject> _views = new Dictionary<EC.EntityId, GameObject>();

        public void OnAttach(in GamePhaseContext ctx)
        {
            ctx.Root.TryGetComponent(out _ctx);
            _query = _ctx != null ? _ctx.EntityQuery : null;

            if (_ctx?.EntityWorld != null)
            {
                _ctx.EntityWorld.EntityDestroyed += OnEntityDestroyed;
            }

            if (_ctx?.Session != null)
            {
                _ctx.Session.FrameReceived += OnFrame;
            }
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            if (_ctx?.Session != null)
            {
                _ctx.Session.FrameReceived -= OnFrame;
            }

            if (_ctx?.EntityWorld != null)
            {
                _ctx.EntityWorld.EntityDestroyed -= OnEntityDestroyed;
            }

            foreach (var kv in _views)
            {
                if (kv.Value != null) UnityEngine.Object.Destroy(kv.Value);
            }
            _views.Clear();
            _ctx = null;
            _query = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }

        private void OnFrame(FramePacket packet)
        {
            if (_query?.World == null) return;

            if (!packet.Snapshot.HasValue) return;
            var snap = packet.Snapshot.Value;
            if (snap.OpCode != (int)AbilityKit.Ability.Share.Impl.Moba.Services.MobaOpCode.ActorTransformSnapshot
                && snap.OpCode != (int)AbilityKit.Ability.Share.Impl.Moba.Services.MobaOpCode.EnterGameSnapshot)
            {
                return;
            }

            var dirty = _ctx != null ? _ctx.DirtyEntities : null;
            if (dirty == null || dirty.Count == 0) return;

            // Sync layer (BattleSyncFeature) updates EC model + produces dirty ids.
            for (int i = 0; i < dirty.Count; i++)
            {
                var id = dirty[i];
                if (!_query.World.IsAlive(id)) continue;
                ApplyEntityView(_query.World.Wrap(id));
            }

            dirty.Clear();
        }

        private void ApplyEntityView(EC.Entity entity)
        {
            if (!entity.TryGetComponent(out BattleNetIdComponent netIdComp) || netIdComp == null) return;
            if (!entity.TryGetComponent(out BattleTransformComponent t) || t == null) return;

            var shell = entity.TryGetComponent(out BattleViewShellComponent existing) ? existing : null;
            if (shell == null || shell.GameObject == null)
            {
                var go = CreateShellGameObject(netIdComp.NetId.Value);
                shell = new BattleViewShellComponent { GameObject = go };
                entity.AddComponent(shell);
                _views[entity.Id] = go;
            }

            shell.GameObject.transform.position = t.Position;
        }

        private void OnEntityDestroyed(EC.EntityId id)
        {
            if (_views.TryGetValue(id, out var go) && go != null)
            {
                UnityEngine.Object.Destroy(go);
            }
            _views.Remove(id);
        }

        private static GameObject CreateShellGameObject(int actorId)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Actor_{actorId}";
            go.transform.localScale = new Vector3(1f, 2f, 1f);
            return go;
        }
    }
}
