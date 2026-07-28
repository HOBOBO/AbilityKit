using System;
using AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal static class ConfirmedViewContextDisposer
    {
        public static void Dispose(BattleContext ctx, Action<IEntity> destroyEntityTree)
        {
            if (ctx == null) return;

            SessionSimRuntimeDisposer.ExecuteCleanupSteps(
                "Failed to clean confirmed view context.",
                ctx.ClearSnapshotRouting,
                () => DestroyEntityTree(ctx, destroyEntityTree),
                () => ctx.EntityLookup?.Clear());

            BattleContext.Return(ctx);
        }

        private static void DestroyEntityTree(BattleContext ctx, Action<IEntity> destroyEntityTree)
        {
            if (ctx.EntityNode.IsValid) destroyEntityTree?.Invoke(ctx.EntityNode);
        }
    }
}
