using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.Continuous
{
    /// <summary>
    /// 持续行为执行状态
    /// </summary>
    public enum EContinuousState
    {
        /// <summary>等待条件满足</summary>
        Waiting,
        /// <summary>执行中</summary>
        Running,
        /// <summary>已暂停</summary>
        Paused,
        /// <summary>已中断</summary>
        Interrupted,
        /// <summary>已完成</summary>
        Completed,
    }

    /// <summary>
    /// 持续行为实例
    /// </summary>
    public interface IContinuousTriggerInstance
    {
        /// <summary>
        /// 实例唯一标识
        /// </summary>
        int InstanceId { get; }

        /// <summary>
        /// 关联的触发器计划ID
        /// </summary>
        int TriggerId { get; }

        /// <summary>
        /// 当前执行状态
        /// </summary>
        EContinuousState CurrentState { get; }

        /// <summary>
        /// 已执行次数
        /// </summary>
        int ExecutionCount { get; }

        /// <summary>
        /// 总消耗时间（毫秒）
        /// </summary>
        float ElapsedMs { get; }

        /// <summary>
        /// 上次执行时间（毫秒）
        /// </summary>
        float LastExecuteAtMs { get; }

        /// <summary>
        /// 最大执行次数，-1表示无限
        /// </summary>
        int MaxExecutions { get; }

        /// <summary>
        /// 是否可被中断
        /// </summary>
        bool CanBeInterrupted { get; }

        /// <summary>
        /// 中断原因
        /// </summary>
        string InterruptReason { get; }

        /// <summary>
        /// 是否已完成（达到最大执行次数）
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// 是否已终止（中断或完成）
        /// </summary>
        bool IsTerminated { get; }
    }

    /// <summary>
    /// 持续行为执行上下文
    /// </summary>
    public class ContinuousExecuteContext
    {
        /// <summary>
        /// 当前实例
        /// </summary>
        public IContinuousTriggerInstance Instance { get; internal set; }

        /// <summary>
        /// 上次执行时间（毫秒）
        /// </summary>
        public float LastExecuteAtMs => Instance.LastExecuteAtMs;

        /// <summary>
        /// 总消耗时间（毫秒）
        /// </summary>
        public float ElapsedMs => Instance.ElapsedMs;

        /// <summary>
        /// 已执行次数
        /// </summary>
        public int ExecutionCount => Instance.ExecutionCount;

        /// <summary>
        /// 本次执行是否是周期性的
        /// </summary>
        public bool IsTickExecution => Instance.LastExecuteAtMs > 0;

        /// <summary>
        /// 是否达到最大执行次数
        /// </summary>
        public bool IsMaxExecutionsReached => Instance.MaxExecutions > 0 && Instance.ExecutionCount >= Instance.MaxExecutions;
    }

    /// <summary>
    /// 持续行为执行器基类
    /// 提供通用的持续行为执行框架
    /// </summary>
    /// <typeparam name="TCtx">上下文类型</typeparam>
    public abstract class ContinuousExecutorBase<TCtx> where TCtx : class
    {
        /// <summary>
        /// 初始化持续行为（条件满足时调用一次）
        /// </summary>
        protected virtual void OnStart(TCtx ctx) { }

        /// <summary>
        /// 每帧执行逻辑
        /// </summary>
        protected virtual void OnUpdate(float deltaTimeMs, ContinuousExecuteContext execCtx, TCtx ctx) { }

        /// <summary>
        /// 持续行为结束时调用
        /// </summary>
        protected virtual void OnTerminate(EContinuousState terminationReason, TCtx ctx) { }

        /// <summary>
        /// 内部执行方法
        /// </summary>
        internal void Execute(float deltaTimeMs, IContinuousTriggerInstance instance, TCtx ctx)
        {
            var execCtx = new ContinuousExecuteContext { Instance = instance };
            OnUpdate(deltaTimeMs, execCtx, ctx);
        }

        /// <summary>
        /// 内部启动方法
        /// </summary>
        internal void Start(TCtx ctx)
        {
            OnStart(ctx);
        }

        /// <summary>
        /// 内部终止方法
        /// </summary>
        internal void Terminate(EContinuousState reason, TCtx ctx)
        {
            OnTerminate(reason, ctx);
        }
    }

    /// <summary>
    /// 持续行为执行器注册表
    /// 用于注册和管理持续行为执行器
    /// </summary>
    public static class ContinuousExecutorRegistry
    {
        private static readonly object _sync = new object();
        private static readonly Dictionary<int, ContinuousExecutorEntry> _executors = new Dictionary<int, ContinuousExecutorEntry>();
        private static int _nextTriggerId = 1000;

        private abstract class ContinuousExecutorEntry
        {
            public abstract float IntervalMs { get; }
            public abstract bool AcceptsContext(object ctx);
            public abstract void Start(object ctx);
            public abstract void Execute(float deltaTimeMs, IContinuousTriggerInstance instance, object ctx);
            public abstract void Terminate(EContinuousState reason, object ctx);
        }

        private class ContinuousExecutorEntry<TCtx> : ContinuousExecutorEntry where TCtx : class
        {
            public ContinuousExecutorBase<TCtx> Executor { get; }
            public override float IntervalMs { get; }

            public ContinuousExecutorEntry(ContinuousExecutorBase<TCtx> executor, float intervalMs)
            {
                Executor = executor;
                IntervalMs = intervalMs;
            }

            public override bool AcceptsContext(object ctx)
            {
                return ctx is TCtx;
            }

            public override void Start(object ctx)
            {
                Executor.Start((TCtx)ctx);
            }

            public override void Execute(float deltaTimeMs, IContinuousTriggerInstance instance, object ctx)
            {
                Executor.Execute(deltaTimeMs, instance, (TCtx)ctx);
            }

            public override void Terminate(EContinuousState reason, object ctx)
            {
                Executor.Terminate(reason, (TCtx)ctx);
            }
        }

        /// <summary>
        /// 注册一个持续行为执行器
        /// </summary>
        public static int Register<TCtx>(ContinuousExecutorBase<TCtx> executor, float intervalMs = 0) where TCtx : class
        {
            if (executor == null) throw new ArgumentNullException(nameof(executor));
            if (float.IsNaN(intervalMs) || float.IsInfinity(intervalMs) || intervalMs < 0)
                throw new ArgumentOutOfRangeException(nameof(intervalMs), "Execution interval must be finite and non-negative.");

            lock (_sync)
            {
                if (_nextTriggerId == int.MaxValue)
                    throw new InvalidOperationException("Continuous executor id space is exhausted.");

                var triggerId = _nextTriggerId++;
                _executors.Add(triggerId, new ContinuousExecutorEntry<TCtx>(executor, intervalMs));
                return triggerId;
            }
        }

        /// <summary>
        /// 尝试启动持续行为
        /// </summary>
        internal static bool TryStart(int triggerId, object ctx)
        {
            ContinuousExecutorEntry entry;
            lock (_sync)
            {
                if (!_executors.TryGetValue(triggerId, out entry))
                    return false;
            }

            if (!entry.AcceptsContext(ctx))
                return false;

            entry.Start(ctx);
            return true;
        }

        /// <summary>
        /// 尝试执行持续行为 tick
        /// </summary>
        internal static bool TryExecute(int triggerId, float deltaTimeMs, IContinuousTriggerInstance instance, object ctx)
        {
            if (instance == null)
                return false;

            ContinuousExecutorEntry entry;
            lock (_sync)
            {
                if (!_executors.TryGetValue(triggerId, out entry))
                    return false;
            }

            if (!entry.AcceptsContext(ctx))
                return false;

            entry.Execute(deltaTimeMs, instance, ctx);
            return true;
        }

        /// <summary>
        /// 尝试终止持续行为
        /// </summary>
        internal static bool TryTerminate(int triggerId, EContinuousState reason, object ctx)
        {
            ContinuousExecutorEntry entry;
            lock (_sync)
            {
                if (!_executors.TryGetValue(triggerId, out entry))
                    return false;
            }

            if (!entry.AcceptsContext(ctx))
                return false;

            entry.Terminate(reason, ctx);
            return true;
        }

        /// <summary>
        /// 获取执行间隔
        /// </summary>
        public static float GetInterval(int triggerId)
        {
            lock (_sync)
            {
                return _executors.TryGetValue(triggerId, out var entry) ? entry.IntervalMs : 0;
            }
        }

        /// <summary>
        /// 注销执行器
        /// </summary>
        public static bool Unregister(int triggerId)
        {
            lock (_sync)
            {
                return _executors.Remove(triggerId);
            }
        }

        /// <summary>
        /// 注册的触发器数量
        /// </summary>
        public static int Count
        {
            get
            {
                lock (_sync)
                {
                    return _executors.Count;
                }
            }
        }
    }
}
