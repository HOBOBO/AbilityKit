using AbilityKit.ActionSchema;
using UnityEngine;

namespace AbilityKit.Game.Test
{
    public sealed class TimelineLogicRunner : MonoBehaviour, ITimelineEventSink
    {
        [SerializeField] private TextAsset logicJson;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool loop;
        [SerializeField] private float timeScale = 1f;

        private TimelinePlayer _player;
        private SkillAssetDto _asset;

        private void Start()
        {
            if (!playOnStart) return;
            LoadAndPlay();
        }

        [ContextMenu("Load And Play")]
        public void LoadAndPlay()
        {
            if (logicJson == null)
            {
                UnityEngine.Debug.LogError("[TimelineLogicRunner] logicJson is null");
                return;
            }

            _asset = ActionTimelineJson.LoadFromJson(logicJson.text);
            if (_asset == null)
            {
                UnityEngine.Debug.LogError("[TimelineLogicRunner] Failed to parse logic json");
                return;
            }

            _player = new TimelinePlayer(_asset, this);
            _player.Reset(0f);

            UnityEngine.Debug.Log($"[TimelineLogicRunner] Loaded. length={_asset.length}");
        }

        private void Update()
        {
            if (_player == null || _asset == null) return;

            var dt = Time.deltaTime * Mathf.Max(0f, timeScale);
            _player.Update(dt);

            if (loop && _player.Time >= _asset.length)
            {
                _player.Reset(0f);
            }
        }

        public void OnTriggerLog(float time, string message)
        {
            UnityEngine.Debug.Log($"[TimelineLogicRunner] TriggerLog at t={time:F3}: {message}");
        }
    }
}
