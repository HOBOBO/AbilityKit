namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    internal sealed class BattleViewDirtyEntityRefresher
    {
        private readonly IBattleRuntimeContext _runtimeContext;
        private readonly IBattleEntityContext _entityContext;
        private readonly IBattleEntityQuery _query;
        private readonly BattleViewBinder _binder;
        private readonly ViewDirtyEntityRefreshOperation _operation;

        public BattleViewDirtyEntityRefresher(
            IBattleRuntimeContext runtimeContext,
            IBattleEntityContext entityContext,
            IBattleEntityQuery query,
            BattleViewBinder binder,
            ViewDirtyEntityRefreshOperation operation = null)
        {
            _runtimeContext = runtimeContext;
            _entityContext = entityContext;
            _query = query;
            _binder = binder;
            _operation = operation ?? new ViewDirtyEntityRefreshOperation();
        }

        public void Refresh()
        {
            _operation.Refresh(_runtimeContext, _entityContext, _query, _binder);
        }
    }
}
