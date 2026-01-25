using System.Collections.Generic;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Game.Battle.Entity;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleAreaViewSystem
    {
        private sealed class AoeViewHandle
        {
            public int AreaId;
            public int TemplateId;
            public GameObject ModelGo;
            public GameObject VfxGo;
        }

        private readonly Dictionary<int, AoeViewHandle> _aoeViews = new Dictionary<int, AoeViewHandle>(128);

        public void HandleSnapshot(
            BattleViewBinder binder,
            IBattleEntityQuery query,
            MobaAreaEventSnapshotCodec.Entry[] entries)
        {
            if (entries == null || entries.Length == 0) return;
            if (query == null) return;

            var configs = BattleViewFactory.GetOrLoadConfigs();
            if (configs == null) return;

            for (int i = 0; i < entries.Length; i++)
            {
                var evt = entries[i];
                if (evt.AreaId <= 0) continue;

                var kind = evt.Kind;
                if (kind == (int)MobaAreaEventSnapshotCodec.EventKind.Spawn)
                {
                    if (_aoeViews.ContainsKey(evt.AreaId)) continue;

                    var aoe = BattleViewFactory.TryGetAoe(evt.TemplateId);
                    if (aoe == null) continue;

                    var pos = new Vector3(evt.X, evt.Y, evt.Z);
                    pos += new Vector3(aoe.OffsetX, aoe.OffsetY, aoe.OffsetZ);

                    Transform attach = null;
                    if (aoe.AttachMode == 1 && evt.OwnerActorId > 0)
                    {
                        if (binder != null && binder.TryGetAttachRoot(new BattleNetId(evt.OwnerActorId), out var t) && t != null)
                        {
                            attach = t;
                        }
                    }

                    var h = new AoeViewHandle { AreaId = evt.AreaId, TemplateId = evt.TemplateId };

                    if (aoe.ModelId > 0)
                    {
                        h.ModelGo = BattleViewFactory.CreateModelGo(aoe.ModelId);
                        if (h.ModelGo != null)
                        {
                            if (attach != null)
                            {
                                h.ModelGo.transform.SetParent(attach, worldPositionStays: false);
                                h.ModelGo.transform.localPosition = Vector3.zero;
                            }
                            else
                            {
                                h.ModelGo.transform.position = pos;
                            }
                        }
                    }

                    if (aoe.VfxId > 0)
                    {
                        h.VfxGo = BattleViewFactory.CreateVfxGo(aoe.VfxId);
                        if (h.VfxGo != null)
                        {
                            if (attach != null)
                            {
                                h.VfxGo.transform.SetParent(attach, worldPositionStays: false);
                                h.VfxGo.transform.localPosition = Vector3.zero;
                            }
                            else
                            {
                                h.VfxGo.transform.position = pos;
                            }
                        }
                    }

                    _aoeViews[evt.AreaId] = h;
                }
                else if (kind == (int)MobaAreaEventSnapshotCodec.EventKind.Expire)
                {
                    if (_aoeViews.TryGetValue(evt.AreaId, out var h) && h != null)
                    {
                        if (h.ModelGo != null) Object.Destroy(h.ModelGo);
                        if (h.VfxGo != null) Object.Destroy(h.VfxGo);
                        _aoeViews.Remove(evt.AreaId);
                    }
                }
            }
        }

        public void Clear()
        {
            foreach (var kv in _aoeViews)
            {
                var h = kv.Value;
                if (h == null) continue;
                if (h.ModelGo != null) Object.Destroy(h.ModelGo);
                if (h.VfxGo != null) Object.Destroy(h.VfxGo);
            }
            _aoeViews.Clear();
        }
    }
}
