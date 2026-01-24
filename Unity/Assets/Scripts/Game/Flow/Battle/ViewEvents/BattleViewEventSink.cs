using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Battle.Entity;
using UnityEngine;
using AbilityKit.Game.Flow;
using EC = AbilityKit.Ability.EC;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    public sealed class BattleViewEventSink : IBattleViewEventSink
    {
        private readonly BattleContext _ctx;
        private readonly BattleVfxManager _vfx;
        private readonly EC.Entity _vfxNode;
        private readonly BattleFloatingTextSystem _floatingTexts;
        private readonly BattleAreaViewSystem _areaViews;
        private readonly BattleViewBinder _binder;
        private readonly IBattleEntityQuery _query;

        public BattleViewEventSink(
            BattleContext ctx,
            IBattleEntityQuery query,
            BattleViewBinder binder,
            BattleVfxManager vfx,
            EC.Entity vfxNode,
            BattleFloatingTextSystem floatingTexts,
            BattleAreaViewSystem areaViews)
        {
            _ctx = ctx;
            _query = query;
            _binder = binder;
            _vfx = vfx;
            _vfxNode = vfxNode;
            _floatingTexts = floatingTexts;
            _areaViews = areaViews;
        }

        public void OnTriggerEvent(in TriggerEvent evt)
        {
            if (evt.Id == null) return;

            if (evt.Id == DamagePipelineEvents.AfterApply)
            {
                if (evt.Payload is DamageResult r)
                {
                    HandleDamageResult(r);
                }
                return;
            }

            if (evt.Id == ProjectileTriggering.Events.Hit)
            {
                if (evt.Args == null) return;
                if (_ctx?.EntityWorld == null) return;
                if (_vfx == null) return;
                if (!_vfxNode.IsValid) return;

                if (!evt.Args.TryGetValue(ProjectileTriggering.Args.TemplateId, out var templateObj) || templateObj is not int templateId || templateId <= 0)
                {
                    return;
                }

                var proj = BattleViewFactory.TryGetProjectile(templateId);
                if (proj == null) return;
                if (proj.OnHitVfxId <= 0) return;

                if (!evt.Args.TryGetValue(ProjectileTriggering.Args.HitPoint, out var hitPointObj) || hitPointObj is not AbilityKit.Ability.Share.Math.Vec3 hitPoint)
                {
                    return;
                }

                var pos = new Vector3(hitPoint.X, hitPoint.Y, hitPoint.Z);
                _vfx.TryCreateVfxEntity(_ctx.EntityWorld, _vfxNode, proj.OnHitVfxId, default, in pos, out _);
                return;
            }
        }

        private void HandleDamageResult(DamageResult r)
        {
            if (r == null) return;
            if (_ctx?.EntityWorld == null) return;
            if (_query == null) return;
            if (_vfxNode.IsValid == false) return;
            if (r.TargetActorId <= 0) return;
            if (r.Value == 0f) return;

            var pos = Vector3.zero;
            if (_query.TryGetTransform(new BattleNetId(r.TargetActorId), out var transform) && transform != null)
            {
                pos = transform.Position;
            }
            pos += Vector3.up * 2f;

            var isHeal = r.Value < 0f;
            var v = isHeal ? -r.Value : r.Value;
            var color = isHeal ? new Color(0.2f, 1f, 0.2f, 1f) : new Color(1f, 0.2f, 0.2f, 1f);
            var text = Mathf.Abs(v) >= 1f ? Mathf.RoundToInt(Mathf.Abs(v)).ToString() : Mathf.Abs(v).ToString("0.0");
            if (isHeal) text = $"+{text}";

            _floatingTexts?.Spawn(_vfxNode, text, in pos, color);
        }

        public void OnEnterGameSnapshot(FramePacket packet, EnterMobaGameRes res)
        {
            RefreshDirtyViews();
        }

        public void OnActorTransformSnapshot(FramePacket packet, (int actorId, float x, float y, float z)[] entries)
        {
            RefreshDirtyViews();
        }

        public void OnProjectileEventSnapshot(FramePacket packet, MobaProjectileEventSnapshotCodec.Entry[] entries)
        {
            if (entries == null || entries.Length == 0) return;
            if (_ctx?.EntityWorld == null) return;
            if (_query == null) return;
            if (_vfx == null) return;
            if (!_vfxNode.IsValid) return;

            for (int i = 0; i < entries.Length; i++)
            {
                var evt = entries[i];
                if (evt.TemplateId <= 0) continue;

                var proj = BattleViewFactory.TryGetProjectile(evt.TemplateId);
                if (proj == null) continue;

                var vfxId = 0;
                if (evt.Kind == (int)MobaProjectileEventSnapshotCodec.EventKind.Spawn)
                {
                    vfxId = proj.OnSpawnVfxId;
                }
                else if (evt.Kind == (int)MobaProjectileEventSnapshotCodec.EventKind.Hit)
                {
                    vfxId = proj.OnHitVfxId;
                }
                else if (evt.Kind == (int)MobaProjectileEventSnapshotCodec.EventKind.Exit)
                {
                    vfxId = proj.OnExpireVfxId;
                }

                if (vfxId <= 0) continue;

                var pos = new Vector3(evt.X, evt.Y, evt.Z);

                var followId = default(EC.EntityId);
                if (evt.ProjectileActorId > 0 && _query.TryResolve(new BattleNetId(evt.ProjectileActorId), out var projEntity))
                {
                    followId = projEntity.Id;
                }

                _vfx.TryCreateVfxEntity(_ctx.EntityWorld, _vfxNode, vfxId, followId, in pos, out _);
            }
        }

        public void OnAreaEventSnapshot(FramePacket packet, MobaAreaEventSnapshotCodec.Entry[] entries)
        {
            if (entries == null || entries.Length == 0) return;
            if (_ctx?.EntityWorld == null) return;
            if (_query == null) return;

            _areaViews?.HandleSnapshot(_binder, _query, entries);
        }

        public void OnDamageEventSnapshot(FramePacket packet, MobaDamageEventSnapshotCodec.Entry[] entries)
        {
            if (entries == null || entries.Length == 0) return;
            if (_ctx?.EntityWorld == null) return;
            if (_query == null) return;
            if (_vfxNode.IsValid == false) return;

            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e.TargetActorId <= 0) continue;
                if (e.Value == 0f) continue;

                var pos = Vector3.zero;
                if (_query.TryGetTransform(new BattleNetId(e.TargetActorId), out var transform) && transform != null)
                {
                    pos = transform.Position;
                }
                pos += Vector3.up * 2f;

                var isHeal = e.Kind == (int)MobaDamageEventSnapshotCodec.EventKind.Heal;
                var color = isHeal ? new Color(0.2f, 1f, 0.2f, 1f) : new Color(1f, 0.2f, 0.2f, 1f);
                var text = Mathf.Abs(e.Value) >= 1f ? Mathf.RoundToInt(Mathf.Abs(e.Value)).ToString() : Mathf.Abs(e.Value).ToString("0.0");
                if (isHeal) text = $"+{text}";

                _floatingTexts?.Spawn(_vfxNode, text, in pos, color);
            }
        }

        private void RefreshDirtyViews()
        {
            if (_query?.World == null) return;

            var dirty = _ctx != null ? _ctx.DirtyEntities : null;
            if (dirty == null || dirty.Count == 0) return;

            for (int i = 0; i < dirty.Count; i++)
            {
                var id = dirty[i];
                if (!_query.World.IsAlive(id)) continue;
                _binder?.Sync(_query.World.Wrap(id));
            }

            dirty.Clear();
        }
    }
}
