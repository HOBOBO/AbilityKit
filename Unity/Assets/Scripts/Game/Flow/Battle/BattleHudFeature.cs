using System;
using System.Collections.Generic;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Game.Battle.Entity;
using UnityEngine;
using UnityEngine.UI;
using EC = AbilityKit.Ability.EC;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleHudFeature : IGamePhaseFeature
    {
        private BattleContext _ctx;
        private Camera _camera;

        private EC.Entity _hudNode;
        private Canvas _canvas;
        private RectTransform _root;

        private BattleHudConfig _config;
        private BattleHudBinder _binder;

        private IDisposable _subDamageEvents;

        public void OnAttach(in GamePhaseContext ctx)
        {
            ctx.Root.TryGetComponent(out _ctx);

            _camera = Camera.main;
            _config = BattleHudConfig.Default;

            if (_ctx != null && _ctx.EntityNode.IsValid)
            {
                _hudNode = _ctx.EntityNode.AddChild("BattleHud");
            }

            var go = new GameObject("BattleHudCanvas");
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            _root = _canvas.GetComponent<RectTransform>();

            _binder = new BattleHudBinder(_config, _root, _camera, _ctx);

            if (_ctx?.EntityWorld != null)
            {
                _ctx.EntityWorld.EntityDestroyed += OnEntityDestroyed;
            }

            if (_ctx?.FrameSnapshots != null)
            {
                _subDamageEvents = _ctx.FrameSnapshots.Subscribe<MobaDamageEventSnapshotCodec.Entry[]>((int)MobaOpCode.DamageEventSnapshot, OnDamageEventSnapshot);
            }
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            if (_ctx?.FrameSnapshots != null)
            {
                _subDamageEvents?.Dispose();
            }
            _subDamageEvents = null;

            if (_ctx?.EntityWorld != null)
            {
                _ctx.EntityWorld.EntityDestroyed -= OnEntityDestroyed;
            }

            _binder?.Clear();
            _binder = null;

            if (_canvas != null)
            {
                UnityEngine.Object.Destroy(_canvas.gameObject);
            }

            _canvas = null;
            _root = null;
            _hudNode = default;

            _ctx = null;
            _camera = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_binder == null) return;
            _binder.Tick(deltaTime);
        }

        private void OnDamageEventSnapshot(FramePacket packet, MobaDamageEventSnapshotCodec.Entry[] entries)
        {
            if (entries == null || entries.Length == 0) return;
            _binder?.OnDamageEvents(entries);
        }

        private void OnEntityDestroyed(EC.EntityId id)
        {
            _binder?.OnEntityDestroyed(id);
        }

        private sealed class BattleHudBinder
        {
            private readonly BattleHudConfig _cfg;
            private readonly RectTransform _root;
            private readonly Camera _camera;
            private readonly BattleContext _ctx;

            private readonly Dictionary<int, HudHandle> _byActorId = new Dictionary<int, HudHandle>(64);
            private readonly List<FloatingTextHandle> _floating = new List<FloatingTextHandle>(64);
            private readonly Stack<FloatingTextHandle> _floatingPool = new Stack<FloatingTextHandle>(64);

            private sealed class HudHandle
            {
                public int ActorId;
                public RectTransform Root;
                public Image HpFill;
                public float Hp;
                public float MaxHp;
                public Vector3 WorldOffset;
            }

            private sealed class FloatingTextHandle
            {
                public int TargetActorId;
                public RectTransform Root;
                public Text Text;
                public float Age;
                public float Lifetime;
                public Vector3 WorldOffset;
                public Vector2 ScreenOffset;
            }

            public BattleHudBinder(BattleHudConfig cfg, RectTransform root, Camera camera, BattleContext ctx)
            {
                _cfg = cfg;
                _root = root;
                _camera = camera;
                _ctx = ctx;
            }

            public void OnDamageEvents(MobaDamageEventSnapshotCodec.Entry[] entries)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    var e = entries[i];
                    if (e.TargetActorId <= 0) continue;

                    var absValue = Mathf.Abs(e.Value);
                    if (absValue <= 0.0001f) continue;

                    EnsureHud(e.TargetActorId);
                    UpdateHp(e.TargetActorId, e.TargetHp, e.TargetMaxHp);

                    var isHeal = e.Kind == (int)MobaDamageEventSnapshotCodec.EventKind.Heal;
                    var sign = isHeal ? "+" : "-";
                    var text = sign + Mathf.RoundToInt(absValue).ToString();

                    SpawnFloatingText(e.TargetActorId, text, isHeal);
                }
            }

            public void Tick(float deltaTime)
            {
                if (_camera == null) return;

                var cam = _camera;
                var canvas = _root != null ? _root.GetComponentInParent<Canvas>() : null;
                if (canvas == null) return;

                foreach (var kv in _byActorId)
                {
                    var h = kv.Value;
                    if (h?.Root == null) continue;

                    if (!TryGetActorWorldPos(h.ActorId, out var worldPos)) continue;
                    var screen = cam.WorldToScreenPoint(worldPos + h.WorldOffset);
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam, out var local))
                    {
                        h.Root.anchoredPosition = local;
                    }
                }

                for (int i = _floating.Count - 1; i >= 0; i--)
                {
                    var ft = _floating[i];
                    if (ft?.Root == null)
                    {
                        _floating.RemoveAt(i);
                        continue;
                    }

                    ft.Age += deltaTime;
                    if (ft.Age >= ft.Lifetime)
                    {
                        RecycleFloatingText(ft);
                        _floating.RemoveAt(i);
                        continue;
                    }

                    if (!TryGetActorWorldPos(ft.TargetActorId, out var worldPos2)) continue;
                    var screen2 = cam.WorldToScreenPoint(worldPos2 + ft.WorldOffset);

                    var t = ft.Age / Mathf.Max(0.001f, ft.Lifetime);
                    var y = Mathf.Lerp(0f, _cfg.FloatingTextRisePixels, t);

                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen2, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam, out var local2))
                    {
                        ft.Root.anchoredPosition = local2 + ft.ScreenOffset + new Vector2(0f, y);
                    }
                    if (ft.Text != null)
                    {
                        var c = ft.Text.color;
                        c.a = 1f - t;
                        ft.Text.color = c;
                    }
                }
            }

            public void OnEntityDestroyed(EC.EntityId id)
            {
                if (_ctx?.EntityQuery == null) return;
                if (!_ctx.EntityQuery.World.IsAlive(id)) return;

                var e = _ctx.EntityQuery.World.Wrap(id);
                if (!e.TryGetComponent(out BattleNetIdComponent netIdComp) || netIdComp == null) return;
                var actorId = netIdComp.NetId.Value;
                if (actorId <= 0) return;

                if (_byActorId.TryGetValue(actorId, out var hud) && hud != null)
                {
                    if (hud.Root != null) UnityEngine.Object.Destroy(hud.Root.gameObject);
                    _byActorId.Remove(actorId);
                }

                for (int i = _floating.Count - 1; i >= 0; i--)
                {
                    var ft = _floating[i];
                    if (ft == null) { _floating.RemoveAt(i); continue; }
                    if (ft.TargetActorId != actorId) continue;
                    RecycleFloatingText(ft);
                    _floating.RemoveAt(i);
                }
            }

            public void Clear()
            {
                foreach (var kv in _byActorId)
                {
                    var h = kv.Value;
                    if (h?.Root != null) UnityEngine.Object.Destroy(h.Root.gameObject);
                }
                _byActorId.Clear();

                for (int i = 0; i < _floating.Count; i++)
                {
                    var ft = _floating[i];
                    if (ft?.Root != null) UnityEngine.Object.Destroy(ft.Root.gameObject);
                }
                _floating.Clear();
                _floatingPool.Clear();
            }

            private void EnsureHud(int actorId)
            {
                if (_byActorId.ContainsKey(actorId)) return;

                var prefab = !string.IsNullOrEmpty(_cfg.HpBarPrefabPath)
                    ? Resources.Load<GameObject>(_cfg.HpBarPrefabPath)
                    : null;

                GameObject go;
                if (prefab != null)
                {
                    go = UnityEngine.Object.Instantiate(prefab, _root);
                }
                else
                {
                    go = CreateFallbackHpBar();
                    go.transform.SetParent(_root, worldPositionStays: false);
                }

                var rt = go.GetComponent<RectTransform>();
                if (rt == null) rt = go.AddComponent<RectTransform>();

                var fill = go.GetComponentInChildren<Image>();

                var h = new HudHandle
                {
                    ActorId = actorId,
                    Root = rt,
                    HpFill = fill,
                    Hp = 0f,
                    MaxHp = 0f,
                    WorldOffset = _cfg.HpBarWorldOffset,
                };

                _byActorId[actorId] = h;
            }

            private void UpdateHp(int actorId, float hp, float maxHp)
            {
                if (!_byActorId.TryGetValue(actorId, out var h) || h == null) return;
                h.Hp = hp;
                h.MaxHp = maxHp;

                if (h.HpFill != null)
                {
                    var denom = Mathf.Max(1f, maxHp);
                    h.HpFill.fillAmount = Mathf.Clamp01(hp / denom);
                }
            }

            private void SpawnFloatingText(int targetActorId, string text, bool heal)
            {
                var ft = _floatingPool.Count > 0 ? _floatingPool.Pop() : null;
                if (ft == null)
                {
                    var go = CreateFallbackFloatingText();
                    go.transform.SetParent(_root, worldPositionStays: false);

                    ft = new FloatingTextHandle
                    {
                        Root = go.GetComponent<RectTransform>(),
                        Text = go.GetComponentInChildren<Text>(),
                    };
                }
                else
                {
                    if (ft.Root != null && ft.Root.parent != _root) ft.Root.SetParent(_root, worldPositionStays: false);
                    if (ft.Root != null) ft.Root.gameObject.SetActive(true);
                }

                ft.TargetActorId = targetActorId;
                ft.Age = 0f;
                ft.Lifetime = _cfg.FloatingTextLifetime;
                ft.WorldOffset = _cfg.FloatingTextWorldOffset;
                ft.ScreenOffset = UnityEngine.Random.insideUnitCircle * _cfg.FloatingTextSpreadPixels;

                if (ft.Text != null)
                {
                    ft.Text.text = text;
                    ft.Text.color = heal ? _cfg.HealTextColor : _cfg.DamageTextColor;
                }

                _floating.Add(ft);
            }

            private void RecycleFloatingText(FloatingTextHandle ft)
            {
                if (ft == null) return;
                if (ft.Root != null) ft.Root.gameObject.SetActive(false);
                _floatingPool.Push(ft);
            }

            private bool TryGetActorWorldPos(int actorId, out Vector3 pos)
            {
                pos = default;
                if (_ctx?.EntityQuery == null) return false;
                if (!_ctx.EntityQuery.TryResolve(new BattleNetId(actorId), out var e)) return false;
                if (!e.TryGetComponent(out AbilityKit.Game.Battle.Component.BattleTransformComponent t) || t == null) return false;
                pos = t.Position;
                return true;
            }

            private static GameObject CreateFallbackHpBar()
            {
                var root = new GameObject("HpBar");
                var rt = root.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(80, 10);

                var bg = new GameObject("Bg");
                bg.transform.SetParent(root.transform, worldPositionStays: false);
                var bgImg = bg.AddComponent<Image>();
                bgImg.color = new Color(0f, 0f, 0f, 0.6f);
                var bgRt = bg.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = Vector2.zero;
                bgRt.offsetMax = Vector2.zero;

                var fill = new GameObject("Fill");
                fill.transform.SetParent(bg.transform, worldPositionStays: false);
                var fillImg = fill.AddComponent<Image>();
                fillImg.color = Color.red;
                fillImg.type = Image.Type.Filled;
                fillImg.fillMethod = Image.FillMethod.Horizontal;
                fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
                fillImg.fillAmount = 1f;

                var fillRt = fill.GetComponent<RectTransform>();
                fillRt.anchorMin = Vector2.zero;
                fillRt.anchorMax = Vector2.one;
                fillRt.offsetMin = Vector2.zero;
                fillRt.offsetMax = Vector2.zero;

                return root;
            }

            private static GameObject CreateFallbackFloatingText()
            {
                var root = new GameObject("FloatingText");
                var rt = root.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(200, 40);

                var tgo = new GameObject("Text");
                tgo.transform.SetParent(root.transform, worldPositionStays: false);
                var text = tgo.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                text.alignment = TextAnchor.MiddleCenter;
                text.text = "0";
                text.color = Color.white;

                var trt = tgo.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = Vector2.zero;
                trt.offsetMax = Vector2.zero;

                return root;
            }
        }
    }
}
