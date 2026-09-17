using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Graph;
using AbilityKit.HFSM.Runtime;
using UnityEngine;
#if UNITY_EDITOR
using AbilityKit.HFSM.Visualization;
#endif

namespace AbilityKit.HFSM.Samples.CompleteHfsmShowcase
{
    [AddComponentMenu("AbilityKit/HFSM/Showcase Agent")]
    public sealed class HfsmShowcaseAgent : MonoBehaviour, ICompositeActionSink
    {
        [SerializeField] private TextAsset _runtimeJson;
        [SerializeField] private GraphAsset _sourceGraph;
        [SerializeField] private HfsmShowcaseActionAsset _sourceActions;
        [SerializeField] private TextAsset _actionJson;
        [SerializeField] private int _ticksPerSecond = 10;
        [SerializeField] private bool _moving;
        [SerializeField] private bool _blocked;
        [SerializeField] private int _health = 100;
        [SerializeField] private bool _hasTarget;
        [SerializeField] private float _targetDistance = 5f;
        [SerializeField] private Renderer _agentRenderer;
        [SerializeField] private TextMesh _stateLabel;
        [SerializeField] private int _frame;
        [SerializeField] private string _activePath = "Stopped";
        [SerializeField] private string _currentAction = "-";
        [SerializeField] private int _currentActionLocalFrame = -1;
        [SerializeField] private string _lastActionLog = "-";
        [SerializeField] private int _lastActionLogFrame = -1;
        [SerializeField] private Animator _animator;

        private StateMachineRuntime<HfsmShowcaseAgent> _runtime;
        private RuntimeSnapshot _checkpoint;
        private Fixed64 _time;
        private float _accumulator;
        private readonly List<CompositeStateAction<HfsmShowcaseAgent>> _actionStates =
            new List<CompositeStateAction<HfsmShowcaseAgent>>();

        public int Frame => _frame;
        public string ActivePath => _activePath;
        public bool IsRunning => _runtime != null && _runtime.IsInitialized && !_runtime.IsFaulted;
        public bool Moving { get => _moving; set => _moving = value; }
        public bool Blocked { get => _blocked; set => _blocked = value; }
        public int Health { get => _health; set => _health = value; }
        public bool HasTarget { get => _hasTarget; set => _hasTarget = value; }
        public float TargetDistance { get => _targetDistance; set => _targetDistance = value; }
        public StateMachineRuntime<HfsmShowcaseAgent> Runtime => _runtime;
        public GraphAsset SourceGraph => _sourceGraph;
        public HfsmShowcaseActionAsset SourceActions => _sourceActions;
        public string CurrentAction => _currentAction;
        public int CurrentActionLocalFrame => _currentActionLocalFrame;
        public string LastActionLog => _lastActionLog;
        public int LastActionLogFrame => _lastActionLogFrame;

        private void OnEnable()
        {
            if (Application.isPlaying) Restart();
        }

        private void Update()
        {
            if (!IsRunning) return;
            _accumulator += Time.unscaledDeltaTime;
            var step = 1f / Mathf.Max(1, _ticksPerSecond);
            var budget = 8;
            while (_accumulator >= step && budget-- > 0)
            {
                _accumulator -= step;
                StepOnce();
            }
            if (budget < 0) _accumulator = 0f;
        }

        private void OnDisable() => Stop();
        private void OnDestroy() => Stop();

        public void Restart()
        {
            Stop();
            if (_runtimeJson == null)
                _runtimeJson = Resources.Load<TextAsset>(_sourceGraph != null &&
                    _sourceGraph.GraphName == "HierarchicalCombat"
                    ? "hfsm_showcase_combat" : "hfsm_showcase_patrol");
            if (_runtimeJson == null) return;
            try
            {
                var definition = DefinitionJson.Load(_runtimeJson.text);
                if (_actionJson == null) _actionJson = Resources.Load<TextAsset>("hfsm_showcase_actions");
                var actions = _actionJson == null ? null : CompositeActionCatalog.LoadJson(_actionJson.text);
                _runtime = new StateMachineRuntime<HfsmShowcaseAgent>(this, definition,
                    CreateBindings(actions, Mathf.Max(1, _ticksPerSecond), _actionStates));
                _time = Fixed64.Zero;
                _frame = 0;
                _accumulator = 0f;
                _runtime.Initialize(0, _time);
#if UNITY_EDITOR
                LiveRegistry.Register(gameObject.name, _runtime);
#endif
                RefreshView();
            }
            catch (Exception ex)
            {
                Stop();
                Debug.LogException(ex, this);
            }
        }

