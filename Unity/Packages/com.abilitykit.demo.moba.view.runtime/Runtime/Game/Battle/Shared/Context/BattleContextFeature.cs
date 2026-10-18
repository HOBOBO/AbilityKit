namespace AbilityKit.Game.Flow
{
    public sealed class BattleContextFeature : IGamePhaseFeature
    {
        private BattleContext _ownedContext;
        private BattleContext _publishedContext;

        public void OnAttach(in GamePhaseContext ctx)
        {
            if (!ctx.Features.TryGet(out BattleContext context) || context == null)
            {
                context = BattleContext.Rent();
                _ownedContext = context;
                ctx.Features.Set(context);
            }
            _publishedContext = context;
            ctx.Features.Set<IBattleRuntimeContext>(context);
            ctx.Features.Set<IBattleGameplayPresentationSource>(context);
            ctx.Features.Set<IBattleHudPresentationPort>(context);
            ctx.Features.Set<IBattleEntityContext>(context);
            ctx.Features.Set<IBattleEntityRuntimeBindingPort>(context);
            ctx.Features.Set<IBattleInputContext>(context);
            ctx.Features.Set<IBattleSnapshotRoutingContext>(context);
            ctx.Features.Set<IBattleHudInputSink>(context);
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            var context = _publishedContext;
            ctx.Features.RemoveIfCurrent<IBattleHudInputSink>(context);
            ctx.Features.RemoveIfCurrent<IBattleSnapshotRoutingContext>(context);
            ctx.Features.RemoveIfCurrent<IBattleInputContext>(context);
            ctx.Features.RemoveIfCurrent<IBattleEntityContext>(context);
            ctx.Features.RemoveIfCurrent<IBattleEntityRuntimeBindingPort>(context);
            ctx.Features.RemoveIfCurrent<IBattleRuntimeContext>(context);
            ctx.Features.RemoveIfCurrent<IBattleGameplayPresentationSource>(context);
            ctx.Features.RemoveIfCurrent<IBattleHudPresentationPort>(context);
            if (_ownedContext != null)
            {
                ctx.Features.RemoveIfCurrent(_ownedContext);
                BattleContext.Return(_ownedContext);
                _ownedContext = null;
            }
            _publishedContext = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }
    }
}
