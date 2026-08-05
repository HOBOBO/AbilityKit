using System;
using AbilityKit.Game.Battle.View.Lib.Joystick;
using AbilityKit.Game.Battle.View.Lib.Skill;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudInputControlFactory
    {
        private static readonly Color TransparentInputColor =
            new Color(0f, 0f, 0f, 0.001f);
        private static readonly Color JoystickOuterColor =
            new Color(0.04f, 0.07f, 0.1f, 0.72f);
        private static readonly Color JoystickInnerColor =
            new Color(0.08f, 0.68f, 0.82f, 0.9f);
        private static readonly Color SkillButtonColor =
            new Color(0.04f, 0.08f, 0.13f, 0.82f);
        private static readonly Color InfoButtonColor =
            new Color(0.05f, 0.09f, 0.14f, 0.78f);

        private readonly BattleHudImageElementFactory _images;

        public BattleHudInputControlFactory()
            : this(new BattleHudImageElementFactory())
        {
        }

        public BattleHudInputControlFactory(BattleHudImageElementFactory images)
        {
            _images = images ?? new BattleHudImageElementFactory();
        }

        public JoystickAreaView CreateMoveJoystick(Transform parent, Canvas canvas)
        {
            var area = _images.Create(
                "MoveJoystick",
                parent,
                Vector2.zero,
                Vector2.zero,
                new Vector2(180f, 180f),
                new Vector2(360f, 360f),
                TransparentInputColor,
                raycastTarget: true);

            var outerRt = CreateJoystickPart(
                area.GameObject.transform,
                "Outer",
                new Vector2(220f, 220f),
                JoystickOuterColor,
                raycastTarget: true);
            var innerRt = CreateJoystickPart(
                area.GameObject.transform,
                "Inner",
                new Vector2(90f, 90f),
                JoystickInnerColor,
                raycastTarget: false);

            var joystick = area.GameObject.AddComponent<JoystickAreaView>();
            joystick.Initialize(area.Rect, outerRt, innerRt, canvas, JoystickConfig.Default);
            return joystick;
        }

        public SkillButtonView CreateSkillButton(
            Transform parent,
            RectTransform root,
            Canvas canvas,
            int slot,
            string name,
            Vector2 anchoredPos)
        {
            var element = _images.Create(
                name,
                parent,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                anchoredPos,
                new Vector2(110f, 110f),
                SkillButtonColor,
                raycastTarget: true);

            var cfg = SkillButtonConfig.Default;
            cfg.EnableAim = false;
            cfg.IndicatorShape = SkillAimIndicatorShape.Hidden;

            var cooldownLayer = CreateCooldownLayer(element.GameObject.transform, name + "CooldownLayer");
            var cooldownOverlay = CreateCooldownOverlay(cooldownLayer, name + "CooldownOverlay");
            var cooldownText = CreateCooldownText(cooldownLayer, name + "CooldownText");
            var indicator = CreateSkillAimIndicator(parent, name + "AimIndicator");
            var view = element.GameObject.AddComponent<SkillButtonView>();
            view.Initialize(element.Rect, root, canvas, cfg, indicator, cooldownOverlay, cooldownText);
            return view;
        }

        public Button CreateInfoButton(Transform parent, Vector2 anchoredPos, Action clicked)
        {
            var element = _images.Create(
                "Info",
                parent,
                Vector2.one,
                Vector2.one,
                anchoredPos,
                new Vector2(90f, 45f),
                InfoButtonColor,
                raycastTarget: true,
                typeof(Button));

            var btn = element.GameObject.GetComponent<Button>();
            if (clicked != null)
            {
                btn.onClick.AddListener(() => clicked());
            }

            return btn;
        }

        private static Transform CreateCooldownLayer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, worldPositionStays: false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var canvas = go.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 10;
            return go.transform;
        }

        private Image CreateCooldownOverlay(Transform parent, string name)
        {
            var overlay = _images.Create(
                name,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(110f, 110f),
                new Color(0f, 0f, 0f, 0.58f),
                raycastTarget: false).GameObject.GetComponent<Image>();
            overlay.type = Image.Type.Filled;
            overlay.fillMethod = Image.FillMethod.Radial360;
            overlay.fillOrigin = (int)Image.Origin360.Top;
            overlay.fillClockwise = false;
            overlay.fillAmount = 0f;
            overlay.gameObject.SetActive(false);
            return overlay;
        }

        private Text CreateCooldownText(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, worldPositionStays: false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var text = go.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 30;
            text.fontStyle = FontStyle.Bold;
            text.raycastTarget = false;
            text.gameObject.SetActive(false);
            return text;
        }

        private SkillAimIndicatorView CreateSkillAimIndicator(Transform parent, string name)
        {
            var indicatorGo = new GameObject(name, typeof(RectTransform));
            indicatorGo.transform.SetParent(parent, worldPositionStays: false);
            indicatorGo.SetActive(false);

            var indicatorRt = indicatorGo.GetComponent<RectTransform>();
            BattleHudRectTransformLayout.StretchToParent(indicatorRt);

            var ring = _images.Create(
                "Ring",
                indicatorGo.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(160f, 160f),
                new Color(0.2f, 0.75f, 1f, 0.16f),
                raycastTarget: false).Rect;
            var range = _images.Create(
                "Range",
                indicatorGo.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(120f, 120f),
                new Color(0.15f, 0.95f, 0.75f, 0.2f),
                raycastTarget: false).Rect;
            var dot = _images.Create(
                "Dot",
                indicatorGo.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(42f, 42f),
                new Color(0.2f, 0.75f, 1f, 0.62f),
                raycastTarget: false).Rect;

            var indicator = indicatorGo.AddComponent<SkillAimIndicatorView>();
            indicator.Initialize(ring, dot, range);
            return indicator;
        }

        private RectTransform CreateJoystickPart(
            Transform parent,
            string name,
            Vector2 size,
            Color color,
            bool raycastTarget)
        {
            return _images.Create(
                name,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                size,
                color,
                raycastTarget).Rect;
        }
    }
}
