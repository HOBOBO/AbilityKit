using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Flow;
using AbilityKit.Game.View.Flow;
using Xunit;

namespace AbilityKit.Game.View.Runtime.Tests
{
    /// <summary>
    /// 阶段 7a：资源加载信号解耦测试。
    /// 验证首帧不再驱动 LoadAssets 完成，真实资源加载完成（manifest barrier）由
    /// AssetsLoadCompleted 事件驱动；以及 BattleAssetLoadCoordinator 的成功/失败/取消路径。
    /// </summary>
    public sealed class BattleFlowAssetLoadDecouplingTests
    {
        // --------------------------------------------------------------------
        // MobaBattleAdvanceDecider 解耦验证
        // --------------------------------------------------------------------

        [Fact]
        public void OnFirstFrameReceived_LoadAssets_ReturnsNull()
        {
            var decider = new MobaBattleAdvanceDecider();
            Assert.Null(decider.OnFirstFrameReceived(MobaBattleState.LoadAssets));
        }

        [Fact]
        public void OnAssetsLoadCompleted_LoadAssets_ReturnsAssetsLoadCompleted()
        {
            var decider = new MobaBattleAdvanceDecider();
            Assert.Equal(
                MobaBattleEvent.AssetsLoadCompleted,
                decider.OnAssetsLoadCompleted(MobaBattleState.LoadAssets));
        }

        [Fact]
        public void OnAssetsLoadCompleted_InMatch_ReturnsNull()
        {
            var decider = new MobaBattleAdvanceDecider();
            Assert.Null(decider.OnAssetsLoadCompleted(MobaBattleState.InMatch));
        }

        [Fact]
        public void OnStateEntered_LoadAssets_FirstFrameReceived_ReturnsNull()
        {
            // 阶段 7a：firstFrameReceived 不再推进 LoadAssets
            var decider = new MobaBattleAdvanceDecider();
            Assert.Null(decider.OnStateEntered(
                MobaBattleState.LoadAssets,
                sessionStarted: false,
                worldReady: false,
                firstFrameReceived: true));
        }

        [Fact]
        public void OnStateEntered_LoadAssets_CompletedBarrier_ReturnsAssetsLoadCompleted()
        {
            var decider = new MobaBattleAdvanceDecider();
            Assert.Equal(
                MobaBattleEvent.AssetsLoadCompleted,
                decider.OnStateEntered(
                    MobaBattleState.LoadAssets,
                    sessionStarted: true,
                    worldReady: true,
                    firstFrameReceived: true,
                    assetsLoadCompleted: true));
        }

        // --------------------------------------------------------------------
        // MobaFlowConfiguration 转换验证
        // --------------------------------------------------------------------

        [Fact]
        public void BattleMachine_Contains_AssetsLoadCompleted_LoadAssetsToInMatch()
        {
            var config = MobaFlowConfiguration.CreateDefault();
            var found = config.BattleMachine.Transitions.Any(t =>
                Equals(t.Trigger, MobaBattleEvent.AssetsLoadCompleted) &&
                Equals(t.From, MobaBattleState.LoadAssets) &&
                Equals(t.To, MobaBattleState.InMatch));
            Assert.True(found, "AssetsLoadCompleted: LoadAssets → InMatch 转换缺失");
        }

        // --------------------------------------------------------------------
        // BattleAssetLoadCoordinator 成功 / 失败 / 取消路径
        // --------------------------------------------------------------------

        private static BattleAssetManifest NewManifest()
        {
            return new BattleAssetManifest(
                manifestVersion: 1,
                manifestHash: "hash-1",
                launchGeneration: 1L,
                entries: new[]
                {
                    new BattleAssetEntry("moba/skills", "config:skills", BattleAssetKind.Config)
                });
        }

