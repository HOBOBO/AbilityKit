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

namespace UnityHFSM
{
    /// <summary>
    /// 支持行为树执行的状态机。
    /// 可以从 HfsmGraphAsset 初始化，并自动执行状态中的行为。
    /// </summary>
    public class ActionStateMachine<TStateId, TEvent> : StateMachine<TStateId, TEvent>, IActionable<TEvent>
    {
        private ActionStorage<TEvent> actionStorage;
        private readonly Dictionary<string, BehaviorExecutor> behaviorExecutors = new Dictionary<string, BehaviorExecutor>();
        private MonoBehaviour monoBehaviour;
        private object userData;

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

        /// <summary>
        /// 从 HfsmGraphAsset 初始化状态机（无 MonoBehaviour）
        /// </summary>
        public void InitializeFromGraph(HfsmGraphAsset graph)
        {
            if (graph == null)
                return;

            graph.Initialize();

            var rootSM = graph.GetRootStateMachine();
            if (rootSM != null)
            {
                BuildStateMachineFromNode(graph, rootSM, this);
            }
        }

        private void BuildStateMachineFromNode(HfsmGraphAsset graph, HfsmStateMachineNode smNode, object parentFsm)
        {
            foreach (var childId in smNode.ChildNodeIds)
            {
                var childNode = graph.GetNodeById(childId);
                if (childNode == null)
                    continue;

                if (childNode is HfsmStateMachineNode childSMNode)
                {
                    var subFsm = new HybridStateMachine<TStateId, TEvent>();
                    AddState((TStateId)(object)childNode.GetName(), subFsm);

                    BuildStateMachineFromNode(graph, childSMNode, subFsm);

                    if (!string.IsNullOrEmpty(smNode.DefaultStateId) && smNode.DefaultStateId == childId)
                    {
                        SetStartState((TStateId)(object)childNode.GetName());
                    }
                }
                else if (childNode is HfsmStateNode stateNode)
                {
                    var actionState = new ActionBehaviorState<TStateId, TEvent>(
                        stateNode,
                        stateNode.NeedsExitTime,
                        stateNode.IsGhostState,
                        monoBehaviour,
                        userData,
                        this
                    );

                    AddState((TStateId)(object)stateNode.GetName(), actionState);

                    if (stateNode.isDefault || (!string.IsNullOrEmpty(smNode.DefaultStateId) && smNode.DefaultStateId == childId))
                    {
                        SetStartState((TStateId)(object)stateNode.GetName());
                    }

                    actionState.OnBehaviorCompleted += (behaviorId, status) =>
                    {
                        OnBehaviorCompleted?.Invoke(behaviorId, stateNode.GetName(), status);
                    };

                    actionState.OnBehaviorFailed += (behaviorId) =>
                    {
                        OnBehaviorFailed?.Invoke(behaviorId, stateNode.GetName());
                    };
                }
            }
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
    public class ActionBehaviorState<TStateId, TEvent> : State<TStateId>, IActionable<TEvent>
    {
        private readonly HfsmStateNode node;
        private readonly MonoBehaviour mono;
        private readonly object userData;
        private readonly object parentFsm;
        private BehaviorExecutor executor;
        private ActionStorage<TEvent> actionStorage;
        private string behaviorRootId;
        private bool behaviorCompleted;

        public event Action<string, BehaviorStatus> OnBehaviorCompleted;
        public event Action<string> OnBehaviorFailed;

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
            return BehaviorTreeBuilder.BuildFromEditorItems(stateNode.BehaviorItems);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            behaviorCompleted = false;
            executor?.Reset();
        }

        public override void OnLogic()
        {
            base.OnLogic();

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
