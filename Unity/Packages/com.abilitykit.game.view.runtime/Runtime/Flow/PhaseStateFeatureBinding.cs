using System;
using System.Collections.Generic;

namespace AbilityKit.Game.View.Flow
{
    public sealed class PhaseStateFeatureBinding<TContext, TFeature>
        where TFeature : class, IPhaseFeature<TContext>
    {
        public delegate void PhaseContextAction(in TContext ctx);
        public delegate void PhaseEnterCompleteAction(in TContext ctx, int installedCount);
 
        private readonly PhaseFeaturePlan<TContext, TFeature>? _plan;
        private readonly IReadOnlyList<string>? _featureIds;
        private readonly Action<TFeature> _install;
        private readonly PhaseContextAction? _clear;
        private readonly PhaseContextAction? _beforeEnter;
        private readonly PhaseEnterCompleteAction? _afterEnter;
        private readonly PhaseEnterCompleteAction? _switchFlow;
        private readonly PhaseContextAction? _onExit;
        private readonly PhaseContextAction? _onEnterFailure;
        private readonly Action<string>? _fail;

        public PhaseStateFeatureBinding(
            string name,
            Action<TFeature> install,
            PhaseFeaturePlan<TContext, TFeature>? plan = null,
            IReadOnlyList<string>? featureIds = null,
            bool clearBeforeEnter = false,
            PhaseContextAction? clear = null,
            PhaseContextAction? beforeEnter = null,
            PhaseEnterCompleteAction? afterEnter = null,
            PhaseContextAction? onExit = null,
            Action<string>? fail = null,
            PhaseEnterCompleteAction? switchFlow = null,
            PhaseContextAction? onEnterFailure = null)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("State binding name is required.", nameof(name));
            Name = name;
            _install = install ?? throw new ArgumentNullException(nameof(install));
            _plan = plan;
            _featureIds = featureIds;
            ClearBeforeEnter = clearBeforeEnter;
            _clear = clear;
            _beforeEnter = beforeEnter;
            _afterEnter = afterEnter;
            _switchFlow = switchFlow;
            _onExit = onExit;
            _onEnterFailure = onEnterFailure;
            _fail = fail;
        }

        public string Name { get; }
        public bool ClearBeforeEnter { get; }
        public IReadOnlyList<string>? FeatureIds => _featureIds;

        public int Enter(in TContext ctx)
        {
            try
            {
                _beforeEnter?.Invoke(in ctx);

                if (ClearBeforeEnter)
                {
                    if (_clear == null)
                    {
                        throw new InvalidOperationException($"State binding '{Name}' requires a clear callback.");
                    }

                    _clear(in ctx);
                }

                var installed = 0;
                if (_plan != null)
                {
                    installed = _plan.InstallByIdsOrAll(_featureIds, in ctx, _install, _fail);
                }

                _afterEnter?.Invoke(in ctx, installed);
                _switchFlow?.Invoke(in ctx, installed);
                return installed;
            }
            catch (Exception enterException)
            {
                if (_onEnterFailure != null)
                {
                    try { _onEnterFailure(in ctx); }
                    catch (Exception rollbackException)
                    {
                        throw new AggregateException("State entry failed and feature rollback failed.",
                            enterException, rollbackException);
                    }
                }
                throw;
            }
        }
 
        public void Exit(in TContext ctx)
        {
            _onExit?.Invoke(in ctx);
        }
    }
}