        [Fact]
        public async Task Coordinator_Success_Path_InvokesOnCompleteWithTrue()
        {
            var loadService = new FakeBattleAssetLoadService(
                () => new BattleAssetLoadResult(
                    success: true,
                    launchGeneration: 1L,
                    manifestVersion: 1,
                    manifestHash: "hash-1",
                    errors: System.Array.Empty<BattleAssetLoadError>(),
                    lease: new BattleAssetLease(1L, new[] { "moba/skills" })));

            var coordinator = new BattleAssetLoadCoordinator(loadService, NewManifest);
            bool? result = null;

            coordinator.StartLoading(b => result = b);
            await loadService.WaitForCompletionAsync();

            Assert.True(result.HasValue);
            Assert.True(result.Value);
            Assert.False(coordinator.IsLoading);
        }

        [Fact]
        public async Task Coordinator_Failure_Path_InvokesOnCompleteWithFalse()
        {
            var loadService = new FakeBattleAssetLoadService(
                () => new BattleAssetLoadResult(
                    success: false,
                    launchGeneration: 1L,
                    manifestVersion: 1,
                    manifestHash: "hash-1",
                    errors: new[] { new BattleAssetLoadError("moba/skills", "config:skills", "AssetNotFound") },
                    lease: null));

            var coordinator = new BattleAssetLoadCoordinator(loadService, NewManifest);
            bool? result = null;

            coordinator.StartLoading(b => result = b);
            await loadService.WaitForCompletionAsync();

            Assert.True(result.HasValue);
            Assert.False(result.Value);
            Assert.False(coordinator.IsLoading);
        }

        [Fact]
        public async Task Coordinator_Cancel_Path_InvokesOnCompleteWithFalse()
        {
            // loadService 永不主动完成，只能靠 Cancel 触发回调
            var loadService = new FakeBattleAssetLoadService(
                () => new BattleAssetLoadResult(
                    success: true,
                    launchGeneration: 1L,
                    manifestVersion: 1,
                    manifestHash: "hash-1",
                    errors: System.Array.Empty<BattleAssetLoadError>(),
                    lease: new BattleAssetLease(1L, new[] { "moba/skills" })),
                neverComplete: true);

            var coordinator = new BattleAssetLoadCoordinator(loadService, NewManifest);
            bool? result = null;

            coordinator.StartLoading(b => result = b);
            Assert.True(coordinator.IsLoading);
            coordinator.Cancel();

            Assert.True(result.HasValue);
            Assert.False(result.Value);
            Assert.False(coordinator.IsLoading);
        }

        /// <summary>
        /// 可控的 IBattleAssetLoadService 替身：按需返回结果，支持永不完成（用于取消测试）。
        /// </summary>
        private sealed class FakeBattleAssetLoadService : IBattleAssetLoadService
        {
            private readonly System.Func<BattleAssetLoadResult> _resultFactory;
            private readonly bool _neverComplete;
            private TaskCompletionSource<BattleAssetLoadResult> _tcs;

            public FakeBattleAssetLoadService(
                System.Func<BattleAssetLoadResult> resultFactory,
                bool neverComplete = false)
            {
                _resultFactory = resultFactory;
                _neverComplete = neverComplete;
            }

            public Task<BattleAssetLoadResult> LoadAsync(
                BattleAssetManifest manifest,
                IProgress<BattleAssetLoadProgress> progress = null,
                CancellationToken cancellationToken = default)
            {
                if (_neverComplete)
                {
                    _tcs = new TaskCompletionSource<BattleAssetLoadResult>();
                    // 注册取消：Coordinator.Cancel() 会取消 token
                    cancellationToken.Register(() =>
                    {
                        _tcs?.TrySetCanceled(cancellationToken);
                    });
                    return _tcs.Task;
                }

                progress?.Report(new BattleAssetLoadProgress(1, 1, string.Empty));
                return Task.FromResult(_resultFactory());
            }

            public Task WaitForCompletionAsync()
            {
                return _neverComplete ? _tcs.Task : Task.CompletedTask;
            }
        }
    }
}
