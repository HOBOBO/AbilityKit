namespace AbilityKit.Game.Flow
{
    public sealed class BattleContextFeature : IGamePhaseFeature
    {
        private BattleContext _ownedContext;

        public void OnAttach(in GamePhaseContext ctx)
        {
            if (!ctx.Features.TryGet(out BattleContext context) || context == null)
            {
                context = BattleContext.Rent();
                _ownedContext = context;
                ctx.Features.Set(context);
            }
            ctx.Features.Set<IBattleRuntimeContext>(context);
            ctx.Features.Set<IBattleGameplayPresentationSource>(context);
            ctx.Features.Set<IBattleHudPresentationPort>(context);
            ctx.Features.Set<IBattleEntityContext>(context);
            ctx.Features.Set<IBattleInputContext>(context);
            ctx.Features.Set<IBattleSnapshotRoutingContext>(context);
            ctx.Features.Set<IBattleHudInputSink>(context);
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            ctx.Features.Remove<IBattleHudInputSink>();
            ctx.Features.Remove<IBattleSnapshotRoutingContext>();
            ctx.Features.Remove<IBattleInputContext>();
            ctx.Features.Remove<IBattleEntityContext>();
            ctx.Features.Remove<IBattleRuntimeContext>();
            ctx.Features.Remove<IBattleGameplayPresentationSource>();
            ctx.Features.Remove<IBattleHudPresentationPort>();
            if (_ownedContext != null)
            {
                if (ctx.Features.TryGet(out BattleContext current) &&
                    ReferenceEquals(current, _ownedContext))
                    ctx.Features.Remove<BattleContext>();
                BattleContext.Return(_ownedContext);
                _ownedContext = null;
            }
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }
    }
}
