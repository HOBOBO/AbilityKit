using System;
using System.Collections.Generic;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.ActionScheduler;
using AbilityKit.Triggering.Runtime.Dispatcher;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Testing
{
    /// <summary>
    /// 用于构建确定性 TriggerRunner 测试环境的可复用测试夹具。
    /// 隐藏重复的 EventBus / FunctionRegistry / ActionRegistry / scheduler 搭建过程。
    /// </summary>
    public sealed class TriggeringTestHarness<TCtx> : IDisposable
    {
        private readonly MutableContextSource _contextSource;
        private readonly List<IDisposable> _registrations;
        private bool _disposed;

        public TriggeringTestHarness(
            TCtx context = default(TCtx),
            EventBus eventBus = null,
            FunctionRegistry functions = null,
            ActionRegistry actions = null,
            ActionSchedulerManager actionSchedulerManager = null,
            ExecPolicy policy = default(ExecPolicy),
            EInterruptPolicy interruptPolicy = EInterruptPolicy.None)
        {
            EventBus = eventBus ?? new EventBus();
            Functions = functions ?? new FunctionRegistry();
            Actions = actions ?? new ActionRegistry();
            SchedulerManager = actionSchedulerManager ?? new ActionSchedulerManager();
            _contextSource = new MutableContextSource(context);
            _registrations = new List<IDisposable>();

            Runner = new TriggerRunner<TCtx>(
                EventBus,
                Functions,
                Actions,
                contextSource: _contextSource,
                actionSchedulerManager: SchedulerManager,
                policy: policy,
                interruptPolicy: interruptPolicy);
        }

        public EventBus EventBus { get; }
        public FunctionRegistry Functions { get; }
        public ActionRegistry Actions { get; }
        public ActionSchedulerManager SchedulerManager { get; }
        public TriggerRunner<TCtx> Runner { get; }
        public TCtx Context
        {
            get => _contextSource.Current;
            set => _contextSource.Current = value;
        }

        public ITriggerContextSource<TCtx> ContextSource => _contextSource;

        public float CurrentTimeMs { get; private set; }

        public bool IsDisposed => _disposed;

        public TriggeringTestHarness<TCtx> SetContext(TCtx context)
        {
            ThrowIfDisposed();
            _contextSource.Current = context;
            return this;
        }

        public TriggeringTestHarness<TCtx> RegisterAction<TDelegate>(ActionId actionId, TDelegate action, bool isDeterministic = true)
            where TDelegate : Delegate
        {
            ThrowIfDisposed();
            Actions.Register(actionId, action, isDeterministic);
            return this;
        }

        public TriggeringTestHarness<TCtx> RegisterAction<TDelegate>(int actionId, TDelegate action, bool isDeterministic = true)
            where TDelegate : Delegate
        {
            return RegisterAction(new ActionId(actionId), action, isDeterministic);
        }

        public TriggeringTestHarness<TCtx> RegisterFunction<TDelegate>(FunctionId functionId, TDelegate function, bool isDeterministic = true)
            where TDelegate : Delegate
        {
            ThrowIfDisposed();
            Functions.Register(functionId, function, isDeterministic);
            return this;
        }

        public TriggeringTestHarness<TCtx> RegisterFunction<TDelegate>(int functionId, TDelegate function, bool isDeterministic = true)
            where TDelegate : Delegate
        {
            return RegisterFunction(new FunctionId(functionId), function, isDeterministic);
        }

        public IDisposable Register<TArgs>(EventKey<TArgs> key, ITrigger<TArgs, TCtx> trigger, int phase = 0, int priority = 0)
        {
            ThrowIfDisposed();
            var registration = Runner.Register(key, trigger, phase, priority);
            Track(registration);
            return registration;
        }

        public IDisposable RegisterPlan<TArgs>(EventKey<TArgs> key, in TriggerPlan<TArgs> plan)
        {
            ThrowIfDisposed();
            var registration = Runner.RegisterPlan(key, in plan);
            Track(registration);
            return registration;
        }

        public IDisposable RegisterPlan(int eventId, Type argsType, in TriggerPlan<object> plan)
        {
            ThrowIfDisposed();
            var registration = Runner.RegisterPlan(eventId, argsType, in plan);
            Track(registration);
            return registration;
        }

        public EventKey<TArgs> CreateEventKey<TArgs>(int eventId)
        {
            return new EventKey<TArgs>(eventId);
        }

        public void Publish<TArgs>(int eventId, in TArgs args)
        {
            Publish(new EventKey<TArgs>(eventId), in args);
        }

        public void Publish<TArgs>(EventKey<TArgs> key, in TArgs args)
        {
            ThrowIfDisposed();
            EventBus.Publish(key, in args);
            EventBus.Flush();
        }

        public void Publish<TArgs>(EventKey<TArgs> key, in TArgs args, ExecutionControl control)
        {
            ThrowIfDisposed();
            EventBus.Publish(key, in args, control);
            EventBus.Flush();
        }

        public void AdvanceTime(float deltaTimeMs)
        {
            ThrowIfDisposed();
            if (deltaTimeMs < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTimeMs), deltaTimeMs, "Delta time must be non-negative.");

            CurrentTimeMs += deltaTimeMs;
            SchedulerManager.Update(deltaTimeMs, new TestDispatcherContext(_contextSource.Current, CurrentTimeMs));
            EventBus.Flush();
        }

        public void FlushEvents()
        {
            ThrowIfDisposed();
            EventBus.Flush();
        }

        public void ClearRegistrations()
        {
            ThrowIfDisposed();
            DisposeRegistrations();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DisposeRegistrations();
        }

        private void Track(IDisposable registration)
        {
            if (registration == null) return;
            _registrations.Add(registration);
        }

        private void DisposeRegistrations()
        {
            for (int i = _registrations.Count - 1; i >= 0; i--)
            {
                try
                {
                    _registrations[i]?.Dispose();
                }
                catch
                {
                    // 测试夹具清理只做尽力而为处理。
                }
            }

            _registrations.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TriggeringTestHarness<TCtx>));
            }
        }

        private sealed class MutableContextSource : ITriggerContextSource<TCtx>
        {
            public MutableContextSource(TCtx context)
            {
                Current = context;
            }

            public TCtx Current { get; set; }

            public TCtx GetContext()
            {
                return Current;
            }
        }

        private sealed class TestDispatcherContext : ITriggerDispatcherContext
        {
            public TestDispatcherContext(object context, float currentTimeMs)
            {
                Context = context;
                CurrentTimeMs = currentTimeMs;
            }

            public object Context { get; }

            public float CurrentTimeMs { get; }

            public T GetService<T>() where T : class
            {
                return Context as T;
            }
        }
    }
}
