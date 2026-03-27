#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS || UNITY_SERVER || UNITY_SERVER
#define HFSM_UNITY
#endif

#if HFSM_UNITY
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityHFSM.Actions.Runtime
{
    /// <summary>
    /// 行为执行器，管理行为树的生命周期
    /// </summary>
    public class BehaviorExecutor
    {
        private IAction rootAction;
        private readonly BehaviorContext context;
        private readonly Dictionary<int, IAction> actionCache = new Dictionary<int, IAction>();

        private IYieldAction currentYieldAction;
        private Coroutine activeCoroutine;
        private MonoBehaviour monoBehaviour;

        public bool IsRunning { get; private set; }

        public event Action<BehaviorStatus> OnStatusChanged;

        public BehaviorExecutor()
        {
            context = new BehaviorContext();
        }

        /// <summary>
        /// 设置根行为
        /// </summary>
        public void SetRoot(IAction root)
        {
            rootAction = root;
            IsRunning = false;
        }

        /// <summary>
        /// 设置 MonoBehaviour 用于协程
        /// </summary>
        public void SetMonoBehaviour(MonoBehaviour mono)
        {
            monoBehaviour = mono;
            context.mono = mono;
        }

        /// <summary>
        /// 设置 FSM 引用
        /// </summary>
        public void SetFsm(object fsm)
        {
            context.fsm = fsm;
        }

        /// <summary>
        /// 设置用户数据
        /// </summary>
        public void SetUserData(object userData)
        {
            context.userData = userData;
        }

        /// <summary>
        /// 每帧更新
        /// </summary>
        public BehaviorStatus Tick(float deltaTime)
        {
            if (rootAction == null)
                return BehaviorStatus.Success;

            if (!IsRunning)
            {
                IsRunning = true;
                rootAction.Reset();
            }

            context.deltaTime = deltaTime;
            context.elapsedTime += deltaTime;

            // 处理协程
            if (activeCoroutine != null)
            {
                // 协程正在运行，保持 Running 状态
                return BehaviorStatus.Running;
            }

            var status = rootAction.Execute(context);

            OnStatusChanged?.Invoke(status);

            return status;
        }

        /// <summary>
        /// 启动协程
        /// </summary>
        public void StartCoroutine(IEnumerator routine)
        {
            if (monoBehaviour != null)
            {
                activeCoroutine = monoBehaviour.StartCoroutine(routine);
            }
        }

        /// <summary>
        /// 停止协程
        /// </summary>
        public void StopCoroutine()
        {
            if (activeCoroutine != null && monoBehaviour != null)
            {
                monoBehaviour.StopCoroutine(activeCoroutine);
                activeCoroutine = null;
            }
        }

        /// <summary>
        /// 重置执行器
        /// </summary>
        public void Reset()
        {
            rootAction?.Reset();
            StopCoroutine();
            context.elapsedTime = 0f;
            IsRunning = false;
        }

        /// <summary>
        /// 强制结束执行
        /// </summary>
        public void ForceEnd()
        {
            rootAction?.ForceEnd();
            StopCoroutine();
            IsRunning = false;
        }

        /// <summary>
        /// 获取变量
        /// </summary>
        public T GetVariable<T>(string name)
        {
            return context.GetVariable<T>(name);
        }

        /// <summary>
        /// 设置变量
        /// </summary>
        public void SetVariable<T>(string name, T value)
        {
            context.SetVariable(name, value);
        }
    }
}
#endif
