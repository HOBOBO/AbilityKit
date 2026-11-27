using UnityEngine;

namespace AbilityKit.Game.Platform.Unity.Presentation
{
    /// <summary>
    /// 标记由表现时间轴直接采样的独立预制体动画，不使用 Animator 或渲染帧计时。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnityTimelineSampledAnimation : MonoBehaviour
    {
        [SerializeField] private MeshRenderer _renderer;
        [SerializeField] private Vector2 _visibleSeconds;
        [SerializeField] private AnimationCurve _scaleX;
        [SerializeField] private AnimationCurve _red;
        [SerializeField] private AnimationCurve _green;
        [SerializeField] private AnimationCurve _blue;
        [SerializeField] private AnimationCurve _alpha;
        [SerializeField] private AnimationCurve _intensity;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private MaterialPropertyBlock _properties;

        /// <summary>
        /// 在当前预制体上应用相对于表现片段起点的作者时间。
        /// </summary>
        public void Sample(float seconds)
        {
            if (_renderer == null) return;
            _renderer.enabled = seconds >= _visibleSeconds.x && seconds < _visibleSeconds.y;
            if (!_renderer.enabled) return;
            var scale = _renderer.transform.localScale;
            scale.x = _scaleX.Evaluate(seconds);
            _renderer.transform.localScale = scale;
            _properties ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_properties);
            _properties.SetColor(ColorId, new Color(_red.Evaluate(seconds), _green.Evaluate(seconds),
                _blue.Evaluate(seconds), _alpha.Evaluate(seconds)));
            _properties.SetFloat(IntensityId, _intensity.Evaluate(seconds));
            _renderer.SetPropertyBlock(_properties);
        }
    }
}
