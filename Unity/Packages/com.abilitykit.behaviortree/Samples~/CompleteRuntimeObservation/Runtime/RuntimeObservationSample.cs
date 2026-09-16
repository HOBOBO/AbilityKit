using System;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.Deterministic;
using UnityEngine;

namespace AbilityKit.BehaviorTree.Samples.CompleteRuntimeObservation
{
    /// <summary>
    /// 示例场景的 Unity 生命周期适配器。领域输入、输出、配置和运行实例创建由独立对象负责。
    /// </summary>
    [AddComponentMenu("AbilityKit/Behavior Tree/Runtime Observation Sample")]
    public sealed class RuntimeObservationSample : MonoBehaviour
    {
        [Header("行为树配置")]
        [SerializeField] private TextAsset? _authoringJson;
        [SerializeField] private TextAsset? _runtimeJson;
        [SerializeField, HideInInspector] private UnityEngine.Object? _authoringAsset;

        [Header("场景表现")]
        [SerializeField] private Renderer? _agentRenderer;
        [SerializeField] private TextMesh? _stateLabel;

        [Header("运行设置")]
        [SerializeField] private ObservationRuntimeSettings _settings = new();

        [Header("感知输入")]
        [SerializeField] private AgentDecisionInputs _inputs = new();

        [Header("行为输出（只读）")]
        [SerializeField] private AgentDecisionOutputs _outputs = new();

        [Header("运行状态（只读）")]
        [SerializeField] private int _frame;
        [SerializeField] private string _treeState = "Stopped";

        private TreeRuntime? _runtime;
        private Fixed64 _time;
        private float _accumulator;
        private MaterialPropertyBlock? _visualProperties;

        public TreeRuntime? Runtime => _runtime;
        public bool IsRunning => _runtime?.IsEnabled == true;
        public AgentDecisionOutputs Outputs => _outputs;

        private void OnEnable()
        {
            if (_settings.StartOnEnable && Application.isPlaying) StartRuntime();
        }

        private void Update()
        {
            if (_runtime?.IsEnabled != true) return;

            _accumulator += Time.unscaledDeltaTime;
            var secondsPerTick = 1f / _settings.TicksPerSecond;
            while (_accumulator >= secondsPerTick)
            {
                _accumulator -= secondsPerTick;
                StepOnce();
            }
        }

        private void OnDisable() => StopRuntime();
        private void OnDestroy() => StopRuntime();

        [ContextMenu("启动 / 重建运行实例")]
        public void StartRuntime()
        {
            StopRuntime();
            if (_runtimeJson == null && _authoringJson == null)
            {
                _authoringJson = Resources.Load<TextAsset>("complete_runtime_observation");
            }
            if (_runtimeJson == null && _authoringJson == null)
            {
                Debug.LogError(
                    "请绑定行为树运行时 JSON 或编辑源 JSON。",
                    this);
                return;
            }

            try
            {
                _runtime = _runtimeJson != null
                    ? ObservationRuntimeFactory.CreateFromRuntimeJson(_runtimeJson.text, _settings, gameObject.name)
                    : ObservationRuntimeFactory.Create(_authoringJson!.text, _settings, gameObject.name);
                _frame = 0;
                _time = Fixed64.Zero;
                _accumulator = 0f;
                _inputs.WriteTo(_runtime.Blackboard);
                _runtime.Enable(_frame, _time);
                RefreshPresentation();
            }
            catch (Exception exception)
            {
                StopRuntime();
                Debug.LogException(exception, this);
            }
        }

        [ContextMenu("推进一个逻辑帧")]
        public void StepOnce()
        {
            if (_runtime?.IsEnabled != true) return;

            _inputs.WriteTo(_runtime.Blackboard);
            _frame++;
            _time += Fixed64.FromRatio(1, _settings.TicksPerSecond);
            _runtime.Update(_frame, _time);
            if (_settings.AutoRestart && _runtime.TreeState != NodeState.Running)
            {
                _runtime.Restart();
            }
            RefreshPresentation();
        }

        [ContextMenu("重新开始行为树")]
        public void RestartTree()
        {
            _runtime?.Restart();
            RefreshPresentation();
        }

        [ContextMenu("停止运行")]
        public void StopRuntime()
        {
            _runtime?.Dispose();
            _runtime = null;
            _treeState = "Stopped";
            _outputs.Clear();
            RefreshVisual();
        }

        private void RefreshPresentation()
        {
            if (_runtime == null) return;
            _treeState = _runtime.TreeState.ToString();
            _outputs.ReadFrom(_runtime.Blackboard);
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (_stateLabel != null)
                _stateLabel.text = _runtime == null ? "STOPPED" : _outputs.Mode.ToUpperInvariant();
            if (_agentRenderer == null) return;
            _visualProperties ??= new MaterialPropertyBlock();
            var color = _runtime == null ? new Color(0.4f, 0.44f, 0.47f)
                : _outputs.Mode switch
                {
                    "Retreat" => new Color(0.85f, 0.29f, 0.24f),
                    "Attack" => new Color(0.92f, 0.64f, 0.19f),
                    "Chase" => new Color(0.29f, 0.68f, 0.89f),
                    "Patrol" => new Color(0.31f, 0.72f, 0.48f),
                    _ => new Color(0.47f, 0.56f, 0.64f),
                };
            _visualProperties.SetColor("_Color", color);
            _visualProperties.SetColor("_BaseColor", color);
            _agentRenderer.SetPropertyBlock(_visualProperties);
        }
    }
}
