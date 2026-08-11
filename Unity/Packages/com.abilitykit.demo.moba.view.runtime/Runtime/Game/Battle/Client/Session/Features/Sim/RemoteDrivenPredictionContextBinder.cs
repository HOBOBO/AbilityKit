using System;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Battle.Moba.Config;

namespace AbilityKit.Game.Flow
{
    internal static class RemoteDrivenPredictionContextBinder
    {
        public static void Bind(BattleContext ctx, BattleStartPlan plan, HostRuntime runtime)
        {
            if (ctx == null) return;
            if (!ShouldExposePredictionFeatures(plan)) return;

            ctx.PredictionStats = ResolveFeature<IClientPredictionDriverStats>(runtime);

            if (!plan.Authority.EnableClientPrediction)
            {
                ClearPredictionControls(ctx);
                return;
            }

            ctx.PredictionReconcileTarget = ResolveFeature<IClientPredictionReconcileTarget>(runtime);
            ctx.PredictionReconcileControl = ResolveFeature<IClientPredictionReconcileControl>(runtime);
            ctx.PredictionTuningControl = ResolveFeature<IClientPredictionTuningControl>(runtime);
        }

        private static bool ShouldExposePredictionFeatures(BattleStartPlan plan)
        {
            return plan.HostMode == BattleHostMode.GatewayRemote && plan.Gateway.UseGatewayTransport;
        }

        private static void ClearPredictionControls(BattleContext ctx)
        {
            ctx.PredictionReconcileTarget = null;
            ctx.PredictionReconcileControl = null;
            ctx.PredictionTuningControl = null;
        }

        private static T ResolveFeature<T>(HostRuntime runtime)
            where T : class
        {
            if (runtime == null) return null;

            try
            {
                return runtime.Features.TryGetFeature<T>(out var feature) ? feature : null;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[BattleSessionFeature] TryGetRemoteDrivenFeature failed: {typeof(T).Name}");
                return null;
            }
        }
    }
}
