using System;
using System.Collections.Generic;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleViewFeature : IGamePhaseFeature
    {
        private BattleSessionFeature _session;
        private readonly Dictionary<int, GameObject> _views = new Dictionary<int, GameObject>();

        public void OnAttach(in GamePhaseContext ctx)
        {
            ctx.Root.TryGetComponent(out _session);
            if (_session?.Session != null)
            {
                _session.Session.FrameReceived += OnFrame;
            }
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            if (_session?.Session != null)
            {
                _session.Session.FrameReceived -= OnFrame;
            }

            foreach (var kv in _views)
            {
                if (kv.Value != null) UnityEngine.Object.Destroy(kv.Value);
            }
            _views.Clear();
            _session = null;
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
        }

        private void ApplyEnterGameSnapshot(byte[] payload)
        {
            if (payload == null || payload.Length == 0) return;

            var res = EnterMobaGameCodec.DeserializeRes(payload);
            if (res.Payload == null || res.Payload.Length < 12) return;

            var x = BitConverter.ToSingle(res.Payload, 0);
            var y = BitConverter.ToSingle(res.Payload, 4);
            var z = BitConverter.ToSingle(res.Payload, 8);

            var go = GetOrCreate(res.LocalActorId);
            go.transform.position = new Vector3(x, y, z);
        }

        private void ApplyTransformSnapshot(byte[] payload)
        {
            var entries = MobaActorTransformSnapshotCodec.Deserialize(payload);
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                var go = GetOrCreate(e.actorId);
                go.transform.position = new Vector3(e.x, e.y, e.z);
            }
        }

        private GameObject GetOrCreate(int actorId)
        {
            if (_views.TryGetValue(actorId, out var go) && go != null) return go;

            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Actor_{actorId}";
            go.transform.localScale = new Vector3(1f, 2f, 1f);
            _views[actorId] = go;
            return go;
        }
    }
}
