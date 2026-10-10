using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Game.Flow.Battle.ViewEvents.Triggering;
using AbilityKit.Triggering.Eventing;

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

            var triggerSource = handles.Session != null
                ? new BattleSessionTriggerViewEventSource(handles.Session)
                : null;
            var world = handles.RemoteDriven.World;
            if (world == null &&
                handles.Session != null &&
                handles.Session.TryGetWorld(out var sessionWorld))
            {
                world = sessionWorld;
            }
            ctx.BindRuntimeSession(handles.Session, triggerSource, world);

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
            ctx.BindSessionHooks(hooks);
        }

        public static void ClearSession(BattleContext ctx)
        {
            if (ctx == null) return;

            ctx.ClearRuntimeSession();
        }
    }

    internal sealed class BattleSessionTriggerViewEventSource : IBattleTriggerViewEventSource
    {
        private readonly AbilityKit.Game.Battle.BattleLogicSession _session;

        public BattleSessionTriggerViewEventSource(AbilityKit.Game.Battle.BattleLogicSession session)
        {
            _session = session ?? throw new System.ArgumentNullException(nameof(session));
        }

        public System.IDisposable Subscribe(IBattleViewEventSink sink)
        {
            if (sink == null || !_session.TryGetWorld(out var world) ||
                world?.Services == null ||
                !world.Services.TryResolve(out IEventBus bus) || bus == null)
                return null;

            return new BattleTriggerEventViewBridge(bus, sink);
        }
    }
}
