namespace AbilityKit.Game.Flow
{
    internal sealed class BattlePresentationSessionResolver
    {
        private readonly IBattlePresentationSessionFactory _factory;

        public BattlePresentationSessionResolver(IBattlePresentationSessionFactory factory = null)
        {
            _factory = factory ?? new BattlePresentationSessionFactory();
        }

        public BattlePresentationSessionContext Resolve(in GamePhaseContext ctx)
        {
            if (ctx.Features.TryGet(out BattlePresentationSessionContext existing) && existing != null)
            {
                existing.Retain();
                return existing;
            }

            var created = _factory.Create(ctx) ?? new BattlePresentationSessionFactory().Create(ctx);
            created.Retain();
            ctx.Features.Set(created);

            return created;
        }

        public void Release(in GamePhaseContext ctx, BattlePresentationSessionContext session)
        {
            if (session == null) return;
            if (!session.Release()) return;
            if (!ctx.Features.TryGet(out BattlePresentationSessionContext existing)) return;
            if (!ReferenceEquals(existing, session)) return;

            ctx.Features.Remove<BattlePresentationSessionContext>();
        }
    }
}
