using AbilityKit.Game.View.Presentation;
using Xunit;

namespace AbilityKit.Game.View.Runtime.Tests;

public sealed class ViewRenderBackendTests
{
    [Fact]
    public void CreateBinder_DotsWithoutFactory_FailsExplicitly()
    {
        var shellLoader = new TestShellLoader();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ViewRenderBackendFactory<TestBatch>.CreateBinder(
                shellLoader,
                ViewRenderBackend.Dots,
                _ => new TestBinder()));

        Assert.Contains("DOTS view binder factory", exception.Message);
    }

    [Fact]
    public void CreateBinder_FactoryReturningNull_FailsExplicitly()
    {
        var shellLoader = new TestShellLoader();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ViewRenderBackendFactory<TestBatch>.CreateBinder(
                shellLoader,
                ViewRenderBackend.GameObject,
                _ => null!));

        Assert.Contains("returned null", exception.Message);
    }

    [Fact]
    public void ViewFeature_DotsWithoutImplementation_DoesNotPartiallyInitialize()
    {
        var feature = new GameObjectOnlyFeature();

        Assert.Throws<NotSupportedException>(() =>
            feature.Initialize(new TestShellLoader(), ViewRenderBackend.Dots));

        Assert.Null(feature.CurrentBinder);
        Assert.Null(feature.CurrentShellLoader);
        Assert.Equal(ViewRenderBackend.GameObject, feature.CurrentBackend);
    }

    [Fact]
    public void ViewFeature_DotsImplementation_IsSelected()
    {
        var feature = new DotsFeature();

        feature.Initialize(new TestShellLoader(), ViewRenderBackend.Dots);

        Assert.Same(feature.DotsBinder, feature.CurrentBinder);
        Assert.Equal(ViewRenderBackend.Dots, feature.CurrentBackend);
    }

    private readonly record struct TestBatch(ulong WorldId, int Frame, ulong Sequence) : IViewBatch;

    private sealed class TestShellLoader : IViewShellLoader
    {
        public object LoadShell(int kindId, int modelId) => new object();

        public void UnloadShell(object shell)
        {
        }
    }

    private sealed class TestBinder : IViewBinder<TestBatch>
    {
        public bool InterpolationEnabled { get; set; }

        public void ApplyBatch(in TestBatch batch)
        {
        }

        public void TickInterpolation(float deltaTime)
        {
        }

        public void RebindAll()
        {
        }

        public void Clear()
        {
        }
    }

    private class GameObjectOnlyFeature : ViewFeature<TestBatch>
    {
        public IViewBinder<TestBatch>? CurrentBinder => Binder;

        public IViewShellLoader? CurrentShellLoader => ShellLoader;

        public ViewRenderBackend CurrentBackend => Backend;

        protected override IViewBinder<TestBatch> CreateBinder(IViewShellLoader shellLoader)
        {
            return new TestBinder();
        }
    }

    private sealed class DotsFeature : GameObjectOnlyFeature
    {
        public TestBinder DotsBinder { get; } = new TestBinder();

        protected override IViewBinder<TestBatch> CreateDotsBinder(IViewShellLoader shellLoader)
        {
            return DotsBinder;
        }
    }
}
