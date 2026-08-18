using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Game.View.Runtime.Tests
{
    // 注：MobaBattleState / MobaBattleEvent 为 internal（镜像编译进本程序集可访问），
    // 但 xUnit 的 [Theory] 方法须为 public，public 方法签名不能暴露 internal 类型（CS0051）。
    // 故 InlineData 用底层 int，方法体内转回枚举——保持产品代码可见性不变。
    public sealed class MobaBattleAdvanceDeciderTests
    {
        private readonly MobaBattleAdvanceDecider _decider = new MobaBattleAdvanceDecider();

        [Theory]
        [InlineData((int)MobaBattleState.Prepare, (int)MobaBattleEvent.PrepareDone)]
        [InlineData((int)MobaBattleState.Connect, (int)MobaBattleEvent.Connected)]
        public void OnSessionStarted_Advances(int current, int expected)
        {
            Assert.Equal((MobaBattleEvent)expected, _decider.OnSessionStarted((MobaBattleState)current));
        }

        [Theory]
        [InlineData((int)MobaBattleState.CreateOrJoinWorld)]
        [InlineData((int)MobaBattleState.LoadAssets)]
        [InlineData((int)MobaBattleState.InMatch)]
        [InlineData((int)MobaBattleState.End)]
        public void OnSessionStarted_NoAdvance(int current)
        {
            Assert.Null(_decider.OnSessionStarted((MobaBattleState)current));
        }

        [Theory]
        [InlineData((int)MobaBattleState.Prepare, (int)MobaBattleEvent.PrepareDone)]
        [InlineData((int)MobaBattleState.Connect, (int)MobaBattleEvent.Connected)]
        [InlineData((int)MobaBattleState.CreateOrJoinWorld, (int)MobaBattleEvent.JoinedWorld)]
        public void OnFirstFrameReceived_Advances(int current, int expected)
        {
            Assert.Equal((MobaBattleEvent)expected, _decider.OnFirstFrameReceived((MobaBattleState)current));
        }

        [Theory]
        // 阶段 7a：LoadAssets 不再因首帧推进（真实资源加载完成由 OnAssetsLoadCompleted 驱动）
        [InlineData((int)MobaBattleState.LoadAssets)]
        [InlineData((int)MobaBattleState.InMatch)]
        [InlineData((int)MobaBattleState.End)]
        public void OnFirstFrameReceived_NoAdvance(int current)
        {
            Assert.Null(_decider.OnFirstFrameReceived((MobaBattleState)current));
        }

        [Fact]
        public void OnAssetsLoadCompleted_Advances_WhenLoadAssets()
        {
            Assert.Equal(
                MobaBattleEvent.AssetsLoadCompleted,
                _decider.OnAssetsLoadCompleted(MobaBattleState.LoadAssets));
        }

        [Theory]
        [InlineData((int)MobaBattleState.Prepare)]
        [InlineData((int)MobaBattleState.Connect)]
        [InlineData((int)MobaBattleState.CreateOrJoinWorld)]
        [InlineData((int)MobaBattleState.InMatch)]
        [InlineData((int)MobaBattleState.End)]
        public void OnAssetsLoadCompleted_NoAdvance_WhenNotLoadAssets(int current)
        {
            Assert.Null(_decider.OnAssetsLoadCompleted((MobaBattleState)current));
        }

        [Theory]
        [InlineData((int)MobaBattleState.Prepare)]
        [InlineData((int)MobaBattleState.Connect)]
        [InlineData((int)MobaBattleState.CreateOrJoinWorld)]
        [InlineData((int)MobaBattleState.LoadAssets)]
        [InlineData((int)MobaBattleState.InMatch)]
        public void OnSessionFailed_AdvancesToEnded_WhenNotEnd(int current)
        {
            Assert.Equal(MobaBattleEvent.Ended, _decider.OnSessionFailed((MobaBattleState)current));
        }

        [Fact]
        public void OnSessionFailed_NoAdvance_WhenAlreadyEnd()
        {
            Assert.Null(_decider.OnSessionFailed(MobaBattleState.End));
        }

        [Theory]
        // Connect：SessionStarted 或 FirstFrameReceived 任一为真即推进
        [InlineData((int)MobaBattleState.Connect, true, false, false, (int)MobaBattleEvent.Connected)]
        [InlineData((int)MobaBattleState.Connect, false, false, true, (int)MobaBattleEvent.Connected)]
        [InlineData((int)MobaBattleState.Connect, true, false, true, (int)MobaBattleEvent.Connected)]
        // CreateOrJoinWorld：仅看 WorldReady（sessionStarted/firstFrameReceived 不参与）
        [InlineData((int)MobaBattleState.CreateOrJoinWorld, false, true, false, (int)MobaBattleEvent.JoinedWorld)]
        [InlineData((int)MobaBattleState.CreateOrJoinWorld, true, true, true, (int)MobaBattleEvent.JoinedWorld)]
        public void OnStateEntered_Advances(int current, bool sessionStarted, bool worldReady, bool firstFrameReceived, int expected)
        {
            Assert.Equal(
                (MobaBattleEvent)expected,
                _decider.OnStateEntered((MobaBattleState)current, sessionStarted, worldReady, firstFrameReceived));
        }

        [Fact]
        public void OnStateEntered_LoadAssets_AdvancesWhenCompletionArrivedEarly()
        {
            Assert.Equal(
                MobaBattleEvent.AssetsLoadCompleted,
                _decider.OnStateEntered(
                    MobaBattleState.LoadAssets,
                    sessionStarted: true,
                    worldReady: true,
                    firstFrameReceived: true,
                    assetsLoadCompleted: true));
        }

        [Theory]
        // 标志均未满足
        [InlineData((int)MobaBattleState.Connect, false, false, false)]
        [InlineData((int)MobaBattleState.CreateOrJoinWorld, false, false, false)]
        // CreateOrJoinWorld 不看 SessionStarted / FirstFrameReceived
        [InlineData((int)MobaBattleState.CreateOrJoinWorld, true, false, true)]
        // 阶段 7a：LoadAssets 不再因 firstFrameReceived 自动推进（真实资源加载完成由 OnAssetsLoadCompleted 驱动）
        [InlineData((int)MobaBattleState.LoadAssets, false, false, false)]
        [InlineData((int)MobaBattleState.LoadAssets, true, false, false)]
        [InlineData((int)MobaBattleState.LoadAssets, false, false, true)]
        [InlineData((int)MobaBattleState.LoadAssets, true, false, true)]
        // 无补判规则的状态
        [InlineData((int)MobaBattleState.Prepare, true, true, true)]
        [InlineData((int)MobaBattleState.InMatch, true, true, true)]
        [InlineData((int)MobaBattleState.End, true, true, true)]
        public void OnStateEntered_NoAdvance(int current, bool sessionStarted, bool worldReady, bool firstFrameReceived)
        {
            Assert.Null(_decider.OnStateEntered((MobaBattleState)current, sessionStarted, worldReady, firstFrameReceived));
        }
    }
}
