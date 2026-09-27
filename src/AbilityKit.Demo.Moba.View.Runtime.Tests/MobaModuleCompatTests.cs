using System.Collections.Generic;
using AbilityKit.Game.Flow.Modules;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class MobaModuleCompatTests
{
    [Fact]
    public void LegacyMobaModuleHost_DelegatesToGameViewModuleHost()
    {
        var events = new List<string>();
        var view = new CompatModule("view", events, "session");
        var session = new CompatModule("session", events);
        var host = new ModuleHost<TestContext, CompatModule>(new List<CompatModule> { view, session }, message => events.Add("fail:" + message));
        var ctx = new TestContext(5);

        var sorted = host.TrySortByDependencies();
        host.Attach(in ctx);
        host.Tick(in ctx, 0.1f);
        host.RebindAll(in ctx);
        host.Detach(in ctx);

        Assert.True(sorted);
        Assert.Collection(
            host.Modules,
            m => Assert.Equal("session", m.Id),
            m => Assert.Equal("view", m.Id));
        Assert.Equal(
            new[]
            {
                "attach:session:5",
                "attach:view:5",
                "tick:session:0.10",
                "tick:view:0.10",
                "rebind:session",
                "rebind:view",
                "detach:view:5",
                "detach:session:5"
            },
            events);
    }

    [Fact]
    public void LegacyMobaModuleHost_ValidatesSingleModuleDependencies()
    {
        var failures = new List<string>();
        var events = new List<string>();
        var host = new ModuleHost<TestContext, CompatModule>(
            new List<CompatModule> { new CompatModule("view", events, "missing") },
            failures.Add);

        var sorted = host.TrySortByDependencies();

        Assert.False(sorted);
        Assert.Contains(failures, message => message.Contains("missing module 'missing'"));
    }

    private readonly record struct TestContext(int Value);

    private sealed class CompatModule : IGameModule<TestContext>, IGameModuleTick<TestContext>, IGameModuleRebind<TestContext>, IGameModuleId, IGameModuleDependencies
    {
        private readonly List<string> _events;
        private readonly string[] _dependencies;

        public CompatModule(string id, List<string> events, params string[] dependencies)
        {
            Id = id;
            _events = events;
            _dependencies = dependencies;
        }

        public string Id { get; }
        public IEnumerable<string> Dependencies => _dependencies;

        public void OnAttach(in TestContext ctx)
        {
            _events.Add($"attach:{Id}:{ctx.Value}");
        }

        public void OnDetach(in TestContext ctx)
        {
            _events.Add($"detach:{Id}:{ctx.Value}");
        }

        public void Tick(in TestContext ctx, float deltaTime)
        {
            _events.Add($"tick:{Id}:{deltaTime:0.00}");
        }

        public void RebindAll(in TestContext ctx)
        {
            _events.Add($"rebind:{Id}");
        }
    }
}
