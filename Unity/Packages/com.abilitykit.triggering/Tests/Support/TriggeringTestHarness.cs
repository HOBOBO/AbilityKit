using System;
using System.Collections.Generic;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Testing
{
    /// <summary>
    /// 装配触发器测试所需的事件总线、注册表和可替换的宿主上下文。
    /// </summary>
    public sealed class TriggeringTestHarness<TCtx> : IDisposable
    {
        private readonly MutableContextSource _contextSource;
        private readonly List<IDisposable> _registrations;
        private bool _disposed;

        /// <summary>
        /// 复用传入的服务或创建默认总线与注册表；默认不装配调度。
        /// 注入的扁平动作调度由测试宿主持有、推进和清理，夹具只释放自身登记的句柄。
        /// </summary>
        public TriggeringTestHarness(
            TCtx context = default(TCtx),
            EventBus eventBus = null,
            FunctionRegistry functions = null,
            ActionRegistry actions = null,
            IFlatActionScheduler flatActionScheduler = null,
            ExecPolicy policy = default(ExecPolicy),
            EInterruptPolicy interruptPolicy = EInterruptPolicy.None)
        {
            EventBus = eventBus ?? new EventBus();
            Functions = functions ?? new FunctionRegistry();
            Actions = actions ?? new ActionRegistry();
            _contextSource = new MutableContextSource(context);
            _registrations = new List<IDisposable>();

            Runner = new TriggerRunner<TCtx>(
                EventBus,
                Functions,
                Actions,
                contextSource: _contextSource,
                flatActionScheduler: flatActionScheduler,
                policy: policy,
                interruptPolicy: interruptPolicy);
        }

        /// <summary>
        /// 返回供运行器订阅和测试发事件的总线。
        /// </summary>
        public EventBus EventBus { get; }

        /// <summary>
        /// 返回计划条件使用的函数注册表。
        /// </summary>
        public FunctionRegistry Functions { get; }

        /// <summary>
        /// 返回计划执行使用的动作注册表。
        /// </summary>
        public ActionRegistry Actions { get; }

        /// <summary>
        /// 返回已装配的运行器；通过它直接注册的句柄由调用方管理。
        /// </summary>
        public TriggerRunner<TCtx> Runner { get; }

        /// <summary>
        /// 读取或替换运行器下一次取用的宿主上下文。
        /// </summary>
        public TCtx Context
        {
            get => _contextSource.Current;
            set => _contextSource.Current = value;
        }

        /// <summary>
        /// 返回与 Context 共享当前值的上下文来源。
        /// </summary>
        public ITriggerContextSource<TCtx> ContextSource => _contextSource;

        /// <summary>
        /// 返回夹具是否已释放。
        /// </summary>
        public bool IsDisposed => _disposed;

        /// <summary>
        /// 更新上下文并返回当前夹具；释放后不可调用。
        /// </summary>
        public TriggeringTestHarness<TCtx> SetContext(TCtx context)
        {
            ThrowIfDisposed();
            _contextSource.Current = context;
            return this;
        }

        /// <summary>
        /// 将委托注册到动作表并返回当前夹具；清理订阅不会移除此委托。
        /// </summary>
        public TriggeringTestHarness<TCtx> RegisterAction<TDelegate>(ActionId actionId, TDelegate action, bool isDeterministic = true)
            where TDelegate : Delegate
        {
            ThrowIfDisposed();
            Actions.Register(actionId, action, isDeterministic);
            return this;
        }

        /// <summary>
        /// 使用整数动作编号注册委托，登记规则与类型化编号相同。
        /// </summary>
        public TriggeringTestHarness<TCtx> RegisterAction<TDelegate>(int actionId, TDelegate action, bool isDeterministic = true)
            where TDelegate : Delegate
        {
            return RegisterAction(new ActionId(actionId), action, isDeterministic);
        }

        /// <summary>
        /// 将委托注册到函数表并返回当前夹具；清理订阅不会移除此委托。
        /// </summary>
        public TriggeringTestHarness<TCtx> RegisterFunction<TDelegate>(FunctionId functionId, TDelegate function, bool isDeterministic = true)
            where TDelegate : Delegate
        {
            ThrowIfDisposed();
            Functions.Register(functionId, function, isDeterministic);
            return this;
        }

        /// <summary>
        /// 使用整数函数编号注册委托，登记规则与类型化编号相同。
        /// </summary>
        public TriggeringTestHarness<TCtx> RegisterFunction<TDelegate>(int functionId, TDelegate function, bool isDeterministic = true)
            where TDelegate : Delegate
        {
            return RegisterFunction(new FunctionId(functionId), function, isDeterministic);
        }

        /// <summary>
        /// 注册手写触发器；返回订阅句柄，并记录供清理时释放。
        /// </summary>
        public IDisposable Register<TArgs>(EventKey<TArgs> key, ITrigger<TArgs, TCtx> trigger, int phase = 0, int priority = 0)
        {
            ThrowIfDisposed();
            var registration = Runner.Register(key, trigger, phase, priority);
            Track(registration);
            return registration;
        }

        /// <summary>
        /// 注册类型化计划；返回订阅句柄，并记录供清理时释放。
        /// </summary>
        public IDisposable RegisterPlan<TArgs>(EventKey<TArgs> key, in TriggerPlan<TArgs> plan)
        {
            ThrowIfDisposed();
            var registration = Runner.RegisterPlan(key, in plan);
            Track(registration);
            return registration;
        }

        /// <summary>
        /// 按事件编号和载荷类型注册计划；返回并跟踪订阅句柄。
        /// </summary>
        public IDisposable RegisterPlan(int eventId, Type argsType, in TriggerPlan<object> plan)
        {
            ThrowIfDisposed();
            var registration = Runner.RegisterPlan(eventId, argsType, in plan);
            Track(registration);
            return registration;
        }

        /// <summary>
        /// 从编号构造类型化事件键，不注册订阅。
        /// </summary>
        public EventKey<TArgs> CreateEventKey<TArgs>(int eventId)
        {
            return new EventKey<TArgs>(eventId);
        }

        /// <summary>
        /// 按事件编号发布载荷并刷新队列。
        /// </summary>
        public void Publish<TArgs>(int eventId, in TArgs args)
        {
            Publish(new EventKey<TArgs>(eventId), in args);
        }

        /// <summary>
        /// 发布类型化事件并刷新队列。
        /// </summary>
        public void Publish<TArgs>(EventKey<TArgs> key, in TArgs args)
        {
            ThrowIfDisposed();
            EventBus.Publish(key, in args);
            EventBus.Flush();
        }

        /// <summary>
        /// 使用指定执行控制发布类型化事件并刷新队列。
        /// </summary>
        public void Publish<TArgs>(EventKey<TArgs> key, in TArgs args, ExecutionControl control)
        {
            ThrowIfDisposed();
            EventBus.Publish(key, in args, control);
            EventBus.Flush();
        }

        /// <summary>
        /// 刷新事件队列，不推进时钟。
        /// </summary>
        public void FlushEvents()
        {
            ThrowIfDisposed();
            EventBus.Flush();
        }

        /// <summary>
        /// 按登记的逆序尽力释放订阅句柄，夹具仍可继续使用。
        /// </summary>
        public void ClearRegistrations()
        {
            ThrowIfDisposed();
            DisposeRegistrations();
        }

        /// <summary>
        /// 尽力释放夹具登记的订阅；重复调用无操作。
        /// </summary>
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
                    /*
                     * 单个句柄释放失败不应阻止清理其余测试订阅。
                     */
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

    }
}
