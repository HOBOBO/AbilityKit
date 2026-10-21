using AbilityKit.Demo.Moba.Share;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow.Battle.Hud
{
    /// <summary>
    /// Visual representation of a single active buff instance.
    /// Built imperatively by <see cref="BattleHudBuffBarFactory"/>.
    /// Notifies a callback when its owning actor is destroyed so the parent bar can clean up.
    /// </summary>
    internal sealed class BattleHudBuffIconView : MonoBehaviour
    {
        [SerializeField] private RectTransform _root;
        [SerializeField] private Image _background;
        [SerializeField] private Image _ring;
        [SerializeField] private Image _stackIcon;
        [SerializeField] private Text _stackText;
        [SerializeField] private Text _countdownText;
        [SerializeField] private Text _tagLabel;

        private int _templateId;
        private int _stackCount;
        private int _maxStackCount;
        private float _totalSeconds;
        private float _remainingSeconds;
        private float _lastTickRealtime;
        private Color _tintColor = Color.white;
        private bool _hasColor;
        private bool _isTag;

        public int TemplateId => _templateId;
        public string InstanceKey { get; private set; }
        public int BuffId { get; private set; }
        public bool IsActive => !string.IsNullOrEmpty(InstanceKey);

        public void Initialize(
            RectTransform root,
            Image background,
            Image ring,
            Image stackIcon,
            Text stackText,
            Text countdownText,
            Text tagLabel = null)
        {
            _root = root;
            _background = background;
            _ring = ring;
            _stackIcon = stackIcon;
            _stackText = stackText;
            _countdownText = countdownText;
            _tagLabel = tagLabel;
            ResetState();
        }

        private void Awake()
        {
            EnsureReferences();
            ResetState();
        }

        private void EnsureReferences()
        {
            if (_root == null) _root = transform as RectTransform;
            if (_background == null) _background = GetComponent<Image>();
            if (_ring == null && transform.childCount > 0) _ring = transform.GetChild(0).GetComponent<Image>();
            if (_stackText == null) _stackText = GetComponentInChildren<Text>(true);
            if (_countdownText == null && _stackText != null) _countdownText = _stackText;
        }

        public void ResetState()
        {
            BuffId = 0;
            _templateId = 0;
            InstanceKey = null;
            _stackCount = 0;
            _maxStackCount = 0;
            _totalSeconds = 0f;
            _remainingSeconds = 0f;
            _lastTickRealtime = Time.realtimeSinceStartup;
            _hasColor = false;
            _tintColor = Color.white;
            _isTag = false;
            if (_tagLabel != null) _tagLabel.text = string.Empty;
            ApplyVisualState();
            SetVisible(false);
        }

        public void Apply(in PresentationCueData entry, float totalSecondsHint)
        {
            InstanceKey = entry.InstanceKey;
            _templateId = entry.TemplateId;
            BuffId = ResolveBuffIdFromCueKind(entry.CueKind);
            _isTag = BattleHudStatusCueFilter.IsTagCue(in entry);
            _stackCount = entry.StackCount;
            _maxStackCount = entry.MaxStackCount;

            // totalSeconds = elapsed + remaining; totalSecondsHint is the authored DurationMsOverride (if any).
            var hintSeconds = totalSecondsHint > 0f ? totalSecondsHint : 0f;
            var computed = entry.ElapsedSeconds + entry.RemainingSeconds;
            _totalSeconds = Mathf.Max(hintSeconds, computed);
            if (_totalSeconds <= 0f) _totalSeconds = entry.RemainingSeconds;
            _remainingSeconds = entry.RemainingSeconds;

            // Tag colors and labels come from the presentation catalog keyed by
            // the published tag definition ID.
            if (_isTag && BattleHudStatusCueFilter.TryResolveTagPresentation(in entry, out var tagPresentation))
            {
                _tintColor = new Color(
                    tagPresentation.ColorR,
                    tagPresentation.ColorG,
                    tagPresentation.ColorB,
                    tagPresentation.ColorA);
                _hasColor = true;
                if (_tagLabel != null)
                    _tagLabel.text = string.IsNullOrWhiteSpace(tagPresentation.DisplayName)
                        ? tagPresentation.LocalizationKey
                        : tagPresentation.DisplayName;
            }
            // Apply tint from cue color (only when explicitly authored, otherwise keep white).
            else if (entry.ColorA > 0f)
            {
                _tintColor = new Color(entry.ColorR, entry.ColorG, entry.ColorB, entry.ColorA);
                _hasColor = true;
            }
            else
            {
                _tintColor = Color.white;
                _hasColor = false;
            }

            _lastTickRealtime = Time.realtimeSinceStartup;
            ApplyVisualState();
            SetVisible(true);
        }

        /// <summary>
        /// Decay remaining seconds using real-time elapsed since last update. Called every frame by the bar.
        /// </summary>
        public void TickDecay()
        {
            if (!IsActive) return;
            if (_totalSeconds <= 0f) return;
            var now = Time.realtimeSinceStartup;
            var dt = now - _lastTickRealtime;
            _lastTickRealtime = now;
            if (dt <= 0f) return;
            _remainingSeconds = Mathf.Max(0f, _remainingSeconds - dt);
            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            if (_background != null)
            {
                if (_hasColor) _background.color = _tintColor;
                else _background.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);
            }
            if (_ring != null)
            {
                if (_totalSeconds > 0f)
                {
                    var t = Mathf.Clamp01(_remainingSeconds / _totalSeconds);
                    _ring.fillAmount = t;
                _ring.color = _isTag && _hasColor
                    ? new Color(_tintColor.r, _tintColor.g, _tintColor.b, 0.85f)
                    : new Color(0.05f, 0.85f, 1f, 0.85f);
                }
                else
                {
                    _ring.fillAmount = 0f;
                }
            }
            if (_stackIcon != null)
            {
                _stackIcon.color = _hasColor ? _tintColor : Color.white;
            }
            if (_stackText != null)
            {
                var isGameplayCounter = InstanceKey != null && InstanceKey.StartsWith("gameplay-counter:", System.StringComparison.Ordinal);
                var showStack = !_isTag && (_stackCount > 1 || (isGameplayCounter && _maxStackCount > 0));
                _stackText.text = showStack
                    ? isGameplayCounter && _maxStackCount > 0
                        ? $"{_stackCount}/{_maxStackCount}"
                        : _stackCount.ToString()
                    : string.Empty;
                _stackText.gameObject.SetActive(showStack);
            }
            if (_countdownText != null && !ReferenceEquals(_countdownText, _stackText))
            {
                if (_remainingSeconds > 0f && _remainingSeconds < 99f)
                {
                    _countdownText.gameObject.SetActive(true);
                    _countdownText.text = Mathf.CeilToInt(_remainingSeconds).ToString();
                }
                else
                {
                    _countdownText.gameObject.SetActive(false);
                }
            }
            if (_tagLabel != null)
            {
                _tagLabel.gameObject.SetActive(_isTag && !string.IsNullOrEmpty(_tagLabel.text));
                _tagLabel.color = Color.white;
            }
        }

        private void SetVisible(bool visible)
        {
            if (_root != null && _root.gameObject != gameObject)
            {
                _root.gameObject.SetActive(visible);
            }
            else if (_background != null)
            {
                _background.gameObject.SetActive(visible);
            }
            else
            {
                gameObject.SetActive(visible);
            }
        }

        private static int ResolveBuffIdFromCueKind(string cueKind)
        {
            if (string.IsNullOrEmpty(cueKind)) return 0;
            // Convention from MobaBuffPresentationCueReporter: CueKind = "Buff".
            // The BuffMO id is encoded into the InstanceKey suffix: "buff:{target}:{buffId}:{ctx}".
            return 0; // buffId resolution is intentionally left to caller via key parsing if needed.
        }

        public static int ExtractBuffIdFromInstanceKey(string instanceKey)
        {
            if (string.IsNullOrEmpty(instanceKey)) return 0;
            // Format: "buff:{targetActorId}:{buffId}:{sourceContextId}"
            var parts = instanceKey.Split(':');
            if (parts.Length < 4) return 0;
            return int.TryParse(parts[2], out var v) ? v : 0;
        }

    }
}
