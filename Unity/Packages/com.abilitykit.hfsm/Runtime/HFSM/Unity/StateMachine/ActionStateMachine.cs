#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS || UNITY_SERVER || UNITY_SERVER
#define HFSM_UNITY
#endif

#if HFSM_UNITY
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityHFSM.Actions;
using UnityHFSM.Actions.Runtime;
using UnityHFSM.Graph;
using UnityHFSM.Graph.Compilation;
using UnityHFSM.Graph.Conditions;

namespace UnityHFSM
{
    /// <summary>
    /// 支持行为树执行的状态机。
    /// 可以从 HfsmGraphAsset 初始化，并自动执行状态中的行为。
    /// </summary>
    public class ActionStateMachine<TStateId, TEvent> : StateMachine<TStateId, TEvent>, IActionable<TEvent>, Visualization.IVisualizationParameterSource
    {
        private ActionStorage<TEvent> actionStorage;
        private readonly Dictionary<string, BehaviorExecutor> behaviorExecutors = new Dictionary<string, BehaviorExecutor>();
        private MonoBehaviour monoBehaviour;
        private object userData;

        public StateMachineGraphProgram GraphProgram { get; private set; }
        public HfsmParameterStore Parameters { get; } = new HfsmParameterStore();

        public IEnumerable<Visualization.ParameterInfo> GetVisualizationParameters()
        {
            if (GraphProgram == null || GraphProgram.Parameters == null)
                yield break;

            foreach (var parameter in GraphProgram.Parameters)
            {
                var info = new Visualization.ParameterInfo
                {
                    name = parameter.Name,
                    isTrigger = parameter.ParameterType == HfsmParameterType.Trigger,
                    type = parameter.ParameterType == HfsmParameterType.Bool
                        ? Visualization.ParameterType.Bool
                        : parameter.ParameterType == HfsmParameterType.Int
                            ? Visualization.ParameterType.Int
                            : parameter.ParameterType == HfsmParameterType.Float
                                ? Visualization.ParameterType.Float
                                : Visualization.ParameterType.Trigger
                };

                switch (parameter.ParameterType)
                {
                    case HfsmParameterType.Bool:
                        info.boolValue = Parameters.GetBool(parameter.Name);
                        break;
                    case HfsmParameterType.Int:
                        info.intValue = Parameters.GetInt(parameter.Name);
                        break;
                    case HfsmParameterType.Float:
                        info.floatValue = Parameters.GetFloat(parameter.Name);
                        break;
                }

                yield return info;
            }
        }

        /// <summary>
        /// 行为完成时触发的事件（行为ID，状态ID，完成状态）
        /// </summary>
        public event Action<string, string, BehaviorStatus> OnBehaviorCompleted;

        /// <summary>
        /// 行为失败时触发的事件
        /// </summary>
        public event Action<string, string> OnBehaviorFailed;

        public ActionStateMachine(bool needsExitTime = false, bool isGhostState = false, bool rememberLastState = false)
            : base(needsExitTime: needsExitTime, isGhostState: isGhostState, rememberLastState: rememberLastState)
        {
        }

        /// <summary>
        /// 设置 MonoBehaviour 用于协程支持
        /// </summary>
        public void SetMonoBehaviour(MonoBehaviour mono)
        {
            monoBehaviour = mono;
            foreach (var executor in behaviorExecutors.Values)
            {
                executor.SetMonoBehaviour(mono);
            }
        }

        /// <summary>
        /// 设置用户数据
        /// </summary>
        public void SetUserData(object data)
        {
            userData = data;
            foreach (var executor in behaviorExecutors.Values)
            {
                executor.SetUserData(data);
            }
        }

        /// <summary>
        /// 从 HfsmGraphAsset 初始化状态机
        /// </summary>
        public void InitializeFromGraph(HfsmGraphAsset graph, MonoBehaviour mono)
        {
            monoBehaviour = mono;
            InitializeFromGraph(graph);
        }

        public void InitializeFromGraph(
            HfsmGraphAsset graph,
            MonoBehaviour mono,
            StateMachineGraphBinding<TStateId, TEvent> binding)
        {
            monoBehaviour = mono;
            InitializeFromGraph(graph, binding);
        }

