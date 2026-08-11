using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Flow
{
    internal readonly struct ConfirmedViewSideRuntime
    {
        public readonly BattleContext Context;
        public readonly ConfirmedViewSnapshotRuntime SnapshotRuntime;
        public readonly ConfirmedBattleViewFeature Feature;

        public ConfirmedViewSideRuntime(
            BattleContext context,
            ConfirmedViewSnapshotRuntime snapshotRuntime,
            ConfirmedBattleViewFeature feature)
        {
            Context = context;
            SnapshotRuntime = snapshotRuntime;
            Feature = feature;
        }
    }

    internal static class ConfirmedViewSideRuntimeFactory
    {
        public static ConfirmedViewSideRuntime Create(
            BattleContext sourceCtx,
            WorldId authWorldId,
            System.Action<AbilityKit.World.ECS.IEntity> destroyEntityTree)
        {
            BattleContext ctx = null;
            ConfirmedViewSnapshotRuntime snapshotRuntime = null;
            try
            {
                ctx = ConfirmedViewContextFactory.Create(sourceCtx, authWorldId);
                snapshotRuntime = ConfirmedViewSnapshotRuntime.Create(ctx);
                var feature = new ConfirmedBattleViewFeature(ctx);

                return new ConfirmedViewSideRuntime(ctx, snapshotRuntime, feature);
            }
            catch (Exception creationFailure)
            {
                var cleanupFailures = new List<Exception>();
                try
                {
                    snapshotRuntime?.Dispose();
                }
                catch (Exception cleanupFailure)
                {
                    cleanupFailures.Add(cleanupFailure);
                }

                try
                {
                    ConfirmedViewContextDisposer.Dispose(ctx, destroyEntityTree);
                }
                catch (Exception cleanupFailure)
                {
                    cleanupFailures.Add(cleanupFailure);
                }

                if (cleanupFailures.Count == 0) throw;

                cleanupFailures.Insert(0, creationFailure);
                throw new AggregateException(
                    "Confirmed view resource creation and cleanup both failed.",
                    cleanupFailures);
            }
        }
    }
}
