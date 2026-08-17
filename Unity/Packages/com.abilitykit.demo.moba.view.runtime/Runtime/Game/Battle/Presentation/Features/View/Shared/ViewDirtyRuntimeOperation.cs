namespace AbilityKit.Game.Flow
{
    internal sealed class ViewDirtyRuntimeOperation
    {
        private readonly ViewSeekableRegistry _seekables;
        private readonly ViewTimelineRuntimeOperation _timeline;
        private readonly ViewDirtyEntityRefreshOperation _dirtyEntities;

        public ViewDirtyRuntimeOperation(
            ViewSeekableRegistry seekables = null,
            ViewTimelineRuntimeOperation timeline = null,
            ViewDirtyEntityRefreshOperation dirtyEntities = null)
        {
            _seekables = seekables ?? new ViewSeekableRegistry();
            _timeline = timeline ?? new ViewTimelineRuntimeOperation();
            _dirtyEntities = dirtyEntities ?? new ViewDirtyEntityRefreshOperation();
        }

        public void Refresh(IViewFeatureRuntime runtime)
        {
            if (runtime == null) return;

            var refreshed = _dirtyEntities.Refresh(
                runtime.RuntimeContext,
                runtime.EntityContext,
                runtime.Query,
                runtime.Binder,
                requireViewComponents: true,
                onSynced: id => _seekables.RegisterForEntity(runtime, id));

            if (refreshed)
            {
                _timeline.SeekAllToCurrentFrame(runtime);
            }
        }
    }
}
