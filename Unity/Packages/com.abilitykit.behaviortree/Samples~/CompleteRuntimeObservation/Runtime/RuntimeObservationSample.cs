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
        [Header("Authoring Source")]
        [SerializeField] private TextAsset? _authoringJson;

        [Header("Execution")]
        [SerializeField] private ObservationRuntimeSettings _settings = new();

        [Header("Agent Decision Inputs")]
        [SerializeField] private AgentDecisionInputs _inputs = new();

        [Header("Agent Decision Outputs (Read Only)")]
        [SerializeField] private AgentDecisionOutputs _outputs = new();

        [Header("Runtime State (Read Only)")]
        [SerializeField] private int _frame;
        [SerializeField] private string _treeState = "Stopped";

        private TreeRuntime? _runtime;
        private Fixed64 _time;
        private float _accumulator;

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

        [ContextMenu("Start / Recreate Runtime")]
        public void StartRuntime()
        {
            StopRuntime();
            if (_authoringJson == null)
            {
                Debug.LogError(
                    "Assign complete_runtime_observation.authoring.json to Authoring Json.",
                    this);
                return;
            }

            try
            {
                _runtime = ObservationRuntimeFactory.Create(
                    _authoringJson.text,
                    _settings,
                    gameObject.name);
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

        [ContextMenu("Step One Deterministic Tick")]
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

        [ContextMenu("Restart Tree")]
        public void RestartTree()
        {
            _runtime?.Restart();
            RefreshPresentation();
        }

        [ContextMenu("Stop Runtime")]
        public void StopRuntime()
        {
            _runtime?.Dispose();
            _runtime = null;
            _treeState = "Stopped";
            _outputs.Clear();
        }

        private void RefreshPresentation()
        {
            if (_runtime == null) return;
            _treeState = _runtime.TreeState.ToString();
            _outputs.ReadFrom(_runtime.Blackboard);
        }
    }
}