        public void StepOnce()
        {
            if (!IsRunning) return;
            try
            {
                _frame++;
                _time += Fixed64.FromRatio(1, Mathf.Max(1, _ticksPerSecond));
                _runtime.Tick(_frame, _time);
                RefreshView();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
                Stop();
            }
        }

        public void SaveCheckpoint()
        {
            if (IsRunning) _checkpoint = _runtime.CaptureSnapshot();
        }

        public void Respawn()
        {
            if (!IsRunning) return;
            _health = 100;
            if (_runtime.Trigger("respawn")) RefreshView();
        }

        public void RestoreCheckpoint()
        {
            if (!IsRunning || _checkpoint == null) return;
            _runtime.RestoreSnapshot(_checkpoint);
            _frame = _runtime.CurrentFrame;
            _time = _runtime.CurrentTime;
            _accumulator = 0f;
            ClearCompositeStatus();
            foreach (var action in _actionStates)
                if (action.IsActive) action.Seek(this, _frame);
            RefreshView();
        }

        public void Stop()
        {
            if (_runtime != null)
            {
#if UNITY_EDITOR
                LiveRegistry.Unregister(_runtime);
#endif
                if (_runtime.IsInitialized && !_runtime.IsFaulted) _runtime.Shutdown();
                _runtime = null;
            }
            _checkpoint = null;
            _activePath = "Stopped";
            _actionStates.Clear();
            ClearCompositeStatus();
        }

        public void ClearCompositeStatus()
        {
            _currentAction = "-";
            _currentActionLocalFrame = -1;
            _lastActionLog = "-";
            _lastActionLogFrame = -1;
        }

        public void OnCompositeAction(in CompositeActionIntent intent)
        {
            if (intent.Type == "log")
            {
                _lastActionLog = intent.ActionId + " / " + intent.Message;
                _lastActionLogFrame = intent.Frame;
                Debug.Log("[HFSM Showcase] " + intent.Message + " action=" + intent.ActionId +
                    " frame=" + intent.Frame, this);
                return;
            }
            _currentAction = intent.ActionId + " / " + intent.PlayState;
            _currentActionLocalFrame = intent.LocalFrame;
            if (_animator == null || string.IsNullOrEmpty(intent.PlayState)) return;
            var hash = Animator.StringToHash(intent.PlayState);
            if (!_animator.HasState(0, hash)) return;
            var sampled = intent.FrameCount <= 0 ? intent.LocalFrame : intent.Loop
                ? intent.LocalFrame % intent.FrameCount : Mathf.Min(intent.LocalFrame, intent.FrameCount - 1);
            _animator.Play(hash, 0, 0f);
            _animator.Update(0f);
            var length = _animator.GetCurrentAnimatorStateInfo(0).length;
            var normalized = length > Mathf.Epsilon ? sampled / (length * Mathf.Max(1, _ticksPerSecond)) : 0f;
            _animator.Play(hash, 0, intent.Loop ? normalized : Mathf.Clamp01(normalized));
            _animator.Update(0f);
        }

        private void RefreshView()
        {
            if (_runtime == null) return;
            var path = _runtime.GetActivePath();
            var names = new string[path.Count];
            for (var i = 0; i < path.Count; i++)
            {
                var separator = path[i].LastIndexOf('/');
                var id = separator >= 0 ? path[i].Substring(separator + 1) : path[i];
                names[i] = _sourceGraph?.GetNodeById(id)?.DisplayName ?? id;
            }
            _activePath = string.Join(" / ", names);
            if (_stateLabel != null) _stateLabel.text = names.Length > 0 ? names[names.Length - 1] : "Stopped";
            if (_agentRenderer == null) return;
            var state = _activePath.ToLowerInvariant();
            _agentRenderer.material.color = state.Contains("dead") ? new Color(0.43f, 0.46f, 0.49f) :
                state.Contains("blocked") ? new Color(0.83f, 0.29f, 0.25f) :
                state.Contains("strike") ? new Color(0.9f, 0.52f, 0.16f) :
                state.Contains("chase") || state.Contains("walking") ? new Color(0.23f, 0.65f, 0.54f) :
                new Color(0.36f, 0.58f, 0.85f);
        }

