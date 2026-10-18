using System;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudSkillFailurePresenter : IDisposable
    {
        private const float VisibleDurationSeconds = 1.1f;
        private const float FadeDurationSeconds = 0.35f;

        private readonly BattleHudSkillFailureAckFeed _feed = new BattleHudSkillFailureAckFeed();
        private IBattleHudPresentationPort _context;
        private FrameSnapshotDispatcher _snapshots;
        private IDisposable _subscription;
        private GameObject _root;
        private Text _text;
        private CanvasGroup _canvasGroup;
        private float _remainingSeconds;

        public void Bind(IBattleHudPresentationPort context, RectTransform hudRoot)
        {
            Dispose();
            _context = context;
            TryBindSnapshots();
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
            TryBindSnapshots();

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
                if (Application.isPlaying) UnityEngine.Object.Destroy(_root);
                else UnityEngine.Object.DestroyImmediate(_root);
            }

            _context = null;
            _subscription?.Dispose();
            _subscription = null;
            _snapshots = null;
            _feed.Reset();
            _root = null;
            _text = null;
            _canvasGroup = null;
            _remainingSeconds = 0f;
        }

        private void TryBindSnapshots()
        {
            var snapshots = _context != null && _context.TryGetFrameSnapshots(out var current)
                ? current
                : null;
            if (ReferenceEquals(_snapshots, snapshots)) return;
            _subscription?.Dispose();
            _subscription = null;
            _snapshots = snapshots;
            _feed.Reset();
            _remainingSeconds = 0f;
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            if (snapshots != null)
                _subscription = snapshots.Subscribe<MobaActionAckEntry[]>(
                    MobaOpCodes.Snapshot.ActionAck, (_, entries) => ApplyAcks(entries));
        }

        private void ApplyAcks(MobaActionAckEntry[] entries)
        {
            var actorId = _context?.LocalActorId ?? 0;
            if (_feed.TryReadLatest(entries, actorId, out var message)) Show(message);
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
