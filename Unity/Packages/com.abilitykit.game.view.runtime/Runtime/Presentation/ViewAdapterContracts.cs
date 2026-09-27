namespace AbilityKit.Game.View.Presentation
{
    public interface IViewShellLoader
    {
        object LoadShell(int kindId, int modelId);
        void UnloadShell(object shell);
    }

    public interface IViewHandle
    {
        uint EntityId { get; }
        object Shell { get; }
        void Despawn();
    }

    public interface IViewBinder<TViewBatch> : IViewSink<TViewBatch>
        where TViewBatch : struct, IViewBatch
    {
        bool InterpolationEnabled { get; set; }
        void TickInterpolation(float deltaTime);
        void RebindAll();
    }

    public abstract class ViewFeature<TViewBatch>
        where TViewBatch : struct, IViewBatch
    {
        protected IViewBinder<TViewBatch>? Binder { get; private set; }
        protected IViewShellLoader? ShellLoader { get; private set; }
        protected ViewRenderBackend Backend { get; private set; }

        public virtual void Initialize(IViewShellLoader shellLoader)
        {
            Initialize(shellLoader, ViewRenderBackend.GameObject);
        }

        public virtual void Initialize(
            IViewShellLoader shellLoader,
            ViewRenderBackend backend)
        {
            if (shellLoader == null) throw new System.ArgumentNullException(nameof(shellLoader));

            var binder = ViewRenderBackendFactory<TViewBatch>.CreateBinder(
                shellLoader,
                backend,
                CreateBinder,
                CreateDotsBinder);

            ShellLoader = shellLoader;
            Backend = backend;
            Binder = binder;
        }

        protected abstract IViewBinder<TViewBatch> CreateBinder(IViewShellLoader shellLoader);

        protected virtual IViewBinder<TViewBatch> CreateDotsBinder(IViewShellLoader shellLoader)
        {
            throw new System.NotSupportedException(
                GetType().FullName + " does not provide a DOTS view binder.");
        }

        public virtual void Tick(float deltaTime)
        {
            Binder?.TickInterpolation(deltaTime);
        }

        public virtual void Shutdown()
        {
            Binder?.Clear();
            Binder = null;
            ShellLoader = null;
            Backend = ViewRenderBackend.GameObject;
        }
    }
}