        public static RuntimeBindings<HfsmShowcaseAgent> CreateBindings(
            CompositeActionCatalog actions = null, int tickRate = 10,
            List<CompositeStateAction<HfsmShowcaseAgent>> created = null)
        {
            if (actions == null)
            {
                var fallback = Resources.Load<TextAsset>("hfsm_showcase_actions");
                if (fallback == null) throw new InvalidOperationException("HFSM showcase action JSON is missing.");
                actions = CompositeActionCatalog.LoadJson(fallback.text);
            }
            var bindings = new RuntimeBindings<HfsmShowcaseAgent>();
            bindings.RegisterState("showcase.state", () => new ShowcaseState());
            foreach (var key in new[] { "showcase.patrol.walk", "showcase.patrol.block",
                         "showcase.combat.chase", "showcase.combat.strike" })
            {
                var bindingKey = key;
                bindings.RegisterState(bindingKey, () =>
                {
                    var state = new CompositeStateAction<HfsmShowcaseAgent>(actions.Get(bindingKey), tickRate);
                    created?.Add(state);
                    return state;
                });
            }
            bindings.RegisterAction("showcase.transition", () => new ShowcaseTransitionAction());
            bindings.RegisterCondition("showcase.blocked", () => new ShowcaseCondition(a => a.Blocked));
            bindings.RegisterCondition("showcase.walk", () => new ShowcaseCondition(a => !a.Blocked && a.Moving));
            bindings.RegisterCondition("showcase.idle", () => new ShowcaseCondition(a => !a.Blocked && !a.Moving));
            bindings.RegisterCondition("showcase.dead", () => new ShowcaseCondition(a => a.Health <= 0));
            bindings.RegisterCondition("showcase.alive", () => new ShowcaseCondition(a => a.Health > 0));
            bindings.RegisterCondition("showcase.strike", () => new ShowcaseCondition(a => a.HasTarget && a.TargetDistance <= 2f));
            bindings.RegisterCondition("showcase.chase", () => new ShowcaseCondition(a => a.HasTarget && a.TargetDistance > 2f));
            bindings.RegisterCondition("showcase.no-target", () => new ShowcaseCondition(a => !a.HasTarget));
            return bindings;
        }
    }

    [Binding(BindingKind.State, "showcase.state", "State events", "Showcase")]
    public sealed class ShowcaseState : RuntimeStateBase<HfsmShowcaseAgent>
    {
        public override void OnEnter(HfsmShowcaseAgent agent, in TickContext context)
        {
            agent.ClearCompositeStatus();
            Debug.Log("[HFSM Showcase] enter " + agent.name + " frame=" + context.Frame);
        }
    }

    [Binding(BindingKind.State, "showcase.patrol.walk", "Walking sequence", "Showcase")]
    [Binding(BindingKind.State, "showcase.patrol.block", "Blocked sequence", "Showcase")]
    [Binding(BindingKind.State, "showcase.combat.chase", "Chase parallel", "Showcase")]
    [Binding(BindingKind.State, "showcase.combat.strike", "Strike sequence", "Showcase")]
    public sealed class ShowcaseCompositeBindingMetadata { }

    [Binding(BindingKind.Action, "showcase.transition", "Transition events", "Showcase")]
    public sealed class ShowcaseTransitionAction : ITransitionAction<HfsmShowcaseAgent>
    {
        public void BeforeTransition(HfsmShowcaseAgent agent, in TransitionContext context) { }
        public void AfterTransition(HfsmShowcaseAgent agent, in TransitionContext context) =>
            Debug.Log("[HFSM Showcase] " + context.TransitionId + " frame=" + context.Tick.Frame);
    }

    [Binding(BindingKind.Condition, "showcase.blocked", "Blocked", "Showcase")]
    [Binding(BindingKind.Condition, "showcase.walk", "Walking", "Showcase")]
    [Binding(BindingKind.Condition, "showcase.idle", "Idle", "Showcase")]
    [Binding(BindingKind.Condition, "showcase.dead", "Dead", "Showcase")]
    [Binding(BindingKind.Condition, "showcase.alive", "Alive", "Showcase")]
    [Binding(BindingKind.Condition, "showcase.strike", "In range", "Showcase")]
    [Binding(BindingKind.Condition, "showcase.chase", "Target far", "Showcase")]
    [Binding(BindingKind.Condition, "showcase.no-target", "No target", "Showcase")]
    public sealed class ShowcaseCondition : ITransitionCondition<HfsmShowcaseAgent>
    {
        private readonly Func<HfsmShowcaseAgent, bool> _predicate;
        public ShowcaseCondition(Func<HfsmShowcaseAgent, bool> predicate) => _predicate = predicate;
        public bool Evaluate(HfsmShowcaseAgent agent, in TransitionContext context) => _predicate(agent);
    }
}
