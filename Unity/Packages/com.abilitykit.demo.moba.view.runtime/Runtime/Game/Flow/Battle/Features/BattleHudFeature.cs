using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Battle.View;
using AbilityKit.Game.Battle.View.Lib.Joystick;
using AbilityKit.Game.Battle.View.Lib.Skill;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Config.BattleDemo;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.World.ECS;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleHudFeature : IGamePhaseFeature
    {
        private static MobaConfigDatabase _configs;

        private BattleContext _ctx;
        private Camera _camera;

        private EC.IEntity _hudNode;
        private Canvas _canvas;
        private RectTransform _root;

        private BattleHudConfig _config;
        private BattleHudBinder _binder;

        private GameObject _inputUiRoot;
        private JoystickAreaView _moveJoystick;
        private BattleHudInputView _inputView;
        private Button _infoButton;
        private IDisposable _entityDestroyedSub;

        private BattleHudSkillAimInputMapper _skillAimMapper;

        private GameObject _aimPreview;

        private IDisposable _subDamageEvents;
        private IDisposable _subEnterGame;

        private SkillButtonView _skill1View;
        private SkillButtonView _skill2View;
        private SkillButtonView _skill3View;

        public void OnAttach(in GamePhaseContext ctx)
        {
            ctx.Root.TryGetRef(out _ctx);

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

            EnsureEventSystem();
            EnsureInputUi();

            _entityDestroyedSub?.Dispose();
            if (_ctx?.EntityWorld != null)
            {
                _entityDestroyedSub = _ctx.EntityWorld.EntityDestroyed(OnEntityDestroyed);
            }

            if (_ctx?.FrameSnapshots != null)
            {
                _subEnterGame = _ctx.FrameSnapshots.Subscribe<EnterMobaGameRes>((int)MobaOpCode.EnterGameSnapshot, OnEnterGameSnapshot);
                _subDamageEvents = _ctx.FrameSnapshots.Subscribe<MobaDamageEventSnapshotCodec.Entry[]>((int)MobaOpCode.DamageEventSnapshot, OnDamageEventSnapshot);
            }
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            if (_ctx?.FrameSnapshots != null)
            {
                _subEnterGame?.Dispose();
                _subDamageEvents?.Dispose();
            }
            _subEnterGame = null;
            _subDamageEvents = null;

            _entityDestroyedSub?.Dispose();
            _entityDestroyedSub = null;

            _binder?.Clear();
            _binder = null;

            if (_inputUiRoot != null)
            {
                UnityEngine.Object.Destroy(_inputUiRoot);
            }
            _inputUiRoot = null;
            _moveJoystick = null;
            _inputView = null;
            _infoButton = null;
            _skillAimMapper = null;

            _skill1View = null;
            _skill2View = null;
            _skill3View = null;

            if (_aimPreview != null)
            {
                UnityEngine.Object.Destroy(_aimPreview);
            }
            _aimPreview = null;

            if (_ctx != null)
            {
                _ctx.HudSkillAiming = false;
                _ctx.HudSkillAimSlot = 0;
                _ctx.HudSkillAimDx = 0f;
                _ctx.HudSkillAimDz = 0f;
            }

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

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            go.hideFlags = HideFlags.DontSave;
        }

        private void EnsureInputUi()
        {
            if (_root == null) return;
            if (_ctx == null) return;
            if (_inputUiRoot != null) return;

            _inputUiRoot = new GameObject("BattleHudInput", typeof(RectTransform));
            _inputUiRoot.transform.SetParent(_root, worldPositionStays: false);
            _inputUiRoot.SetActive(false);

            var rt = _inputUiRoot.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _inputView = _inputUiRoot.AddComponent<BattleHudInputView>();

            var joystickArea = new GameObject("MoveJoystick", typeof(RectTransform), typeof(Image));
            joystickArea.transform.SetParent(_inputUiRoot.transform, worldPositionStays: false);
            var joystickAreaRt = joystickArea.GetComponent<RectTransform>();
            joystickAreaRt.anchorMin = new Vector2(0f, 0f);
            joystickAreaRt.anchorMax = new Vector2(0f, 0f);
            joystickAreaRt.anchoredPosition = new Vector2(180f, 180f);
            joystickAreaRt.sizeDelta = new Vector2(360f, 360f);

            var areaImg = joystickArea.GetComponent<Image>();
            areaImg.color = new Color(1f, 1f, 1f, 0.001f);
            areaImg.raycastTarget = true;

            var outer = new GameObject("Outer", typeof(RectTransform), typeof(Image));
            outer.transform.SetParent(joystickArea.transform, worldPositionStays: false);
            var outerRt = outer.GetComponent<RectTransform>();
            outerRt.anchorMin = new Vector2(0.5f, 0.5f);
            outerRt.anchorMax = new Vector2(0.5f, 0.5f);
            outerRt.anchoredPosition = Vector2.zero;
            outerRt.sizeDelta = new Vector2(220f, 220f);
            var outerImg = outer.GetComponent<Image>();
            outerImg.color = new Color(1f, 1f, 1f, 0.15f);
            outerImg.raycastTarget = true;

            var inner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            inner.transform.SetParent(joystickArea.transform, worldPositionStays: false);
            var innerRt = inner.GetComponent<RectTransform>();
            innerRt.anchorMin = new Vector2(0.5f, 0.5f);
            innerRt.anchorMax = new Vector2(0.5f, 0.5f);
            innerRt.anchoredPosition = Vector2.zero;
            innerRt.sizeDelta = new Vector2(90f, 90f);
            var innerImg = inner.GetComponent<Image>();
            innerImg.color = new Color(1f, 1f, 1f, 0.25f);
            innerImg.raycastTarget = false;

            _moveJoystick = joystickArea.AddComponent<JoystickAreaView>();
            SetPrivateField(_moveJoystick, "_area", joystickAreaRt);
            SetPrivateField(_moveJoystick, "_outer", outerRt);
            SetPrivateField(_moveJoystick, "_inner", innerRt);
            SetPrivateField(_moveJoystick, "_canvas", _canvas);

            SetPrivateField(_inputView, "_moveJoystick", _moveJoystick);

            _moveJoystick.OnBegin += OnMoveBegin;
            _moveJoystick.OnEnd += OnMoveEnd;

            var moveMapper = _inputUiRoot.AddComponent<BattleHudMoveInputMapper>();
            SetPrivateField(moveMapper, "_hud", _inputView);
            moveMapper.MoveDxDzChanged += OnMoveDxDzChanged;

            var skillAimMapper = _inputUiRoot.AddComponent<BattleHudSkillAimInputMapper>();
            SetPrivateField(skillAimMapper, "_hud", _inputView);
            _skillAimMapper = skillAimMapper;

            _skillAimMapper.SkillAimStart += OnSkillAimStart;
            _skillAimMapper.SkillAimUpdate += OnSkillAimUpdate;
            _skillAimMapper.SkillAimEnd += OnSkillAimEnd;

            var skill1 = CreateSkillButton(1, "Skill1", new Vector2(-260f, 200f));
            var skill2 = CreateSkillButton(2, "Skill2", new Vector2(-140f, 110f));
            var skill3 = CreateSkillButton(3, "Skill3", new Vector2(-120f, 260f));

            _skill1View = skill1;
            _skill2View = skill2;
            _skill3View = skill3;

            SetPrivateField(_inputView, "_skill1", skill1);
            SetPrivateField(_inputView, "_skill2", skill2);
            SetPrivateField(_inputView, "_skill3", skill3);

            _inputView.SkillClick += OnSkillClick;

            _infoButton = CreateInfoButton(new Vector2(-80f, -80f));

            _inputUiRoot.SetActive(true);
        }

        private void OnMoveBegin()
        {
            if (_ctx == null) return;
            _ctx.HudHasMove = true;
        }

        private void OnMoveEnd()
        {
            if (_ctx == null) return;
            _ctx.HudHasMove = false;
            _ctx.HudMoveDx = 0f;
            _ctx.HudMoveDz = 0f;
        }

        private void OnMoveDxDzChanged(float dx, float dz)
        {
            if (_ctx == null) return;
            _ctx.HudMoveDx = dx;
            _ctx.HudMoveDz = dz;
        }

        private void OnSkillClick(int slot)
        {
            if (_ctx == null) return;
            _ctx.HudSkillClickSlot = slot;
        }

        private void OnSkillAimStart(int slot, Vector2 aim)
        {
            if (_ctx == null) return;
            _ctx.HudSkillAiming = true;
            _ctx.HudSkillAimSlot = slot;
            _ctx.HudSkillAimDx = aim.x;
            _ctx.HudSkillAimDz = aim.y;
        }

        private void OnSkillAimUpdate(int slot, Vector2 aim)
        {
            if (_ctx == null) return;
            _ctx.HudSkillAiming = true;
            _ctx.HudSkillAimSlot = slot;
            _ctx.HudSkillAimDx = aim.x;
            _ctx.HudSkillAimDz = aim.y;
        }

        private void OnSkillAimEnd(int slot, Vector2 aim)
        {
            if (_ctx == null) return;
            _ctx.HudSkillAiming = false;
            _ctx.HudSkillAimSlot = slot;
            _ctx.HudSkillAimDx = aim.x;
            _ctx.HudSkillAimDz = aim.y;

            _ctx.HudSkillAimSubmit = true;
            _ctx.HudSkillAimSubmitSlot = slot;
            _ctx.HudSkillAimSubmitDx = aim.x;
            _ctx.HudSkillAimSubmitDz = aim.y;
        }

        private SkillButtonView CreateSkillButton(int slot, string name, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_inputUiRoot.transform, worldPositionStays: false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(110f, 110f);

            var img = go.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.2f);
            img.raycastTarget = true;

            var view = go.AddComponent<SkillButtonView>();
            SetPrivateField(view, "_buttonRect", rt);
            SetPrivateField(view, "_uiRootRect", _root);
            SetPrivateField(view, "_canvas", _canvas);

            var cfg = SkillButtonConfig.Default;
            cfg.EnableAim = true;
            cfg.AimMaxRadius = 220f;
            cfg.AimMode = slot == 1 ? SkillAimMode.Direction : SkillAimMode.Point;
            SetPrivateField(view, "_config", cfg);

            return view;
        }

        private void OnEnterGameSnapshot(ISnapshotEnvelope packet, EnterMobaGameRes res)
        {
            TryApplySkillButtonTemplates(res);
        }

        private void TryApplySkillButtonTemplates(EnterMobaGameRes res)
        {
            if (_ctx == null) return;
            if (_skill1View == null && _skill2View == null && _skill3View == null) return;

            var playerId = _ctx.Plan.PlayerId;
            if (string.IsNullOrEmpty(playerId)) return;

            var loadout = FindLocalLoadout(res.PlayersLoadout, playerId);
            if (!loadout.HasValue) return;

            _configs ??= MobaConfigLoader.LoadDefault();
            if (_configs == null) return;

            ApplySkillButtonTemplate(1, _skill1View, loadout.Value, _configs);
            ApplySkillButtonTemplate(2, _skill2View, loadout.Value, _configs);
            ApplySkillButtonTemplate(3, _skill3View, loadout.Value, _configs);
        }

        private static MobaPlayerLoadout? FindLocalLoadout(MobaPlayerLoadout[] loadouts, string playerId)
        {
            if (loadouts == null || loadouts.Length == 0) return null;
            if (string.IsNullOrEmpty(playerId)) return null;

            for (int i = 0; i < loadouts.Length; i++)
            {
                var l = loadouts[i];
                if (l.PlayerId.Value == playerId)
                {
                    return l;
                }
            }

            return null;
        }

        private static void ApplySkillButtonTemplate(int slot, SkillButtonView view, in MobaPlayerLoadout loadout, MobaConfigDatabase configs)
        {
            if (view == null) return;
            if (slot <= 0) return;

            var cfg = view.Config;

            var skills = loadout.SkillIds;
            if (skills == null || skills.Length < slot) { view.Config = cfg; return; }
            var skillId = skills[slot - 1];
            if (skillId <= 0) { view.Config = cfg; return; }

            SkillMO skill;
            try { skill = configs.GetSkill(skillId); }
            catch { view.Config = cfg; return; }
            if (skill == null) { view.Config = cfg; return; }

            var templateId = skill.SkillButtonTemplateId;
            if (templateId <= 0) { view.Config = cfg; return; }

            SkillButtonTemplateMO template;
            try { template = configs.GetSkillButtonTemplate(templateId); }
            catch { view.Config = cfg; return; }
            if (template == null) { view.Config = cfg; return; }

            cfg.LongPressSeconds = template.LongPressSeconds > 0f ? template.LongPressSeconds : cfg.LongPressSeconds;
            cfg.DragThreshold = template.DragThreshold > 0f ? template.DragThreshold : cfg.DragThreshold;
            cfg.EnableAim = template.EnableAim;
            cfg.AimMaxRadius = template.AimMaxRadius > 0f ? template.AimMaxRadius : cfg.AimMaxRadius;

            cfg.AimMode = template.AimMode == (int)SkillAimMode.Point ? SkillAimMode.Point : SkillAimMode.Direction;
            cfg.UsePointMode = template.UsePointMode == (int)SkillUsePointMode.Aim ? SkillUsePointMode.Aim : template.UsePointMode == (int)SkillUsePointMode.TargetPoint ? SkillUsePointMode.TargetPoint : SkillUsePointMode.None;
            cfg.SelectRange = template.SelectRange;
            cfg.FaceToAim = template.FaceToAim;

            view.Config = cfg;
        }

        private Button CreateInfoButton(Vector2 anchoredPos)
        {
            var go = new GameObject("Info", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_inputUiRoot.transform, worldPositionStays: false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(90f, 45f);

            var img = go.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.18f);
            img.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(OnInfoClick);
            return btn;
        }

        private void OnInfoClick()
        {
            Debug.Log("BattleHud: Info clicked");
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null) return;
            var f = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null) return;
            f.SetValue(target, value);
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_binder == null) return;
            _binder.Tick(deltaTime);
            TickAimPreview();
        }

        private void TickAimPreview()
        {
            if (_ctx == null || _ctx.EntityQuery == null)
            {
                if (_aimPreview != null) _aimPreview.SetActive(false);
                return;
            }

            if (!_ctx.HudSkillAiming)
            {
                if (_aimPreview != null) _aimPreview.SetActive(false);
                return;
            }

            var casterId = _ctx.LocalActorId;
            if (casterId <= 0)
            {
                if (_aimPreview != null) _aimPreview.SetActive(false);
                return;
            }

            if (!_ctx.EntityQuery.TryResolve(new BattleNetId(casterId), out var caster))
            {
                if (_aimPreview != null) _aimPreview.SetActive(false);
                return;
            }

            if (!caster.TryGetRef(out AbilityKit.Game.Battle.Component.BattleTransformComponent t) || t == null)
            {
                if (_aimPreview != null) _aimPreview.SetActive(false);
                return;
            }

            if (_aimPreview == null)
            {
                _aimPreview = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _aimPreview.name = "SkillAimPreview";
                _aimPreview.hideFlags = HideFlags.DontSave;
                _aimPreview.transform.localScale = Vector3.one * 0.35f;
                var col = _aimPreview.GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }

            var casterPos = t.Position;
            var pos = casterPos + new Vector3(_ctx.HudSkillAimDx, 0f, _ctx.HudSkillAimDz);
            _aimPreview.transform.position = pos;
            _aimPreview.SetActive(true);
        }

        private void OnDamageEventSnapshot(ISnapshotEnvelope packet, MobaDamageEventSnapshotCodec.Entry[] entries)
        {
            if (entries == null || entries.Length == 0) return;
            _binder?.OnDamageEvents(entries);
        }

        private void OnEntityDestroyed(EC.EntityDestroyed evt)
        {
            _binder?.OnEntityDestroyed(evt.EntityId);
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

            public void OnEntityDestroyed(EC.IEntityId id)
            {
                if (_ctx?.EntityQuery == null) return;
                if (!_ctx.EntityQuery.World.IsAlive(id)) return;

                var e = _ctx.EntityQuery.World.Wrap(id);
                if (!e.TryGetRef(out BattleNetIdComponent netIdComp) || netIdComp == null) return;
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
                if (!e.TryGetRef(out AbilityKit.Game.Battle.Component.BattleTransformComponent t) || t == null) return false;
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
