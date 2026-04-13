using System;
using System.Collections.Generic;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Common.Event;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Systems
{
    /// <summary>
    /// 触发器作用域管理器
    /// 用于批量管理触发器的注册和注销
    /// </summary>
    public sealed class TriggerScope : IDisposable
    {
        private readonly TriggerRunner<IWorldResolver> _runner;
        private readonly List<IDisposable> _registrations = new();
        private bool _disposed;

        public TriggerScope(TriggerRunner<IWorldResolver> runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        }

        /// <summary>
        /// 注册一个触发器计划
        /// </summary>
        public IDisposable RegisterPlan<TArgs>(EventKey<TArgs> key, in TriggerPlan<TArgs> plan) where TArgs : struct
        {
            ThrowIfDisposed();
            var token = _runner.RegisterPlan(key, plan);
            _registrations.Add(token);
            return token;
        }

        /// <summary>
        /// 注册一个自定义触发器
        /// </summary>
        public IDisposable Register<TArgs>(EventKey<TArgs> key, ITrigger<TArgs, IWorldResolver> trigger, int phase = 0, int priority = 0) where TArgs : struct
        {
            ThrowIfDisposed();
            var token = _runner.Register(key, trigger, phase, priority);
            _registrations.Add(token);
            return token;
        }

        /// <summary>
        /// 批量注册多个触发器
        /// </summary>
        public void RegisterPlans<TArgs>(IEnumerable<(EventKey<TArgs> Key, TriggerPlan<TArgs> Plan)> plans) where TArgs : struct
        {
            ThrowIfDisposed();
            foreach (var (key, plan) in plans)
            {
                RegisterPlan(key, in plan);
            }
        }

        /// <summary>
        /// 注销所有已注册的触发器
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            var count = _registrations.Count;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    _registrations[i]?.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "[TriggerScope] dispose registration failed");
                }
            }
            _registrations.Clear();

            Log.Info($"[TriggerScope] Disposed. released {count} registrations");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TriggerScope));
        }
    }

    /// <summary>
    /// 触发器注册辅助类
    /// </summary>
    public static class TriggerRegistrationExtensions
    {
        /// <summary>
        /// 创建TriggerScope
        /// </summary>
        public static TriggerScope CreateScope(this TriggerRunner<IWorldResolver> runner)
        {
            return new TriggerScope(runner);
        }

        /// <summary>
        /// 使用using语法便捷注册
        /// </summary>
        public static TriggerScope Use(this TriggerRunner<IWorldResolver> runner, Action<TriggerScope> configure)
        {
            var scope = new TriggerScope(runner);
            try
            {
                configure(scope);
                return scope;
            }
            catch
            {
                scope.Dispose();
                throw;
            }
        }
    }
}