        /// <summary>
        /// 从 HfsmGraphAsset 初始化状态机（无 MonoBehaviour）
        /// </summary>
        public void InitializeFromGraph(HfsmGraphAsset graph)
        {
            InitializeFromGraph(graph, StateMachineGraphBinding<TStateId, TEvent>.CreateNameBinding(Parameters));
        }

        public void InitializeFromGraph(
            HfsmGraphAsset graph,
            StateMachineGraphBinding<TStateId, TEvent> binding)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));

            InitializeFromProgram(new StateMachineGraphCompiler().Compile(graph), binding);
        }

        public void InitializeFromProgram(
            StateMachineGraphProgram program,
            StateMachineGraphBinding<TStateId, TEvent> binding)
        {
            GraphProgram = program ?? throw new ArgumentNullException(nameof(program));
            if (binding == null)
                throw new ArgumentNullException(nameof(binding));

            Parameters.LoadDefaults(program.Parameters);
            var mappedStateIds = new Dictionary<string, TStateId>(StringComparer.Ordinal);
            BuildMachine(program, program.RootMachine, this, binding, mappedStateIds);
        }

        private void BuildMachine(
            StateMachineGraphProgram program,
            MachineProgram machine,
            StateMachine<TStateId, TStateId, TEvent> runtimeMachine,
            StateMachineGraphBinding<TStateId, TEvent> binding,
            IDictionary<string, TStateId> mappedStateIds)
        {
            var localIds = new HashSet<TStateId>(EqualityComparer<TStateId>.Default);
            foreach (var childNodeId in machine.ChildNodeIds)
            {
                var child = program.GetNode(childNodeId);
                var runtimeId = binding.StateIdSelector(child);
                if (!localIds.Add(runtimeId))
                {
                    throw new InvalidOperationException(
                        $"The graph binding maps more than one child of machine '{machine.RuntimeName}' to state ID '{runtimeId}'.");
                }

                mappedStateIds.Add(childNodeId, runtimeId);
                if (child is MachineProgram childMachine)
                {
                    var subMachine = new HybridStateMachine<TStateId, TEvent>(rememberLastState: childMachine.RememberLastState);
                    runtimeMachine.AddState(runtimeId, subMachine);
                    BuildMachine(program, childMachine, subMachine, binding, mappedStateIds);
                }
                else if (child is StateProgram state)
                {
                    var actionState = new ActionBehaviorState<TStateId, TEvent>(
                        state.Template,
                        state.NeedsExitTime,
                        state.IsGhostState,
                        monoBehaviour,
                        userData,
                        runtimeMachine
                    );

                    runtimeMachine.AddState(runtimeId, actionState);

                    Parameters.BindActionCompletion(state.SourceNodeId, () => actionState.BehaviorCompleted);
                    Parameters.BindNodeElapsedTime(state.SourceNodeId, () => actionState.ElapsedTime);

                    actionState.OnBehaviorCompleted += (behaviorId, status) =>
                    {
                        OnBehaviorCompleted?.Invoke(behaviorId, state.RuntimeName, status);
                    };

                    actionState.OnBehaviorFailed += (behaviorId) =>
                    {
                        OnBehaviorFailed?.Invoke(behaviorId, state.RuntimeName);
                    };
                }
            }

            if (!string.IsNullOrEmpty(machine.DefaultChildNodeId))
                runtimeMachine.SetStartState(mappedStateIds[machine.DefaultChildNodeId]);

            Parameters.BindActiveState(machine.SourceNodeId, stateNodeId =>
            {
                if (!mappedStateIds.TryGetValue(stateNodeId, out var mappedStateId))
                    return false;
                try
                {
                    return EqualityComparer<TStateId>.Default.Equals(runtimeMachine.ActiveStateName, mappedStateId);
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            });

            foreach (var transition in machine.Transitions)
                AddCompiledTransition(runtimeMachine, transition, binding, mappedStateIds);
        }

        private static void AddCompiledTransition(
            StateMachine<TStateId, TStateId, TEvent> runtimeMachine,
            TransitionProgram program,
            StateMachineGraphBinding<TStateId, TEvent> binding,
            IDictionary<string, TStateId> mappedStateIds)
        {
            var from = program.IsFromAnyState || program.IsExitTransition && string.IsNullOrEmpty(program.SourceNodeId)
                ? default
                : mappedStateIds[program.SourceNodeId];
            var to = program.IsExitTransition ? default : mappedStateIds[program.TargetNodeId];
            var condition = CreateCondition(program, binding.EvaluationContext);
            var action = ResolveTransitionAction(program, binding.TransitionActionResolver);
            var transition = new Transition<TStateId>(
                from,
                to,
                condition == null ? null : _ => condition(),
                action == null ? null : _ => action(),
                forceInstantly: program.ForceInstantly);

            var hasTrigger = !string.IsNullOrEmpty(program.TriggerId);
            var trigger = hasTrigger ? binding.EventIdSelector(program.TriggerId) : default;
            if (program.IsExitTransition)
            {
                if (program.IsFromAnyState)
                {
                    if (hasTrigger)
                        runtimeMachine.AddExitTriggerTransitionFromAny(trigger, transition);
                    else
                        runtimeMachine.AddExitTransitionFromAny(transition);
                }
                else if (hasTrigger)
                    runtimeMachine.AddExitTriggerTransition(trigger, transition);
                else
                    runtimeMachine.AddExitTransition(transition);
            }
            else if (program.IsFromAnyState)
            {
                if (hasTrigger)
                    runtimeMachine.AddTriggerTransitionFromAny(trigger, transition);
                else
                    runtimeMachine.AddTransitionFromAny(transition);
            }
            else if (hasTrigger)
                runtimeMachine.AddTriggerTransition(trigger, transition);
            else
                runtimeMachine.AddTransition(transition);
        }

        private static Func<bool> CreateCondition(TransitionProgram program, IHfsmEvaluationContext context)
        {
            if (program.Conditions.Count == 0)
                return null;
            if (context == null)
            {
                throw new InvalidOperationException(
                    $"Transition '{program.SourceEdgeId}' has conditions, but the graph binding has no evaluation context.");
            }

            if (program.UseAndLogic)
            {
                return () =>
                {
                    foreach (var condition in program.Conditions)
                    {
                        if (!condition.Evaluate(context))
                            return false;
                    }
                    return true;
                };
            }

            return () =>
            {
                foreach (var condition in program.Conditions)
                {
                    if (condition.Evaluate(context))
                        return true;
                }
                return false;
            };
        }

        private static Action ResolveTransitionAction(
            TransitionProgram program,
            Func<string, Action> resolver)
        {
            if (string.IsNullOrEmpty(program.ActionKey))
                return null;
            if (resolver == null)
            {
                throw new InvalidOperationException(
                    $"Transition '{program.SourceEdgeId}' declares action key '{program.ActionKey}', " +
                    "but the graph binding has no transition action resolver.");
            }

            var action = resolver(program.ActionKey);
            if (action == null)
                throw new InvalidOperationException($"No transition action is registered for key '{program.ActionKey}'.");
            return action;
        }

        /// <summary>
        /// 添加行为执行器到指定状态
        /// </summary>
        public void AddBehaviorExecutor(string stateName, BehaviorExecutor executor)
        {
            executor.SetMonoBehaviour(monoBehaviour);
            executor.SetUserData(userData);
            executor.SetFsm(this);
            behaviorExecutors[stateName] = executor;
        }

        /// <summary>
        /// 获取指定状态的行为执行器
        /// </summary>
        public BehaviorExecutor GetBehaviorExecutor(string stateName)
        {
            return behaviorExecutors.TryGetValue(stateName, out var executor) ? executor : null;
        }

        public override void OnAction(TEvent trigger)
        {
            actionStorage?.RunAction(trigger);
            base.OnAction(trigger);
        }

        public override void OnAction<TData>(TEvent trigger, TData data)
        {
            actionStorage?.RunAction<TData>(trigger, data);
            base.OnAction<TData>(trigger, data);
        }

        public ActionStateMachine<TStateId, TEvent> AddAction(TEvent trigger, Action action)
        {
            actionStorage = actionStorage ?? new ActionStorage<TEvent>();
            actionStorage.AddAction(trigger, action);
            return this;
        }

        public ActionStateMachine<TStateId, TEvent> AddAction<TData>(TEvent trigger, Action<TData> action)
        {
            actionStorage = actionStorage ?? new ActionStorage<TEvent>();
            actionStorage.AddAction<TData>(trigger, action);
            return this;
        }
    }

    /// <summary>
    /// 支持行为树执行的状态机（简化版，使用 string 作为状态ID）
    /// </summary>
    public class ActionStateMachine : ActionStateMachine<string, string>
    {
        public ActionStateMachine(bool needsExitTime = false, bool isGhostState = false, bool rememberLastState = false)
            : base(needsExitTime, isGhostState, rememberLastState)
        {
        }
    }

    /// <summary>
    /// 支持行为树执行的状态
    /// </summary>
    public class ActionBehaviorState<TStateId, TEvent> : State<TStateId>, IActionable<TEvent>, IActionRuntimeStateProvider
    {
        private readonly HfsmStateNode node;
        private readonly MonoBehaviour mono;
        private readonly object userData;
        private readonly object parentFsm;
        private BehaviorExecutor executor;
        private ActionStorage<TEvent> actionStorage;
        private readonly Dictionary<string, IActionRuntimeStateSource> runtimeStates =
            new Dictionary<string, IActionRuntimeStateSource>(StringComparer.Ordinal);
        private string behaviorRootId;
        private bool behaviorCompleted;
        private float elapsedTime;

        public bool BehaviorCompleted => behaviorCompleted;
        public float ElapsedTime => elapsedTime;

        public event Action<string, BehaviorStatus> OnBehaviorCompleted;
        public event Action<string> OnBehaviorFailed;

        public IEnumerable<IActionRuntimeStateSource> GetActionRuntimeStates() => runtimeStates.Values;

        public ActionBehaviorState(
            HfsmStateNode node,
            bool needsExitTime,
            bool isGhostState,
            MonoBehaviour mono,
            object userData,
            object parentFsm)
            : base(needsExitTime: needsExitTime, isGhostState: isGhostState)
        {
            this.node = node;
            this.mono = mono;
            this.userData = userData;
            this.parentFsm = parentFsm;

            InitializeExecutor();
        }

        private void InitializeExecutor()
        {
            if (node.BehaviorItems == null || node.BehaviorItems.Count == 0)
                return;

            executor = new BehaviorExecutor();
            executor.SetMonoBehaviour(mono);
            executor.SetUserData(userData);
            executor.SetFsm(parentFsm);

            var action = CreateActionTree(node);
            if (action != null)
            {
                executor.SetRoot(action);
            }
        }

        private IAction CreateActionTree(HfsmStateNode stateNode)
        {
            if (stateNode.BehaviorItems == null || stateNode.BehaviorItems.Count == 0)
                return null;

            var roots = stateNode.GetRootBehaviorItems();
            if (roots.Count == 1)
                behaviorRootId = roots[0].id;
            return BehaviorTreeBuilder.BuildInstrumentedFromEditorItems(
                stateNode.BehaviorItems,
                roots.Count == 1 ? roots[0].id : null,
                runtimeStates);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            behaviorCompleted = false;
            elapsedTime = 0f;
            executor?.Reset();
        }

        public override void OnLogic()
        {
            base.OnLogic();
            elapsedTime += Time.deltaTime;

            if (executor != null && !behaviorCompleted)
            {
                var status = executor.Tick(Time.deltaTime);
                if (status != BehaviorStatus.Running)
                {
                    behaviorCompleted = true;
                    OnBehaviorCompleted?.Invoke(behaviorRootId, status);
                    if (status == BehaviorStatus.Failure)
                        OnBehaviorFailed?.Invoke(behaviorRootId);
                    if (needsExitTime)
                        fsm?.StateCanExit();
                }
            }
        }

        public override void OnExit()
        {
            behaviorCompleted = true;
            executor?.ForceEnd();
            base.OnExit();
        }

        public void Trigger(TEvent trigger)
        {
            (parentFsm as ITriggerable<TEvent>)?.Trigger(trigger);
        }

        public void OnAction(TEvent trigger)
        {
            actionStorage?.RunAction(trigger);
        }

        public void OnAction<TData>(TEvent trigger, TData data)
        {
            actionStorage?.RunAction<TData>(trigger, data);
        }

        public ActionBehaviorState<TStateId, TEvent> AddAction(TEvent trigger, Action action)
        {
            actionStorage = actionStorage ?? new ActionStorage<TEvent>();
            actionStorage.AddAction(trigger, action);
            return this;
        }

        public ActionBehaviorState<TStateId, TEvent> AddAction<TData>(TEvent trigger, Action<TData> action)
        {
            actionStorage = actionStorage ?? new ActionStorage<TEvent>();
            actionStorage.AddAction<TData>(trigger, action);
            return this;
        }
    }
}
#endif
