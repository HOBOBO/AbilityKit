using System;

namespace AbilityKit.Game.View.Flow
{
    public static class PhaseStateFeatureBindingFactory
    {
        public delegate void PhaseActionResolver<TContext>(in TContext ctx, string actionId);
        public delegate void PhaseEnterCompleteActionResolver<TContext>(in TContext ctx, string actionId, int installedCount);

        public static PhaseStateFeatureBinding<TContext, TFeature> Create<TContext, TFeature>(
            PhaseStateFeatureSpec spec,
            Action<TFeature> install,
            PhaseFeaturePlan<TContext, TFeature>? plan = null,
            PhaseStateFeatureBinding<TContext, TFeature>.PhaseContextAction? clear = null,
            PhaseStateFeatureBinding<TContext, TFeature>.PhaseContextAction? beforeEnter = null,
            PhaseStateFeatureBinding<TContext, TFeature>.PhaseEnterCompleteAction? afterEnter = null,
            PhaseStateFeatureBinding<TContext, TFeature>.PhaseContextAction? onExit = null,
            PhaseActionResolver<TContext>? enterBeforeAction = null,
            PhaseEnterCompleteActionResolver<TContext>? enterAfterAction = null,
            PhaseActionResolver<TContext>? exitAction = null,
            Action<string>? fail = null,
            PhaseEnterCompleteActionResolver<TContext>? switchFlowAction = null,
            PhaseStateFeatureBinding<TContext, TFeature>.PhaseContextAction? onEnterFailure = null)
            where TFeature : class, IPhaseFeature<TContext>
            {
                if (spec == null) throw new ArgumentNullException(nameof(spec));

                spec.Freeze();

                return new PhaseStateFeatureBinding<TContext, TFeature>(
                    spec.StateId,
                    install,
                    plan,
                    spec.FeatureIds,
                    spec.ClearBeforeEnter,
                    clear,
                    BuildBeforeEnter(spec, beforeEnter, enterBeforeAction),
                    BuildAfterEnter(spec, afterEnter, enterAfterAction),
                    BuildExit(spec, onExit, exitAction),
                    fail,
                    BuildSwitchFlow<TContext, TFeature>(spec, switchFlowAction),
                    onEnterFailure);
            }

        private static PhaseStateFeatureBinding<TContext, TFeature>.PhaseContextAction? BuildBeforeEnter<TContext, TFeature>(
            PhaseStateFeatureSpec spec,
            PhaseStateFeatureBinding<TContext, TFeature>.PhaseContextAction? beforeEnter,
            PhaseActionResolver<TContext>? enterBeforeAction)
            where TFeature : class, IPhaseFeature<TContext>
        {
            if (enterBeforeAction == null || spec.EnterBeforeActionIds.Count == 0)
            {
                return beforeEnter;
            }

            return (in TContext ctx) =>
            {
                beforeEnter?.Invoke(in ctx);
                for (var i = 0; i < spec.EnterBeforeActionIds.Count; i++)
                {
                    enterBeforeAction(in ctx, spec.EnterBeforeActionIds[i]);
                }
            };
        }

        private static PhaseStateFeatureBinding<TContext, TFeature>.PhaseEnterCompleteAction? BuildAfterEnter<TContext, TFeature>(
            PhaseStateFeatureSpec spec,
            PhaseStateFeatureBinding<TContext, TFeature>.PhaseEnterCompleteAction? afterEnter,
            PhaseEnterCompleteActionResolver<TContext>? enterAfterAction)
            where TFeature : class, IPhaseFeature<TContext>
        {
            if (enterAfterAction == null || spec.EnterAfterActionIds.Count == 0)
            {
                return afterEnter;
            }

            return (in TContext ctx, int installedCount) =>
            {
                afterEnter?.Invoke(in ctx, installedCount);
                for (var i = 0; i < spec.EnterAfterActionIds.Count; i++)
                {
                    enterAfterAction(in ctx, spec.EnterAfterActionIds[i], installedCount);
                }
            };
        }
 
        private static PhaseStateFeatureBinding<TContext, TFeature>.PhaseContextAction? BuildExit<TContext, TFeature>(
            PhaseStateFeatureSpec spec,
            PhaseStateFeatureBinding<TContext, TFeature>.PhaseContextAction? onExit,
            PhaseActionResolver<TContext>? exitAction)
            where TFeature : class, IPhaseFeature<TContext>
        {
            if (exitAction == null || spec.ExitActionIds.Count == 0)
            {
                return onExit;
            }
 
            return (in TContext ctx) =>
            {
                onExit?.Invoke(in ctx);
                for (var i = 0; i < spec.ExitActionIds.Count; i++)
                {
                    exitAction(in ctx, spec.ExitActionIds[i]);
                }
            };
        }

        private static PhaseStateFeatureBinding<TContext, TFeature>.PhaseEnterCompleteAction? BuildSwitchFlow<TContext, TFeature>(
            PhaseStateFeatureSpec spec,
            PhaseEnterCompleteActionResolver<TContext>? switchFlowAction)
            where TFeature : class, IPhaseFeature<TContext>
        {
            if (switchFlowAction == null || spec.SwitchFlowIds.Count == 0)
            {
                return null;
            }

            return (in TContext ctx, int installedCount) =>
            {
                for (var i = 0; i < spec.SwitchFlowIds.Count; i++)
                {
                    switchFlowAction(in ctx, spec.SwitchFlowIds[i], installedCount);
                }
            };
        }
    }
}
