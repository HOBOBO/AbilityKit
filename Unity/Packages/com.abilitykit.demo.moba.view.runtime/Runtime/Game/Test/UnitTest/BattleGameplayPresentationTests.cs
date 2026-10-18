using System;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Battle.Presentation.Features.Settlement;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Flow;
using AbilityKit.Protocol.Moba;
using NUnit.Framework;
using UnityEngine;
using FrameSnapshotDispatcher = AbilityKit.Core.Snapshots.Routing.FrameSnapshotDispatcher;
using GamePhaseContext = AbilityKit.Game.Flow.GamePhaseContext;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleGameplayPresentationTests
    {
        [Test]
        public void EndedSnapshot_ProjectsResultWithoutLogicWorld()
        {
            var state = NewState();
            state.Apply(new GameplayClockData(1, GameplayClockPhase.Ended, 120000, 30000));
            state.Apply(new GameplayMatchData(1, GameplayClockPhase.Ended, 4, 2, 1, 2700, "score"));

            var summary = BattleEndSummaryProjector.Build(state);

            Assert.IsTrue(state.HasAuthoritativeResult);
            Assert.IsTrue(summary.HasAuthoritativeResult);
            Assert.AreEqual(1, summary.WinningTeamId);
            Assert.AreEqual(4, summary.Team1Score);
            Assert.AreEqual(90, summary.MatchDurationSeconds);
            Assert.AreEqual(2, summary.Players.Count);
            Assert.IsTrue(summary.LocalPlayerVictory);
            Assert.IsTrue(summary.Players.Single(p => p.PlayerId == "p1").IsLocalPlayer);
        }

        [Test]
        public void DrawAndNextBattle_DoNotRetainPreviousResult()
        {
            var state = NewState();
            state.Apply(new GameplayMatchData(1, GameplayClockPhase.Ended, 3, 3, 0, 900, "timeout"));
            var draw = BattleEndSummaryProjector.Build(state);
            Assert.IsTrue(draw.HasAuthoritativeResult);
            Assert.IsFalse(draw.LocalPlayerVictory);
            Assert.AreEqual(0, draw.WinningTeamId);

            state.Reset();
            state.SetLaunch(2, Array.Empty<MobaPlayerLoadout>(), "p3");
            Assert.IsFalse(state.HasEndedMatch);
            Assert.IsFalse(state.HasClock);
            Assert.IsEmpty(state.Players);
            Assert.IsFalse(BattleEndSummaryProjector.Build(state).HasAuthoritativeResult);
        }

        [Test]
        public void ModeResult_CanCompleteBattleWithoutDeathmatchSnapshot()
        {
            var state = new BattleGameplayPresentationState();
            state.SetLaunch(2, Array.Empty<MobaPlayerLoadout>(), "p1", matchId: "match-b");

            Assert.IsFalse(state.TryApplyAuthoritativeResult(2, "match-a", 10));
            Assert.IsTrue(state.TryApplyAuthoritativeResult(2, "match-b", 10));
            Assert.IsTrue(state.HasAuthoritativeResult);
            Assert.IsFalse(state.Deathmatch.HasMatch);

            state.Reset();
            Assert.IsFalse(state.HasAuthoritativeResult);
        }

        [Test]
        public void GameplayHud_ResetAndRebindClearPreviousMatchViews()
        {
            var root = new GameObject("GameplayHudRoot", typeof(RectTransform));
            var module = new TeamDeathmatchHudModule();
            var first = NewState();
            try
            {
                module.Bind((RectTransform)root.transform, first);
                module.SetLocalActorId(12);
                first.Apply(new GameplayClockData(1, GameplayClockPhase.Running, 90000, 30000));
                first.Apply(new GameplayMatchData(1, GameplayClockPhase.Running, 2, 1, 0, 0, null));
                first.Apply(new[] { new GameplayRespawnData(12, 5000) });
                Assert.IsTrue(root.transform.Find("MatchClock").gameObject.activeSelf);
                Assert.IsTrue(root.transform.Find("MatchState").gameObject.activeSelf);
                Assert.IsTrue(root.transform.Find("RespawnClock").gameObject.activeSelf);

                first.Reset();
                Assert.IsFalse(root.transform.Find("MatchClock").gameObject.activeSelf);
                Assert.IsFalse(root.transform.Find("MatchState").gameObject.activeSelf);
                Assert.IsFalse(root.transform.Find("RespawnClock").gameObject.activeSelf);

                var second = NewState();
                module.Bind((RectTransform)root.transform, second);
                first.Apply(new GameplayClockData(1, GameplayClockPhase.Running, 90000, 10000));
                Assert.IsFalse(root.transform.Find("MatchClock").gameObject.activeSelf);
                second.Apply(new GameplayClockData(1, GameplayClockPhase.Running, 90000, 20000));
                Assert.IsTrue(root.transform.Find("MatchClock").gameObject.activeSelf);
            }
            finally
            {
                module.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OtherGameplaySnapshot_IsIgnored()
        {
            var state = NewState();
            state.Apply(new GameplayClockData(2, GameplayClockPhase.Ended, 1000, 0));
            state.Apply(new GameplayMatchData(2, GameplayClockPhase.Ended, 1, 0, 1, 30, "score"));

            Assert.IsFalse(state.HasClock);
            Assert.IsFalse(state.HasMatch);
        }

        [Test]
        public void SameGameplay_NewMatchRejectsOldAndOutOfOrderSnapshots()
        {
            var state = new BattleGameplayPresentationState();
            state.SetLaunch(1, Array.Empty<MobaPlayerLoadout>(), "p1", matchId: "match-b");
            state.Apply(new GameplayMatchData(1, GameplayClockPhase.Ended, 9, 1, 1, 90,
                "score", "match-a", 90));
            state.Apply(new GameplayClockData(1, GameplayClockPhase.Ended, 90000, 0,
                "match-a", 90));
            Assert.IsFalse(state.HasMatch);
            Assert.IsFalse(state.HasClock);

            state.Apply(new GameplayMatchData(1, GameplayClockPhase.Running, 2, 1, 0, 0,
                null, "match-b", 100));
            state.Apply(new GameplayClockData(1, GameplayClockPhase.Running, 90000, 30000,
                "match-b", 100));
            state.Apply(new GameplayMatchData(1, GameplayClockPhase.Ended, 9, 1, 1, 90,
                "score", "match-b", 99));
            state.Apply(new GameplayClockData(1, GameplayClockPhase.Ended, 90000, 0,
                "match-b", 99));

            Assert.AreEqual(GameplayClockPhase.Running, state.Match.Phase);
            Assert.AreEqual(2, state.Match.Team1Score);
            Assert.AreEqual(30000, state.Clock.RemainingMilliseconds);

            state.Apply(new GameplayMatchData(1, GameplayClockPhase.Ended, 2, 1, 1, 101,
                "score", "match-b", 101));
            state.Apply(new GameplayClockData(1, GameplayClockPhase.Running, 90000, 10000,
                "match-b", 100));
            Assert.AreEqual(30000, state.Clock.RemainingMilliseconds);
        }

        [Test]
        public void Reconnect_CurrentEndedSnapshotsRebuildSettlementWithoutLogicWorld()
        {
            var state = NewState();
            state.Reset();
            state.SetLaunch(1, Array.Empty<MobaPlayerLoadout>(), "p1", matchId: "match-b");
            state.Apply(new GameplayMatchData(1, GameplayClockPhase.Ended, 5, 2, 1, 2700,
                "score_limit", "match-b", 2700));
            state.Apply(new GameplayClockData(1, GameplayClockPhase.Ended, 120000, 30000,
                "match-b", 2700));

            var summary = BattleEndSummaryProjector.Build(state);
            Assert.IsTrue(summary.HasAuthoritativeResult);
            Assert.AreEqual(5, summary.Team1Score);
            Assert.AreEqual(90, summary.MatchDurationSeconds);
            Assert.AreEqual("match-b", state.MatchId);
            Assert.AreEqual("match-b", summary.MatchId);
        }

        [Test]
        public void DispatcherReplacement_PreservesSameMatchAndResetsNewMatch()
        {
            var context = BattleContext.Rent();
            var state = new BattleGameplayPresentationState();
            var features = new TestFeatureStore();
            features.Set(context);
            features.Set<IBattleGameplayPresentationSource>(context);
            var phase = new GamePhaseContext(null, features, null, state);
            var feature = new BattleGameplayPresentationFeature();
            var first = CreateGameplayDispatcher();
            var second = CreateGameplayDispatcher();
            var third = CreateGameplayDispatcher();
            try
            {
                context.Plan = CreatePlan("match-a");
                feature.OnAttach(phase);
                var generation = context.BindSnapshotRouting(first, null, null);
                feature.Tick(phase, 0f);
                FeedClock(first, "match-a", 10, 30000);
                Assert.AreEqual(30000, state.Clock.RemainingMilliseconds);

                Assert.IsTrue(context.ClearSnapshotRouting(generation, first));
                feature.Tick(phase, 0f);
                FeedClock(first, "match-a", 11, 20000);
                Assert.AreEqual(30000, state.Clock.RemainingMilliseconds);

                generation = context.BindSnapshotRouting(second, null, null);
                feature.Tick(phase, 0f);
                Assert.AreEqual(30000, state.Clock.RemainingMilliseconds);
                FeedClock(second, "match-a", 12, 19000);
                Assert.AreEqual(19000, state.Clock.RemainingMilliseconds);

                Assert.IsTrue(context.ClearSnapshotRouting(generation, second));
                context.Plan = CreatePlan("match-b");
                generation = context.BindSnapshotRouting(third, null, null);
                feature.Tick(phase, 0f);
                Assert.IsFalse(state.HasClock);
                Assert.AreEqual("match-b", state.MatchId);
                FeedClock(second, "match-a", 13, 10000);
                Assert.IsFalse(state.HasClock);
                FeedClock(third, "match-b", 1, 60000);
                Assert.AreEqual(60000, state.Clock.RemainingMilliseconds);
                context.ClearSnapshotRouting(generation, third);
            }
            finally
            {
                feature.OnDetach(phase);
                BattleContext.Return(context);
                first.Dispose();
                second.Dispose();
                third.Dispose();
            }
        }

        [Test]
        public void GameplayProjection_BindsWithoutFullBattleContext()
        {
            var source = new ProjectionOnlySource(CreatePlan("projection-only").LaunchSpec);
            var state = new BattleGameplayPresentationState();
            var features = new TestFeatureStore();
            features.Set<IBattleGameplayPresentationSource>(source);
            var phase = new GamePhaseContext(null, features, null, state);
            var feature = new BattleGameplayPresentationFeature();
            using var dispatcher = CreateGameplayDispatcher();
            source.Snapshots = dispatcher;
            try
            {
                feature.OnAttach(phase);
                FeedClock(dispatcher, "projection-only", 1, 45000);
                Assert.AreEqual(45000, state.Clock.RemainingMilliseconds);
                Assert.AreEqual("projection-only", state.MatchId);
            }
            finally
            {
                feature.OnDetach(phase);
            }
        }

        private sealed class ProjectionOnlySource : IBattleGameplayPresentationSource
        {
            public ProjectionOnlySource(MobaBattleLaunchSpec launchSpec) { LaunchSpec = launchSpec; }
            public MobaBattleLaunchSpec LaunchSpec { get; }
            public FrameSnapshotDispatcher Snapshots { get; set; }
            public bool TryGetFrameSnapshots(out FrameSnapshotDispatcher snapshots)
            {
                snapshots = Snapshots;
                return snapshots != null;
            }
            public string ResolveLocalControlPlayerId() => "p1";
        }

        private static FrameSnapshotDispatcher CreateGameplayDispatcher()
        {
            var dispatcher = new FrameSnapshotDispatcher();
            GameplayMatchSnapshotRoute.RegisterDecoder(dispatcher);
            GameplayClockSnapshotRoute.RegisterDecoder(dispatcher);
            GameplayRespawnSnapshotRoute.RegisterDecoder(dispatcher);
            return dispatcher;
        }

        private static void FeedClock(FrameSnapshotDispatcher dispatcher, string matchId,
            int frame, int remainingMilliseconds)
        {
            var clock = new GameplayClockData(1, GameplayClockPhase.Running, 90000,
                remainingMilliseconds, matchId, frame);
            dispatcher.Feed(new FramePacket(new WorldId("gameplay-route"), new FrameIndex(frame),
                Array.Empty<PlayerInputCommand>(), new WorldStateSnapshot(
                    GameplayClockSnapshotRoute.OpCode, GameplayClockSnapshotCodec.Encode(in clock))));
        }

        private static AbilityKit.Game.Flow.BattleStartPlan CreatePlan(string matchId)
        {
            var launch = new MobaBattleLaunchSpec(matchId, matchId, "gameplay-route", "battle",
                "client", new PlayerId("p1"), 1, 1, 1, 1, 1, 1, 30, 0,
                default, default, default, Array.Empty<MobaPlayerLoadout>());
            return BattleStartPlanBuilder.ForWorld("gameplay-route", "battle", "client", "p1", 30, 0)
                .WithLaunchSpec(in launch).Build();
        }

        [Test]
        public void Profiles_SelectModulesById()
        {
            var catalog = UnityGameplayPresentationProfileCatalogLoader.Parse(
                "{\"Profiles\":[{" +
                "\"GameplayId\":1,\"HudModuleId\":\"team_deathmatch\",\"ProjectionModuleId\":\"team_deathmatch\",\"SettlementModuleId\":\"team_deathmatch\"}," +
                "{\"GameplayId\":2,\"HudModuleId\":\"arena\",\"ProjectionModuleId\":\"arena\",\"SettlementModuleId\":\"arena\"}]}");
            Assert.AreEqual("arena", catalog.Get(2).HudModuleId);
            Assert.AreEqual("team_deathmatch", catalog.Get(1).SettlementModuleId);

            var registry = new GameplayPresentationModuleRegistry()
                .RegisterHud("arena", () => new TestHudModule())
                .RegisterSettlement("arena", () => new BattleEndSettlementFeature());
            Assert.IsInstanceOf<TestHudModule>(registry.CreateHud(catalog.Get(2).HudModuleId));
            Assert.IsInstanceOf<BattleEndSettlementFeature>(registry.CreateSettlement(catalog.Get(2).SettlementModuleId));
        }

        [Test]
        public void Profiles_RejectUnregisteredModulesDuringComposition()
        {
            var catalog = UnityGameplayPresentationProfileCatalogLoader.Parse(
                "{\"Profiles\":[{\"GameplayId\":2,\"HudModuleId\":\"arena\"," +
                "\"ProjectionModuleId\":\"arena\",\"SettlementModuleId\":\"arena\"}]}");
            var registry = new GameplayPresentationModuleRegistry()
                .RegisterHud("arena", () => new TestHudModule())
                .RegisterSettlement("arena", () => new BattleEndSettlementFeature());

            StringAssert.Contains("projection",
                Assert.Throws<InvalidOperationException>(() => registry.ValidateCatalog(catalog)).Message);
        }

        [Test]
        public void BattleViewModelResolvers_AreRetainedAcrossViewInstances()
        {
            var resolvers = new BattleViewModelResolverRegistry()
                .Register(BattleEntityKind.Turret, (_, code) => code + 1000);
            var session = BattlePresentationSessionContext.CreateDefault(
                modelResolvers: resolvers);
            var first = session.CreateViewResources();
            var second = session.CreateViewResources();
            var meta = new BattleEntityMetaComponent
            {
                Kind = BattleEntityKind.Turret,
                EntityCode = 42,
            };

            Assert.AreSame(resolvers, first.ModelResolvers);
            Assert.AreSame(resolvers, second.ModelResolvers);
            Assert.AreEqual(1042, first.ModelResolvers.ResolveModelId(null, meta));
            Assert.Throws<ArgumentException>(() =>
                resolvers.Register(BattleEntityKind.Turret, (_, __) => 1));
        }

        [Test]
        public void GameplayAssets_AreIncludedInManifest()
        {
            var provider = new ProfileAssetProvider();
            var dependencies = new ResourcesBattleAssetDependencyProvider(provider);
            var source = new GameplayManifestSource(2);
            var manifest = BattleAssetManifestResolver.Resolve(source, dependencies);
            var gameplayAsset = manifest.Entries.Single(e =>
                e.AssetKey == "presentation:gameplay:2:gameplay/arena/hud");
            Assert.AreEqual("gameplay/arena/hud", gameplayAsset.AssetPath);
            Assert.AreEqual(1, manifest.Entries.Count(e => e.AssetKey == gameplayAsset.AssetKey));
        }

        private static BattleGameplayPresentationState NewState()
        {
            var players = new[]
            {
                new MobaPlayerLoadout(new PlayerId("p1"), 1, 1001, 2001, 1, 3001, Array.Empty<int>(), 0),
                new MobaPlayerLoadout(new PlayerId("p2"), 2, 1002, 2002, 1, 3002, Array.Empty<int>(), 1)
            };
            var state = new BattleGameplayPresentationState();
            state.SetLaunch(1, players, "p1");
            return state;
        }

        private sealed class TestHudModule : IGameplayHudModule
        {
            public void Bind(RectTransform root, BattleGameplayPresentationState state) { }
            public void SetLocalActorId(int actorId) { }
            public void Dispose() { }
        }

        private sealed class GameplayManifestSource : IBattleAssetManifestSource, IBattleGameplayManifestSource
        {
            public GameplayManifestSource(int gameplayId) { GameplayId = gameplayId; }
            public int GameplayId { get; }
            public System.Collections.Generic.IReadOnlyList<IBattleAssetManifestPlayer> Players =>
                Array.Empty<IBattleAssetManifestPlayer>();
            public int LaunchManifestVersion => 1;
            public string LaunchManifestHash => "hash";
            public long LaunchGeneration => 1;
        }

        private sealed class ProfileAssetProvider : IAssetProvider
        {
            public T Load<T>(string path) where T : UnityEngine.Object
            {
                string json = null;
                if (path == "luban/moba/models") json = "[]";
                if (path == "vfx/vfx") json = "[]";
                if (path == UnityGameplayPresentationProfileCatalogLoader.ResourcePath)
                    json = "{\"Profiles\":[{\"GameplayId\":2,\"HudModuleId\":\"arena\",\"ProjectionModuleId\":\"arena\",\"SettlementModuleId\":\"arena\",\"AssetPaths\":[\"gameplay/arena/hud\",\"gameplay/arena/hud\"]}]}";
                return json == null ? null : new TextAsset(json) as T;
            }

            public T[] LoadAll<T>(string path) where T : UnityEngine.Object => Array.Empty<T>();
        }

    }
}
