using System;

namespace AbilityKit.Game.View.Presentation
{
    public enum ViewRenderBackend
    {
        GameObject = 0,
        Dots = 1,
    }

    public interface IViewRenderBackend<TViewBatch>
        where TViewBatch : struct, IViewBatch
    {
        ViewRenderBackend Backend { get; }
        IViewBinder<TViewBatch> CreateBinder(IViewShellLoader shellLoader);
    }

    public static class ViewRenderBackendFactory<TViewBatch>
        where TViewBatch : struct, IViewBatch
    {
        public static IViewBinder<TViewBatch> CreateBinder(
            IViewShellLoader shellLoader,
            ViewRenderBackend backend,
            Func<IViewShellLoader, IViewBinder<TViewBatch>> gameObjectFactory,
            Func<IViewShellLoader, IViewBinder<TViewBatch>>? dotsFactory = null)
        {
            if (shellLoader == null) throw new ArgumentNullException(nameof(shellLoader));
            if (gameObjectFactory == null) throw new ArgumentNullException(nameof(gameObjectFactory));

            return backend switch
            {
                ViewRenderBackend.GameObject => new GameObjectViewRenderBackend<TViewBatch>(gameObjectFactory).CreateBinder(shellLoader),
                ViewRenderBackend.Dots when dotsFactory != null => new DotsViewRenderBackend<TViewBatch>(dotsFactory).CreateBinder(shellLoader),
                ViewRenderBackend.Dots => throw new InvalidOperationException(
                    "A DOTS view binder factory must be configured before selecting the DOTS backend."),
                _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, "Unsupported view render backend."),
            };
        }
    }

    public sealed class GameObjectViewRenderBackend<TViewBatch> : IViewRenderBackend<TViewBatch>
        where TViewBatch : struct, IViewBatch
    {
        private readonly Func<IViewShellLoader, IViewBinder<TViewBatch>> _binderFactory;

        public GameObjectViewRenderBackend(Func<IViewShellLoader, IViewBinder<TViewBatch>> binderFactory)
        {
            _binderFactory = binderFactory ?? throw new ArgumentNullException(nameof(binderFactory));
        }

        public ViewRenderBackend Backend => ViewRenderBackend.GameObject;

        public IViewBinder<TViewBatch> CreateBinder(IViewShellLoader shellLoader)
        {
            if (shellLoader == null) throw new ArgumentNullException(nameof(shellLoader));
            return _binderFactory(shellLoader)
                ?? throw new InvalidOperationException("The GameObject view binder factory returned null.");
        }
    }

    public sealed class DotsViewRenderBackend<TViewBatch> : IViewRenderBackend<TViewBatch>
        where TViewBatch : struct, IViewBatch
    {
        private readonly Func<IViewShellLoader, IViewBinder<TViewBatch>> _binderFactory;

        public DotsViewRenderBackend(Func<IViewShellLoader, IViewBinder<TViewBatch>> binderFactory)
        {
            _binderFactory = binderFactory ?? throw new ArgumentNullException(nameof(binderFactory));
        }

        public ViewRenderBackend Backend => ViewRenderBackend.Dots;

        public IViewBinder<TViewBatch> CreateBinder(IViewShellLoader shellLoader)
        {
            if (shellLoader == null) throw new ArgumentNullException(nameof(shellLoader));
            return _binderFactory(shellLoader)
                ?? throw new InvalidOperationException("The DOTS view binder factory returned null.");
        }
    }
}
