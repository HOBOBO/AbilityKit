#nullable enable
using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

using AbilityKit.HFSM.Definition;


namespace AbilityKit.HFSM.Runtime
{

    public sealed class RuntimeBindings<TOwner>
    {
        private readonly Dictionary<string, Func<IRuntimeState<TOwner>>> _stateFactories =
            new Dictionary<string, Func<IRuntimeState<TOwner>>>(StringComparer.Ordinal);

        private readonly Dictionary<string, Func<ITransitionCondition<TOwner>>> _conditionFactories =
            new Dictionary<string, Func<ITransitionCondition<TOwner>>>(StringComparer.Ordinal);

        private readonly Dictionary<string, Func<ITransitionAction<TOwner>>> _actionFactories =
            new Dictionary<string, Func<ITransitionAction<TOwner>>>(StringComparer.Ordinal);

        public RuntimeBindings<TOwner> RegisterState(
            string key,
            Func<IRuntimeState<TOwner>> factory)
        {
            Register(_stateFactories, key, factory, "state");
            return this;
        }

        public RuntimeBindings<TOwner> RegisterCondition(
            string key,
            Func<ITransitionCondition<TOwner>> factory)
        {
            Register(_conditionFactories, key, factory, "condition");
            return this;
        }

        public RuntimeBindings<TOwner> RegisterAction(
            string key,
            Func<ITransitionAction<TOwner>> factory)
        {
            Register(_actionFactories, key, factory, "action");
            return this;
        }

        internal IRuntimeState<TOwner> CreateState(string key)
        {
            if (string.IsNullOrEmpty(key)) return new NoOpState();
            return Create(_stateFactories, key, "state");
        }

        internal ITransitionCondition<TOwner>? CreateCondition(string key)
        {
            return string.IsNullOrEmpty(key) ? null : Create(_conditionFactories, key, "condition");
        }

        internal ITransitionAction<TOwner>? CreateAction(string key)
        {
            return string.IsNullOrEmpty(key) ? null : Create(_actionFactories, key, "action");
        }

        private static void Register<T>(Dictionary<string, Func<T>> factories, string key, Func<T> factory, string kind)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException($"HFSM {kind} binding key is required.", nameof(key));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (!factories.TryAdd(key, factory))
                throw new InvalidOperationException($"HFSM {kind} binding '{key}' is already registered.");
        }

        private static T Create<T>(Dictionary<string, Func<T>> factories, string key, string kind)
        {
            if (!factories.TryGetValue(key, out var factory))
                throw new InvalidOperationException($"HFSM {kind} binding '{key}' is not registered.");

            var instance = factory();
            if (instance == null)
                throw new InvalidOperationException($"HFSM {kind} binding '{key}' returned null.");
            return instance;
        }

        private sealed class NoOpState : RuntimeStateBase<TOwner>
        {
        }
    }
}
