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
        private const int QueryLimit = 64;

        private BattleContext _context;
        private IBattleDiagnosticEventReadStore _store;
        private int _actorId;
        private long _observedRevision = -1;
        private long _lastSequence;
        private long _requestId;
        private GameObject _root;
        private Text _text;
        private CanvasGroup _canvasGroup;
        private float _remainingSeconds;

        public void Bind(BattleContext context, RectTransform hudRoot)
        {
            Dispose();
            _context = context;
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
            _actorId = 0;
            _observedRevision = -1;
            _lastSequence = 0;
            _requestId = 0;
            _root = null;
            _text = null;
            _canvasGroup = null;
            _remainingSeconds = 0f;
        }

        private void ReadLatestFailure()
        {
            var actorId = _context?.LocalActorId ?? 0;
            if (actorId <= 0 || !TryResolveStore(out var store)) return;

            if (!ReferenceEquals(_store, store) || _actorId != actorId)
            {
                _store = store;
                _actorId = actorId;
                _observedRevision = store.Revision;
                _lastSequence = QueryLatestSequence(store, actorId);
                return;
            }

            if (_observedRevision == store.Revision) return;
            _observedRevision = store.Revision;

            var result = store.Query(CreateQuery(store.Revision, actorId));
            BattleDiagnosticSkillFailurePayload latestFailure = default;
            var latestFailureSequence = _lastSequence;
            var highestSequence = _lastSequence;
            var foundFailure = false;

            for (var i = 0; i < result.Items.Count; i++)
            {
                var item = result.Items[i];
                if (item.Sequence > highestSequence) highestSequence = item.Sequence;
                if (item.Sequence <= latestFailureSequence ||
                    item.Kind != BattleDiagnosticEventKind.SkillFailure ||
                    !item.Payload.TryGetSkillFailure(out var failure))
                {
                    continue;
                }

                latestFailure = failure;
                latestFailureSequence = item.Sequence;
                foundFailure = true;
            }

            _lastSequence = highestSequence;
            if (foundFailure)
            {
                Show(latestFailure);
            }
        }

        private long QueryLatestSequence(IBattleDiagnosticEventReadStore store, int actorId)
        {
            var result = store.Query(CreateQuery(store.Revision, actorId, 1));
            return result.Items.Count > 0 ? result.Items[0].Sequence : 0;
        }

        private BattleDiagnosticEventQuery CreateQuery(long revision, int actorId, int limit = QueryLimit)
        {
            var filter = new BattleDiagnosticFilter(
                BattleDiagnosticFilter.Default.Frames,
                BattleDiagnosticEventChannel.Skill,
                actorId,
                BattleDiagnosticActorRelation.Source,
                failuresOnly: true);
            return new BattleDiagnosticEventQuery(
                ++_requestId,
                filter,
                new BattleDiagnosticPageRequest(revision, 0, limit),
                newestFirst: true);
        }

        private bool TryResolveStore(out IBattleDiagnosticEventReadStore store)
        {
            store = null;
            return _context != null &&
                   _context.TryGetRuntimeWorld(out var world) &&
                   world.Services != null &&
                   world.Services.TryResolve(out store) &&
                   store != null;
        }

        private void Show(in BattleDiagnosticSkillFailurePayload failure)
        {
            if (_text == null || _canvasGroup == null) return;
            _root.transform.SetAsLastSibling();
            _text.text = BattleHudSkillFailureText.Format(failure.Code, failure.Message);
            _remainingSeconds = VisibleDurationSeconds + FadeDurationSeconds;
            _canvasGroup.alpha = 1f;
        }
    }

    internal static class BattleHudSkillFailureText
    {
        public static string Format(string code, string message)
        {
            var detail = ((code ?? string.Empty) + " " + (message ?? string.Empty)).ToLowerInvariant();
            if (ContainsAny(detail, "not_enough_mana", "not enough mana", "insufficient mana"))
                return "蓝量不足";
            if (ContainsAny(detail, "cooldown", "cool down"))
                return "技能冷却中";
            if (ContainsAny(detail, "alreadyrunning", "already running"))
                return "技能正在释放";
            if (ContainsAny(detail, "outofrange", "out of range", "outside cast range"))
                return "超出施法范围";
            if (ContainsAny(detail, "targetmissing", "target missing", "no valid target"))
                return "没有有效目标";
            if (ContainsAny(detail, "invalidslot", "missingskill", "skill not found"))
                return "技能不可用";
            if (ContainsAny(detail, "resource", "not_enough"))
                return "资源不足";
            return "技能释放失败";
        }

        private static bool ContainsAny(string value, params string[] candidates)
        {
            for (var i = 0; i < candidates.Length; i++)
            {
                if (value.IndexOf(candidates[i], StringComparison.Ordinal) >= 0) return true;
            }

            return false;
        }
    }
}
