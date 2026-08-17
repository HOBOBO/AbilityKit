using AbilityKit.Game.Flow.Battle.Modules;

namespace AbilityKit.Game.Flow
{
    internal static class SessionContextBinder
    {
        public static void BindRuntimeSession(
            BattleContext ctx,
            BattleSessionState state,
            BattleSessionHandles handles)
        {
            if (ctx == null) return;

            ctx.Session = handles.Session;
            ctx.RuntimeWorld = handles.RemoteDriven.World;
            if (ctx.RuntimeWorld == null &&
                handles.Session != null &&
                handles.Session.TryGetWorld(out var sessionWorld))
            {
                ctx.RuntimeWorld = sessionWorld;
            }

            BindLastFrame(ctx, state);
        }

        public static void BindLastFrame(BattleContext ctx, BattleSessionState state)
        {
            if (ctx == null || state == null) return;

            ctx.LastFrame = state.Tick.LastFrame;
        }

        public static void BindTickProjection(
            BattleContext ctx,
            in BattleSessionTickProjection projection)
        {
            if (ctx == null) return;

            ctx.LastFrame = projection.LastFrame;
            ctx.LogicTimeSeconds = projection.LogicTimeSeconds;
        }

        public static void BindSession(
            BattleContext ctx,
            BattleSessionState state,
            BattleSessionHandles handles,
            BattleSessionHooks hooks,
            BattleStartPlan plan)
        {
            if (ctx == null) return;

            ctx.Plan = plan;
            BindRuntimeSession(ctx, state, handles);
            ctx.Hooks = hooks;
        }

        public static void ClearSession(BattleContext ctx)
        {
            if (ctx == null) return;

            ctx.Session = null;
            ctx.RuntimeWorld = null;
            ctx.Hooks = null;
        }
    }
}
