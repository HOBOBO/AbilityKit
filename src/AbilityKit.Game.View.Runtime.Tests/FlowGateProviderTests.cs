using AbilityKit.Ability.World.DI;
using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Game.View.Runtime.Tests
{
    /// <summary>
    /// Step 3 验证：四个准入 gate 从 GameFlowDomain 硬编码 return true 迁移到 scope 内
    /// <see cref="IFlowGateProvider"/> 服务后，行为契约成立：
    /// - 默认实现四项全 true（零行为变化）；
    /// - BattleWorldModule 把它注册为每局一份的 Scoped 服务；
    /// - 经 MobaFlowConditionContext 合成的 BattleEntryReady 受 gate 取值影响（任一 gate false 即拦截进入）。
    /// </summary>
    public sealed class FlowGateProviderTests
    {
        [Fact]
        public void DefaultProvider_AllGatesTrue()
        {
            var gates = new DefaultFlowGateProvider();

            Assert.True(gates.IsAuthenticated);
            Assert.True(gates.IsRoomReady);
            Assert.True(gates.IsConnectivityReady);
            Assert.True(gates.IsAssetsReady);
        }

        [Fact]
        public void Module_RegistersGateProvider_AsScopedDefault()
        {
            using var container = new WorldContainerBuilder()
                .AddModule(new BattleWorldModule())
                .Build();

            Assert.True(container.IsRegistered(typeof(IFlowGateProvider)));

            using var scope = container.CreateScope();
            var gates = scope.Resolve<IFlowGateProvider>();

            // 默认实现：四项全 true，等价迁移前四个硬编码 return true。
            Assert.True(gates.IsAuthenticated);
            Assert.True(gates.IsRoomReady);
            Assert.True(gates.IsConnectivityReady);
            Assert.True(gates.IsAssetsReady);
        }

        [Fact]
        public void GateProvider_PerScope_SharesSingleInstance()
        {
            using var container = new WorldContainerBuilder()
                .AddModule(new BattleWorldModule())
                .Build();
            using var scope = container.CreateScope();

            var a = scope.Resolve<IFlowGateProvider>();
            var b = scope.Resolve<IFlowGateProvider>();

            Assert.Same(a, b);
        }

        [Fact]
        public void BattleEntryReady_True_WhenAllGatesPassAndBattleRequested()
        {
            var gates = new DefaultFlowGateProvider();
            var ctx = new MobaFlowConditionContext(
                battleRequested: true,
                authenticated: gates.IsAuthenticated,
                roomReady: gates.IsRoomReady,
                connectivityReady: gates.IsConnectivityReady,
                assetsReady: gates.IsAssetsReady);

            Assert.True(ctx.BattleEntryReady);

            var resolver = new MobaFlowConditionResolver();
            Assert.True(resolver.Evaluate(MobaFlowConditionIds.BattleEntryReady, in ctx));
        }

        [Theory]
        [InlineData(false, true, true, true)]
        [InlineData(true, false, true, true)]
        [InlineData(true, true, false, true)]
        [InlineData(true, true, true, false)]
        public void BattleEntryReady_False_WhenAnyGateFails(
            bool authenticated, bool roomReady, bool connectivityReady, bool assetsReady)
        {
            var ctx = new MobaFlowConditionContext(
                battleRequested: true,
                authenticated: authenticated,
                roomReady: roomReady,
                connectivityReady: connectivityReady,
                assetsReady: assetsReady);

            // 任一 gate false，合成的 BattleEntryReady 必须为 false（拦截进入战斗）。
            Assert.False(ctx.BattleEntryReady);

            var resolver = new MobaFlowConditionResolver();
            Assert.False(resolver.Evaluate(MobaFlowConditionIds.BattleEntryReady, in ctx));
        }

        [Fact]
        public void CustomGateProvider_CanFlipIndividualGate()
        {
            // 演示后续接入真实判定：换 scope 内实现即可改变某个 gate，flow 侧读取逻辑不变。
            IFlowGateProvider gates = new StubGateProvider { IsRoomReady = false };

            var ctx = new MobaFlowConditionContext(
                battleRequested: true,
                authenticated: gates.IsAuthenticated,
                roomReady: gates.IsRoomReady,
                connectivityReady: gates.IsConnectivityReady,
                assetsReady: gates.IsAssetsReady);

            Assert.False(ctx.BattleEntryReady);
        }

        [Fact]
        public void ConditionBuilder_WithoutBattleScope_FailsClosed()
        {
            using var host = new BattleWorldScopeHost();
            var builder = CreateConditionBuilder(host, battleRequested: true);

            var ctx = builder.BuildFlowConditionContext();

            Assert.True(ctx.BattleRequested);
            Assert.False(ctx.Authenticated);
            Assert.False(ctx.RoomReady);
            Assert.False(ctx.ConnectivityReady);
            Assert.False(ctx.AssetsReady);
            Assert.False(ctx.BattleEntryReady);
        }

        [Fact]
        public void ConditionBuilder_UsesSeededPerBattleGateProvider()
        {
            using var host = new BattleWorldScopeHost();
            var seeded = new StubGateProvider { IsRoomReady = false };
            host.BeginBattle(seeder => seeder.Seed<IFlowGateProvider>(seeded));
            var builder = CreateConditionBuilder(host, battleRequested: true);

            var ctx = builder.BuildFlowConditionContext();

            Assert.True(ctx.Authenticated);
            Assert.False(ctx.RoomReady);
            Assert.True(ctx.ConnectivityReady);
            Assert.True(ctx.AssetsReady);
            Assert.False(ctx.BattleEntryReady);
            Assert.Same(seeded, host.Resolve<IFlowGateProvider>());
        }

        private static FlowConditionContextBuilder CreateConditionBuilder(
            BattleWorldScopeHost host,
            bool battleRequested)
        {
            return new FlowConditionContextBuilder(
                new FlowConditionContextBuilder.Callbacks
                {
                    GetBattleRequested = () => battleRequested,
                },
                host,
                new MobaFlowConditionResolver());
        }

        private sealed class StubGateProvider : IFlowGateProvider
        {
            public bool IsAuthenticated { get; set; } = true;
            public bool IsRoomReady { get; set; } = true;
            public bool IsConnectivityReady { get; set; } = true;
            public bool IsAssetsReady { get; set; } = true;
        }
    }
}
