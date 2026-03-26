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

            executor.OnStatusChanged += (status) =>
            {
                if (status == BehaviorStatus.Failure)
                {
                    OnBehaviorFailed?.Invoke(node.RootBehaviorId);
                }
            };

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

            var rootItems = stateNode.GetRootBehaviorItems();
            if (rootItems.Count == 0)
                return null;

            if (rootItems.Count == 1)
            {
                return CreateAction(rootItems[0], stateNode);
            }

            var sequence = new SequenceAction();
            foreach (var item in rootItems)
            {
                var action = CreateAction(item, stateNode);
                if (action != null)
                {
                    sequence.children.Add(action);
                }
            }

            return sequence.children.Count > 0 ? sequence : null;
        }

        private IAction CreateAction(HfsmBehaviorItem item, HfsmStateNode stateNode)
        {
            if (item == null)
                return null;

            IAction action = item.Type switch
            {
                HfsmBehaviorType.Wait => CreateWaitAction(item),
                HfsmBehaviorType.Log => CreateLogAction(item),
                HfsmBehaviorType.SetFloat => CreateSetFloatAction(item),
                HfsmBehaviorType.SetBool => CreateSetBoolAction(item),
                HfsmBehaviorType.SetInt => CreateSetIntAction(item),
                HfsmBehaviorType.PlayAnimation => CreatePlayAnimationAction(item),
                HfsmBehaviorType.SetActive => CreateSetActiveAction(item),
                HfsmBehaviorType.MoveTo => CreateMoveToAction(item),
                HfsmBehaviorType.Sequence => CreateSequenceAction(item, stateNode),
                HfsmBehaviorType.Selector => CreateSelectorAction(item, stateNode),
                HfsmBehaviorType.Parallel => CreateParallelAction(item, stateNode),
                HfsmBehaviorType.RandomSelector => CreateRandomSelectorAction(item, stateNode),
                HfsmBehaviorType.RandomSequence => CreateRandomSequenceAction(item, stateNode),
                HfsmBehaviorType.Repeat => CreateRepeatAction(item, stateNode),
                HfsmBehaviorType.Invert => CreateInvertAction(item, stateNode),
                HfsmBehaviorType.TimeLimit => CreateTimeLimitAction(item, stateNode),
                HfsmBehaviorType.UntilSuccess => CreateUntilSuccessAction(item, stateNode),
                HfsmBehaviorType.UntilFailure => CreateUntilFailureAction(item, stateNode),
                HfsmBehaviorType.Cooldown => CreateCooldownAction(item, stateNode),
                _ => null
            };

            return action;
        }

        private WaitAction CreateWaitAction(HfsmBehaviorItem item)
        {
            var duration = item.GetParamValue<float>("duration");
            return new WaitAction(duration);
        }

        private LogAction CreateLogAction(HfsmBehaviorItem item)
        {
            var message = item.GetParamValue<string>("message");
            return new LogAction(message);
        }

        private SetFloatAction CreateSetFloatAction(HfsmBehaviorItem item)
        {
            var varName = item.GetParamValue<string>("variableName");
            var value = item.GetParamValue<float>("value");
            return new SetFloatAction(varName, value);
        }

        private SetBoolAction CreateSetBoolAction(HfsmBehaviorItem item)
        {
            var varName = item.GetParamValue<string>("variableName");
            var value = item.GetParamValue<bool>("value");
            return new SetBoolAction(varName, value);
        }

        private SetIntAction CreateSetIntAction(HfsmBehaviorItem item)
        {
            var varName = item.GetParamValue<string>("variableName");
            var value = item.GetParamValue<int>("value");
            return new SetIntAction(varName, value);
        }

        private PlayAnimationAction CreatePlayAnimationAction(HfsmBehaviorItem item)
        {
            var stateName = item.GetParamValue<string>("stateName");
            var duration = item.GetParamValue<float>("crossFadeDuration");
            return new PlayAnimationAction(stateName, duration);
        }

        private SetActiveAction CreateSetActiveAction(HfsmBehaviorItem item)
        {
            var target = item.GetParamValue<UnityEngine.Object>("target");
            var active = item.GetParamValue<bool>("active");
            var action = new SetActiveAction();
            action.targetObject = target;
            action.active = active;
            return action;
        }

        private MoveToAction CreateMoveToAction(HfsmBehaviorItem item)
        {
            var target = item.GetParamValue<Transform>("target");
            var dest = item.GetParamValue<Vector3>("destination");
            var speed = item.GetParamValue<float>("speed");
            var action = new MoveToAction(target, dest, speed);
            return action;
        }

        private SequenceAction CreateSequenceAction(HfsmBehaviorItem item, HfsmStateNode stateNode)
        {
            var sequence = new SequenceAction();
            foreach (var childId in item.childIds)
            {
                var child = stateNode.GetBehaviorItem(childId);
                if (child != null)
                {
                    var childAction = CreateAction(child, stateNode);
                    if (childAction != null)
                    {
                        sequence.children.Add(childAction);
                    }
                }
            }
            return sequence;
        }

        private SelectorAction CreateSelectorAction(HfsmBehaviorItem item, HfsmStateNode stateNode)
        {
            var selector = new SelectorAction();
            foreach (var childId in item.childIds)
            {
                var child = stateNode.GetBehaviorItem(childId);
                if (child != null)
                {
                    var childAction = CreateAction(child, stateNode);
                    if (childAction != null)
                    {
                        selector.children.Add(childAction);
                    }
                }
            }
            return selector;
        }

        private ParallelAction CreateParallelAction(HfsmBehaviorItem item, HfsmStateNode stateNode)
        {
            var parallel = new ParallelAction();
            parallel.failOnAnyFailure = item.GetParamValue<bool>("failOnAnyFailure");

            foreach (var childId in item.childIds)
            {
                var child = stateNode.GetBehaviorItem(childId);
                if (child != null)
                {
                    var childAction = CreateAction(child, stateNode);
                    if (childAction != null)
                    {
                        parallel.children.Add(childAction);
                    }
                }
            }
            return parallel;
        }

        private RandomSelectorAction CreateRandomSelectorAction(HfsmBehaviorItem item, HfsmStateNode stateNode)
        {
            var randomSelector = new RandomSelectorAction();
            foreach (var childId in item.childIds)
            {
                var child = stateNode.GetBehaviorItem(childId);
                if (child != null)
                {
                    var childAction = CreateAction(child, stateNode);
                    if (childAction != null)
                    {
                        randomSelector.children.Add(childAction);
                    }
                }
            }
            return randomSelector;
        }

        private RandomSequenceAction CreateRandomSequenceAction(HfsmBehaviorItem item, HfsmStateNode stateNode)
        {
            var randomSeq = new RandomSequenceAction();
            foreach (var childId in item.childIds)
            {
                var child = stateNode.GetBehaviorItem(childId);
                if (child != null)
                {
                    var childAction = CreateAction(child, stateNode);
                    if (childAction != null)
                    {
                        randomSeq.children.Add(childAction);
                    }
                }
            }
            return randomSeq;
        }

        private RepeatAction CreateRepeatAction(HfsmBehaviorItem item, HfsmStateNode stateNode)
        {
            var count = item.GetParamValue<int>("count");
            var childId = item.childIds.Count > 0 ? item.childIds[0] : null;
            IAction child = null;

            if (!string.IsNullOrEmpty(childId))
            {
                var childItem = stateNode.GetBehaviorItem(childId);
                if (childItem != null)
                {
                    child = CreateAction(childItem, stateNode);
                }
            }

            return new RepeatAction(child, count);
        }

        private InvertAction CreateInvertAction(HfsmBehaviorItem item, HfsmStateNode stateNode)
        {
            var childId = item.childIds.Count > 0 ? item.childIds[0] : null;
            IAction child = null;

            if (!string.IsNullOrEmpty(childId))
            {
                var childItem = stateNode.GetBehaviorItem(childId);
                if (childItem != null)
                {
                    child = CreateAction(childItem, stateNode);
                }
            }

            return new InvertAction(child);
        }

        private TimeLimitAction CreateTimeLimitAction(HfsmBehaviorItem item, HfsmStateNode stateNode)
        {
            var timeLimit = item.GetParamValue<float>("timeLimit");
            var childId = item.childIds.Count > 0 ? item.childIds[0] : null;
            IAction child = null;

            if (!string.IsNullOrEmpty(childId))
            {
                var childItem = stateNode.GetBehaviorItem(childId);
                if (childItem != null)
                {
                    child = CreateAction(childItem, stateNode);
                }
            }

            return new TimeLimitAction(child, timeLimit);
        }

        private UntilSuccessAction CreateUntilSuccessAction(HfsmBehaviorItem item, HfsmStateNode stateNode)
        {
            var childId = item.childIds.Count > 0 ? item.childIds[0] : null;
            IAction child = null;

            if (!string.IsNullOrEmpty(childId))
            {
                var childItem = stateNode.GetBehaviorItem(childId);
                if (childItem != null)
                {
                    child = CreateAction(childItem, stateNode);
                }
            }

            return new UntilSuccessAction(child);
        }

        private UntilFailureAction CreateUntilFailureAction(HfsmBehaviorItem item, HfsmStateNode stateNode)
        {
            var childId = item.childIds.Count > 0 ? item.childIds[0] : null;
            IAction child = null;

            if (!string.IsNullOrEmpty(childId))
            {
                var childItem = stateNode.GetBehaviorItem(childId);
                if (childItem != null)
                {
                    child = CreateAction(childItem, stateNode);
                }
            }

            return new UntilFailureAction(child);
        }

        private CooldownAction CreateCooldownAction(HfsmBehaviorItem item, HfsmStateNode stateNode)
        {
            var cooldown = item.GetParamValue<float>("cooldownDuration");
            var childId = item.childIds.Count > 0 ? item.childIds[0] : null;
            IAction child = null;

            if (!string.IsNullOrEmpty(childId))
            {
                var childItem = stateNode.GetBehaviorItem(childId);
                if (childItem != null)
                {
                    child = CreateAction(childItem, stateNode);
                }
            }

            return new CooldownAction(child, cooldown);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            executor?.Reset();
        }

        public override void OnLogic()
        {
            base.OnLogic();

            if (executor != null && executor.IsRunning)
            {
                var status = executor.Tick(Time.deltaTime);
                OnBehaviorCompleted?.Invoke(node.RootBehaviorId, status);

                if (status == BehaviorStatus.Success && needsExitTime)
                {
                    fsm?.StateCanExit();
                }
                else if (status == BehaviorStatus.Failure)
                {
                    OnBehaviorFailed?.Invoke(node.RootBehaviorId);
                    if (needsExitTime)
                    {
                        fsm?.StateCanExit();
                    }
                }
            }
        }

        public override void OnExit()
        {
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
