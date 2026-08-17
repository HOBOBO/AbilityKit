using System;
using AbilityKit.Demo.Moba.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudSkillFailurePresenter : IDisposable
    {
        private const float VisibleDurationSeconds = 1.1f;
        private const float FadeDurationSeconds = 0.35f;

        private readonly BattleHudSkillFailureFeed _feed = new BattleHudSkillFailureFeed();
        private BattleContext _context;
        private IBattleDiagnosticEventReadStore _store;
        private GameObject _root;
        private Text _text;
        private CanvasGroup _canvasGroup;
        private float _remainingSeconds;

        public void Bind(BattleContext context, RectTransform hudRoot)
        {
            Dispose();
            _context = context;
            _store = ResolveStore(context);
            if (hudRoot == null) return;

            _root = new GameObject("SkillFailurePrompt", typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)_root.transform;
            rect.SetParent(hudRoot, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 96f);
            rect.sizeDelta = new Vector2(420f, 54f);

            _canvasGroup = _root.GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            _text = _root.AddComponent<Text>();
            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.fontSize = 26;
            _text.fontStyle = FontStyle.Bold;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.color = new Color(1f, 0.82f, 0.25f, 1f);
            _text.raycastTarget = false;
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            _text.verticalOverflow = VerticalWrapMode.Truncate;

            var outline = _root.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        public void Tick(float deltaTime)
        {
            ReadLatestFailure();

            if (_canvasGroup == null || _remainingSeconds <= 0f) return;
            _remainingSeconds = Mathf.Max(0f, _remainingSeconds - Mathf.Max(0f, deltaTime));
            _canvasGroup.alpha = _remainingSeconds >= FadeDurationSeconds
                ? 1f
                : _remainingSeconds / FadeDurationSeconds;
        }

        public void Dispose()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }

            _context = null;
            _store = null;
            _feed.Reset();
            _root = null;
            _text = null;
            _canvasGroup = null;
            _remainingSeconds = 0f;
        }

        private void ReadLatestFailure()
        {
            var actorId = _context?.LocalActorId ?? 0;
            if (actorId <= 0 || _store == null)
            {
                _feed.Reset();
                return;
            }

            _feed.Bind(_store, actorId);
            if (_feed.TryReadLatest(out var message)) Show(message);
        }

        private static IBattleDiagnosticEventReadStore ResolveStore(BattleContext context)
        {
            if (context == null ||
                !context.TryGetRuntimeWorld(out var world) ||
                world.Services == null ||
                !world.Services.TryResolve<IBattleDiagnosticEventReadStore>(out var store))
            {
                return null;
            }

            return store;
        }

        private void Show(string message)
        {
            if (_text == null || _canvasGroup == null) return;
            _root.transform.SetAsLastSibling();
            _text.text = message;
            _remainingSeconds = VisibleDurationSeconds + FadeDurationSeconds;
            _canvasGroup.alpha = 1f;
        }
    }
}
