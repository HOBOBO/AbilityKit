using System;
using System.Collections.Generic;
using AbilityKit.Game.Flow;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleContextInputGateTests
    {
        [Test]
        public void SeparateRecoveryReasonsCannotReenableEachOther()
        {
            var context = BattleContext.Rent();
            try
            {
                context.SetGameplayInputBlocked(BattleGameplayInputBlockReason.PauseRecovery, true);
                context.SetGameplayInputBlocked(BattleGameplayInputBlockReason.AuthoritativeRecovery, true);
                context.SetGameplayInputBlocked(BattleGameplayInputBlockReason.PauseRecovery, false);
                Assert.IsFalse(context.CanSubmitGameplayInput);

                context.SetGameplayInputBlocked(BattleGameplayInputBlockReason.AuthoritativeRecovery, false);
                Assert.IsTrue(context.CanSubmitGameplayInput);
            }
            finally
            {
                BattleContext.Return(context);
            }
        }

        [Test]
        public void PoolReuseClearsRecoveryReasons()
        {
            var context = BattleContext.Rent();
            context.SetGameplayInputBlocked(BattleGameplayInputBlockReason.PauseRecovery, true);
            BattleContext.Return(context);

            var reused = BattleContext.Rent();
            try
            {
                Assert.IsTrue(reused.CanSubmitGameplayInput);
            }
            finally
            {
                BattleContext.Return(reused);
            }
        }

        [Test]
        public void ContextFeature_PublishesAndWithdrawsNarrowPorts()
        {
            var store = new PortStore();
            var phase = new GamePhaseContext(null, store, null);
            var feature = new BattleContextFeature();

            feature.OnAttach(phase);
            var context = phase.RequireFeature<BattleContext>(nameof(BattleContextInputGateTests));
            Assert.AreSame(context, phase.RequireFeature<IBattleRuntimeContext>(nameof(BattleContextInputGateTests)));
            Assert.AreSame(context, phase.RequireFeature<IBattleEntityContext>(nameof(BattleContextInputGateTests)));
            feature.OnDetach(phase);

            Assert.IsFalse(store.TryGet(out IBattleRuntimeContext _));
            Assert.IsFalse(store.TryGet(out BattleContext _));
        }

        [Test]
        public void ContextFeature_DoesNotReturnCallerOwnedContext()
        {
            var store = new PortStore();
            var context = BattleContext.Rent();
            store.Set(context);
            var phase = new GamePhaseContext(null, store, null);
            var feature = new BattleContextFeature();
            try
            {
                feature.OnAttach(phase);
                feature.OnDetach(phase);
                Assert.IsTrue(store.TryGet(out BattleContext retained));
                Assert.AreSame(context, retained);
                Assert.IsFalse(store.TryGet(out IBattleRuntimeContext _));
            }
            finally
            {
                store.Remove<BattleContext>();
                BattleContext.Return(context);
            }
        }

        private sealed class PortStore : IGameFeatureStore
        {
            private readonly Dictionary<Type, object> _values = new Dictionary<Type, object>();

            public bool TryGet<T>(out T component) where T : class
            {
                component = _values.TryGetValue(typeof(T), out var value) ? value as T : null;
                return component != null;
            }

            public void Set<T>(T component) where T : class => _values[typeof(T)] = component;
            public void Remove<T>() where T : class => _values.Remove(typeof(T));
            public void Remove(Type componentType) => _values.Remove(componentType);
        }
    }
}
