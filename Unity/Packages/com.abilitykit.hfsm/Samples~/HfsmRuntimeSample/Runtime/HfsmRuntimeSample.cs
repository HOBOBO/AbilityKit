using System;
using AbilityKit.Deterministic;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Runtime;
using UnityEngine;
#if UNITY_EDITOR
using AbilityKit.HFSM.Visualization;
#endif

namespace AbilityKit.HFSM.Samples.HfsmRuntimeSample
{
    /// <summary>
    /// 最小运行时示例：加载编辑器导出的 Next Definition JSON，组装 RuntimeBindings，
    /// 创建并驱动 <see cref="StateMachineRuntime{TOwner}"/>，并注册进编辑器调试用的 LiveRegistry。
    /// </summary>
    [AddComponentMenu("AbilityKit/HFSM/Runtime Sample")]
    public sealed class HfsmRuntimeSample : MonoBehaviour
    {
        [Header("Definition")]
        [SerializeField] private TextAsset _definitionJson;

        [Header("Simulation")]
        [SerializeField] private int _ticksPerSecond = 10;

        [Header("Runtime State (Read Only)")]
        [SerializeField] private int _frame;
        [SerializeField] private string _activePath = "Stopped";

        private StateMachineRuntime<HfsmRuntimeSample> _runtime;
        private Fixed64 _time;
        private float _accumulator;
        private bool _registered;

        public StateMachineRuntime<HfsmRuntimeSample> Runtime => _runtime;
        public bool IsRunning => _runtime != null && _runtime.IsInitialized;

        private void OnEnable()
        {
            if (Application.isPlaying) StartRuntime();
        }

        private void Update()
        {
            if (!IsRunning) return;
            _accumulator += Time.unscaledDeltaTime;
            var secondsPerTick = 1f / Mathf.Max(1, _ticksPerSecond);
            while (_accumulator >= secondsPerTick)
            {
                _accumulator -= secondsPerTick;
                StepOnce();
            }
        }

        private void OnDisable() => StopRuntime();
        private void OnDestroy() => StopRuntime();

        [ContextMenu("Start Runtime")]
        public void StartRuntime()
        {
            StopRuntime();
            if (_definitionJson == null)
            {
                _definitionJson = Resources.Load<TextAsset>("hfsm_sample");
            }
            if (_definitionJson == null)
            {
                Debug.LogError("Assign a Definition .hfsm JSON to the sample.", this);
                return;
            }

            try
            {
                var definition = DefinitionJson.Load(_definitionJson.text);
                _runtime = new StateMachineRuntime<HfsmRuntimeSample>(this, definition, BuildBindings());
                _frame = 0;
                _time = Fixed64.Zero;
                _accumulator = 0f;
                _runtime.Initialize(_frame, _time);
                RegisterForDebug();
                RefreshPresentation();
            }
            catch (Exception exception)
            {
                StopRuntime();
                Debug.LogException(exception, this);
            }
        }

        [ContextMenu("Step One Tick")]
        public void StepOnce()
        {
            if (!IsRunning) return;
            _frame++;
            _time += Fixed64.FromRatio(1, Mathf.Max(1, _ticksPerSecond));
            _runtime.Tick(_frame, _time);
            RefreshPresentation();
        }

        [ContextMenu("Trigger: go")]
        public void TriggerGo() => _runtime?.Trigger("go");

        [ContextMenu("Trigger: back")]
        public void TriggerBack() => _runtime?.Trigger("back");

        [ContextMenu("Stop Runtime")]
        public void StopRuntime()
        {
            UnregisterForDebug();
            if (_runtime != null)
            {
                if (_runtime.IsInitialized) _runtime.Shutdown();
                _runtime = null;
            }
            _activePath = "Stopped";
        }

        private static RuntimeBindings<HfsmRuntimeSample> BuildBindings()
        {
            var bindings = new RuntimeBindings<HfsmRuntimeSample>();
            bindings.RegisterState("log", () => new LogState<HfsmRuntimeSample>("log"));
            bindings.RegisterCondition("always", () => new AlwaysCondition<HfsmRuntimeSample>());
            bindings.RegisterAction("log", () => new LogAction<HfsmRuntimeSample>());
            return bindings;
        }

        private void RegisterForDebug()
        {
#if UNITY_EDITOR
            if (_runtime == null) return;
            LiveRegistry.Register(gameObject.name, _runtime, snapshot =>
            {
                snapshot.activeStatePaths.Clear();
                foreach (var path in _runtime.GetActivePath()) snapshot.activeStatePaths.Add(path);
            });
            _registered = true;
#endif
        }

        private void UnregisterForDebug()
        {
#if UNITY_EDITOR
            if (_registered && _runtime != null)
            {
                LiveRegistry.Unregister(_runtime);
                _registered = false;
            }
#endif
        }

        private void RefreshPresentation()
        {
            if (_runtime == null) return;
            _activePath = string.Join(" -> ", _runtime.GetActivePath());
        }
    }

    /// <summary>示例状态：进入/退出时打日志，证明 binding 工厂确实被调用。</summary>
    public sealed class LogState<TOwner> : RuntimeStateBase<TOwner>
    {
        private readonly string _key;
        public LogState(string key) { _key = key; }

        public override void OnEnter(TOwner owner, in TickContext context)
        {
            Debug.Log("[HFSM Sample] enter " + _key);
        }

        public override void OnExit(TOwner owner, in TickContext context)
        {
            Debug.Log("[HFSM Sample] exit " + _key);
        }
    }

    /// <summary>示例条件：恒真。</summary>
    public sealed class AlwaysCondition<TOwner> : ITransitionCondition<TOwner>
    {
        public bool Evaluate(TOwner owner, in TransitionContext context) => true;
    }

    /// <summary>示例动作：转移完成后打日志。</summary>
    public sealed class LogAction<TOwner> : ITransitionAction<TOwner>
    {
        public void BeforeTransition(TOwner owner, in TransitionContext context) { }

        public void AfterTransition(TOwner owner, in TransitionContext context)
        {
            Debug.Log("[HFSM Sample] transition completed");
        }
    }
}
