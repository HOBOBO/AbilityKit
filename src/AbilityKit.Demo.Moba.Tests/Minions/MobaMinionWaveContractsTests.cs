using System;
using System.Collections.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Console.Bootstrap;
using AbilityKit.Demo.Moba.Minions;
using AbilityKit.Demo.Moba.Share.Config;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Minions
{
    public sealed class MobaMinionWaveContractsTests
    {
        [Fact]
        public void SchedulerCommitsImmediateThenThirtySecondWavesInOrder()
        {
            var schedule = new MobaMinionWaveSchedule(
                firstWaveAtMs: 0,
                intervalMs: 30_000,
                composition: new[]
                {
                    new MobaMinionCompositionEntry(MobaMinionArchetype.Melee, 3, 250),
                    new MobaMinionCompositionEntry(MobaMinionArchetype.Ranged, 3, 250),
                });
            var scheduler = new MobaMinionWaveScheduler(in schedule);
            scheduler.Reset(7);

            Assert.True(scheduler.TryCommitDueWave(0, out var first));
            Assert.Equal(0, first.WaveOrdinal);
            Assert.Equal(7L << 32, first.WaveId);
            Assert.False(scheduler.TryCommitDueWave(29_999, out _));
            Assert.True(scheduler.TryCommitDueWave(30_000, out var second));
            Assert.Equal(1, second.WaveOrdinal);
        }

        [Fact]
        public void SchedulerDoesNotSkipDueWaveWhenTickIsLate()
        {
            var schedule = new MobaMinionWaveSchedule(
                firstWaveAtMs: 0,
                intervalMs: 30_000,
                composition: new[] { new MobaMinionCompositionEntry(MobaMinionArchetype.Melee, 1, 0) });
            var scheduler = new MobaMinionWaveScheduler(in schedule);
            scheduler.Reset(1);

            Assert.True(scheduler.TryCommitDueWave(90_000, out var first));
            Assert.Equal(0, first.WaveOrdinal);
            Assert.True(scheduler.TryCommitDueWave(90_000, out var second));
            Assert.Equal(1, second.WaveOrdinal);
        }

        [Fact]
        public void SchedulerPreviewDoesNotAdvanceUntilWaveIsCommitted()
        {
            var schedule = new MobaMinionWaveSchedule(
                firstWaveAtMs: 0,
                intervalMs: 30_000,
                composition: new[] { new MobaMinionCompositionEntry(MobaMinionArchetype.Melee, 1, 0) });
            var scheduler = new MobaMinionWaveScheduler(in schedule);
            scheduler.Reset(4);

            Assert.True(scheduler.TryPeekDueWave(0, out var firstPreview));
            Assert.True(scheduler.TryPeekDueWave(0, out var repeatedPreview));
            Assert.Equal(firstPreview.WaveId, repeatedPreview.WaveId);
            Assert.Equal(0, scheduler.NextWaveOrdinal);
            Assert.True(scheduler.CommitWave(in firstPreview));
            Assert.False(scheduler.CommitWave(in repeatedPreview));
            Assert.Equal(1, scheduler.NextWaveOrdinal);
        }

        [Fact]
        public void CapacityPolicyCountsActiveAndPendingMinionsBeforeAdmittingWholeWave()
        {
            var composition = new[]
            {
                new MobaMinionCompositionEntry(MobaMinionArchetype.Melee, 3, 250),
                new MobaMinionCompositionEntry(MobaMinionArchetype.Ranged, 3, 250),
            };

            Assert.True(MobaMinionWaveCapacityPolicy.CanCommitWave(12, 0, 0, composition));
            Assert.False(MobaMinionWaveCapacityPolicy.CanCommitWave(12, 1, 6, composition));
            Assert.True(MobaMinionWaveCapacityPolicy.CanCommitWave(12, 0, 0, composition));
            Assert.False(MobaMinionWaveCapacityPolicy.CanCommitWave(5, 0, 0, composition));
        }

        [Fact]
        public void BaselineWaveCompositionHasThreeMeleeAndThreeRangedSlots()
        {
            var schedule = new MobaMinionWaveSchedule(
                firstWaveAtMs: 0,
                intervalMs: 30_000,
                composition: new[]
                {
                    new MobaMinionCompositionEntry(MobaMinionArchetype.Melee, 3, 250),
                    new MobaMinionCompositionEntry(MobaMinionArchetype.Ranged, 3, 250),
                });
            var scheduler = new MobaMinionWaveScheduler(in schedule);
            scheduler.Reset(7);

            Assert.True(scheduler.TryCommitDueWave(0, out var wave));
            Assert.Equal(6, wave.Composition[0].Count + wave.Composition[1].Count);
            Assert.Equal(MobaMinionArchetype.Melee, wave.Composition[0].Archetype);
            Assert.Equal(MobaMinionArchetype.Ranged, wave.Composition[1].Archetype);
            Assert.Equal(250, wave.Composition[0].SpawnIntervalMs);
        }

        [Fact]
        public void RouteCursorAdvancesOnlyWithinAcceptanceRadius()
        {
            var cursor = new MobaLaneRouteCursor(
                new List<Vec3> { new Vec3(0, 0, 0), new Vec3(10, 0, 0), new Vec3(20, 0, 0) },
                acceptanceRadius: 0.5f);

            Assert.False(cursor.AdvanceIfReached(new Vec3(1, 0, 0)));
            Assert.Equal(0, cursor.WaypointIndex);
            Assert.True(cursor.AdvanceIfReached(new Vec3(0.25f, 0, 0)));
            Assert.Equal(1, cursor.WaypointIndex);
            Assert.False(cursor.IsTerminal);
        }

        [Fact]
        public void RouteCursorBecomesTerminalOnlyAfterReachingFinalWaypoint()
        {
            var cursor = new MobaLaneRouteCursor(
                new List<Vec3> { new Vec3(0, 0, 0), new Vec3(10, 0, 0), new Vec3(20, 0, 0) },
                acceptanceRadius: 0.5f);

            Assert.True(cursor.AdvanceIfReached(new Vec3(0, 0, 0)));
            Assert.Equal(1, cursor.WaypointIndex);
            Assert.False(cursor.IsTerminal);

            Assert.True(cursor.AdvanceIfReached(new Vec3(10, 0, 0)));
            Assert.Equal(2, cursor.WaypointIndex);
            Assert.False(cursor.IsTerminal);

            Assert.False(cursor.AdvanceIfReached(new Vec3(19, 0, 0)));
            Assert.False(cursor.IsTerminal);
            Assert.True(cursor.AdvanceIfReached(new Vec3(20, 0, 0)));
            Assert.True(cursor.IsTerminal);
        }

        [Fact]
        public void RouteCursorReportsDeterministicAlongRouteProgress()
        {
            var cursor = new MobaLaneRouteCursor(
                new List<Vec3>
                {
                    new Vec3(0, 0, 0),
                    new Vec3(3, 0, 0),
                    new Vec3(3, 0, 4),
                },
                acceptanceRadius: 0.5f);

            Assert.Equal(1.5f, cursor.GetProgress(new Vec3(1.5f, 0, 0)), 3);
            Assert.True(cursor.AdvanceIfReached(new Vec3(0, 0, 0)));
            Assert.True(cursor.AdvanceIfReached(new Vec3(3, 0, 0)));
            Assert.Equal(5f, cursor.GetProgress(new Vec3(3, 0, 2)), 3);
        }

        [Theory]
        [InlineData(10, 2f, 11, 3.5f, 1.5f, true)]
        [InlineData(10, 2f, 11, 3.5002f, 1.5f, false)]
        [InlineData(10, 2f, 11, 2f, 1.5f, false)]
        [InlineData(11, 2f, 10, 2f, 1.5f, true)]
        [InlineData(10, 2f, 11, 1.9f, 1.5f, false)]
        public void LaneSpacingPolicyUsesGapAndActorIdTieBreak(
            int actorId,
            float progress,
            int candidateActorId,
            float candidateProgress,
            float safeSpacing,
            bool expected)
        {
            Assert.Equal(expected, MobaMinionLaneSpacingPolicy.IsAheadWithinSafeSpacing(
                actorId, progress, candidateActorId, candidateProgress, safeSpacing));
        }

        [Fact]
        public void StallTrackerReportsOncePerContinuousBlockAndCanReportAgainAfterRelease()
        {
            var tracker = new MobaMinionStallTracker();

            Assert.False(tracker.Update(true, 100L, 1_000L, out var blockedForMs));
            Assert.Equal(0L, blockedForMs);
            Assert.False(tracker.Update(true, 1_099L, 1_000L, out blockedForMs));
            Assert.Equal(999L, blockedForMs);
            Assert.True(tracker.Update(true, 1_100L, 1_000L, out blockedForMs));
            Assert.Equal(1_000L, blockedForMs);
            Assert.False(tracker.Update(true, 2_000L, 1_000L, out _));

            Assert.False(tracker.Update(false, 2_001L, 1_000L, out _));
            Assert.False(tracker.Update(true, 3_000L, 1_000L, out blockedForMs));
            Assert.Equal(0L, blockedForMs);
            Assert.True(tracker.Update(true, 4_000L, 1_000L, out blockedForMs));
            Assert.Equal(1_000L, blockedForMs);
        }

        [Fact]
        public void RouteResourceLoaderValidatesAndSortsVersionedRoutes()
        {
            const string json = "{\"Version\":1,\"Routes\":[{\"RouteId\":2,\"AcceptanceRadius\":0.5,\"TerminalPolicy\":\"BaseDamageEvent\",\"Waypoints\":[{\"X\":0,\"Y\":0,\"Z\":0},{\"X\":1,\"Y\":0,\"Z\":0}]},{\"RouteId\":1,\"AcceptanceRadius\":0.5,\"TerminalPolicy\":\"Despawn\",\"Waypoints\":[{\"X\":0,\"Y\":0,\"Z\":0},{\"X\":0,\"Y\":0,\"Z\":1}]}]}";

            var routes = MobaMinionRouteResourceLoader.Load(json);

            Assert.Equal(2, routes.Count);
            Assert.Equal(1, routes[0].RouteId);
            Assert.Equal(MobaMinionTerminalPolicy.BaseDamageEvent, routes[1].TerminalPolicy);
        }

        [Fact]
        public void RouteResourceLoaderKeepsExplicitMapIdentityForCurrentVersion()
        {
            const string json = "{\"Version\":2,\"MapId\":7,\"Routes\":[{\"RouteId\":1,\"AcceptanceRadius\":0.5,\"TerminalPolicy\":\"BaseDamageEvent\",\"Waypoints\":[{\"X\":0,\"Y\":0,\"Z\":0},{\"X\":1,\"Y\":0,\"Z\":0}]}]}";

            var resource = MobaMinionRouteResourceLoader.LoadResource(json);

            Assert.Equal(2, resource.Version);
            Assert.Equal(7, resource.MapId);
            Assert.Equal(1, resource.DefaultRouteId);
            Assert.False(resource.IsLegacy);
            Assert.Single(resource.Routes);
        }

        [Fact]
        public void RouteResourceLoaderReadsVersionThreeDefaultRouteAndWaveSchedule()
        {
            const string json = "{\"Version\":3,\"MapId\":7,\"DefaultRouteId\":4,\"Schedule\":{\"FirstWaveAtMs\":1000,\"IntervalMs\":45000,\"MaxWaves\":5,\"Composition\":[{\"Archetype\":\"Ranged\",\"Count\":2,\"SpawnIntervalMs\":300}]},\"Routes\":[{\"RouteId\":4,\"AcceptanceRadius\":0.75,\"TerminalPolicy\":\"Despawn\",\"Waypoints\":[{\"X\":0,\"Y\":0,\"Z\":0},{\"X\":1,\"Y\":0,\"Z\":0}]}]}";

            var resource = MobaMinionRouteResourceLoader.LoadResource(json);

            Assert.Equal(4, resource.DefaultRouteId);
            Assert.Equal(1000, resource.Schedule.FirstWaveAtMs);
            Assert.Equal(45000, resource.Schedule.IntervalMs);
            Assert.Equal(5, resource.Schedule.MaxWaves);
            var composition = Assert.Single(resource.Schedule.Composition);
            Assert.Equal(MobaMinionArchetype.Ranged, composition.Archetype);
            Assert.Equal(2, composition.Count);
            Assert.Equal(300, composition.SpawnIntervalMs);
        }

        [Fact]
        public void RouteResourceLoaderReadsVersionFourPerTeamCapacity()
        {
            const string json = "{\"Version\":4,\"MapId\":7,\"DefaultRouteId\":1,\"Schedule\":{\"FirstWaveAtMs\":0,\"IntervalMs\":30000,\"MaxActiveMinionsPerTeam\":18,\"Composition\":[{\"Archetype\":\"Melee\",\"Count\":1,\"SpawnIntervalMs\":0}]},\"Routes\":[{\"RouteId\":1,\"AcceptanceRadius\":0.5,\"TerminalPolicy\":\"Despawn\",\"Waypoints\":[{\"X\":0,\"Y\":0,\"Z\":0},{\"X\":1,\"Y\":0,\"Z\":0}]}]}";

            var resource = MobaMinionRouteResourceLoader.LoadResource(json);

            Assert.Equal(4, resource.Version);
            Assert.Equal(18, resource.Schedule.MaxActiveMinionsPerTeam);
        }

        [Fact]
        public void RouteResourceLoaderRequiresCapacityForVersionFour()
        {
            const string json = "{\"Version\":4,\"MapId\":7,\"DefaultRouteId\":1,\"Schedule\":{\"FirstWaveAtMs\":0,\"IntervalMs\":30000,\"Composition\":[{\"Archetype\":\"Melee\",\"Count\":1,\"SpawnIntervalMs\":0}]},\"Routes\":[{\"RouteId\":1,\"AcceptanceRadius\":0.5,\"TerminalPolicy\":\"Despawn\",\"Waypoints\":[{\"X\":0,\"Y\":0,\"Z\":0},{\"X\":1,\"Y\":0,\"Z\":0}]}]}";

            var error = Assert.Throws<InvalidOperationException>(
                () => MobaMinionRouteResourceLoader.LoadResource(json));

            Assert.Contains("MaxActiveMinionsPerTeam", error.Message);
        }

        [Fact]
        public void RouteResourceLoaderRejectsVersionThreeWithoutScheduleOrDefaultRoute()
        {
            const string missingSchedule = "{\"Version\":3,\"MapId\":7,\"DefaultRouteId\":1,\"Routes\":[{\"RouteId\":1,\"AcceptanceRadius\":0.5,\"TerminalPolicy\":\"Despawn\",\"Waypoints\":[{\"X\":0,\"Y\":0,\"Z\":0},{\"X\":1,\"Y\":0,\"Z\":0}]}]}";
            const string missingDefaultRoute = "{\"Version\":3,\"MapId\":7,\"Schedule\":{\"FirstWaveAtMs\":0,\"IntervalMs\":30000,\"Composition\":[{\"Archetype\":\"Melee\",\"Count\":1,\"SpawnIntervalMs\":0}]},\"Routes\":[{\"RouteId\":1,\"AcceptanceRadius\":0.5,\"TerminalPolicy\":\"Despawn\",\"Waypoints\":[{\"X\":0,\"Y\":0,\"Z\":0},{\"X\":1,\"Y\":0,\"Z\":0}]}]}";

            Assert.Contains("WaveSchedule", Assert.Throws<InvalidOperationException>(
                () => MobaMinionRouteResourceLoader.LoadResource(missingSchedule)).Message);
            Assert.Contains("DefaultRouteId", Assert.Throws<InvalidOperationException>(
                () => MobaMinionRouteResourceLoader.LoadResource(missingDefaultRoute)).Message);
        }

        [Fact]
        public void RouteResourceLoaderKeepsLegacyVersionBoundToDefaultMap()
        {
            const string json = "{\"Version\":1,\"Routes\":[{\"RouteId\":1,\"AcceptanceRadius\":0.5,\"TerminalPolicy\":\"Despawn\",\"Waypoints\":[{\"X\":0,\"Y\":0,\"Z\":0},{\"X\":1,\"Y\":0,\"Z\":0}]}]}";

            var resource = MobaMinionRouteResourceLoader.LoadResource(json);

            Assert.Equal(1, resource.MapId);
            Assert.True(resource.IsLegacy);
        }

        [Fact]
        public void RouteResourceLoaderRequiresMapIdentityForVersionTwo()
        {
            const string json = "{\"Version\":2,\"Routes\":[{\"RouteId\":1,\"AcceptanceRadius\":0.5,\"TerminalPolicy\":\"Despawn\",\"Waypoints\":[{\"X\":0,\"Y\":0,\"Z\":0},{\"X\":1,\"Y\":0,\"Z\":0}]}]}";

            var error = Assert.Throws<InvalidOperationException>(() => MobaMinionRouteResourceLoader.LoadResource(json));

            Assert.Contains("MapId", error.Message);
        }

        [Theory]
        [InlineData("Despawn", MobaMinionTerminalPolicy.Despawn)]
        [InlineData("BaseDamageEvent", MobaMinionTerminalPolicy.BaseDamageEvent)]
        [InlineData("Hold", MobaMinionTerminalPolicy.Hold)]
        public void RouteResourceLoaderPreservesTerminalPolicy(
            string policy,
            MobaMinionTerminalPolicy expected)
        {
            var json = $"{{\"Version\":1,\"Routes\":[{{\"RouteId\":1,\"AcceptanceRadius\":0.5,\"TerminalPolicy\":\"{policy}\",\"Waypoints\":[{{\"X\":0,\"Y\":0,\"Z\":0}},{{\"X\":1,\"Y\":0,\"Z\":0}}]}}]}}";

            var route = Assert.Single(MobaMinionRouteResourceLoader.Load(json));

            Assert.Equal(expected, route.TerminalPolicy);
        }

        [Fact]
        public void BaseDamageEventIdentifiesEnemyBaseAndSimulationFrame()
        {
            var payload = new MobaMinionBaseDamageEvent(
                waveId: (7L << 32) | 1,
                actorId: 42,
                sourceTeamId: 1,
                targetTeamId: 2,
                damage: 1,
                frame: 128);

            Assert.Equal(1, payload.SourceTeamId);
            Assert.Equal(2, payload.TargetTeamId);
            Assert.Equal(1, payload.TeamId);
            Assert.Equal(128, payload.Frame);
        }

        [Fact]
        public void PublishedMinionCharactersResolveDistinctAttributesAndModels()
        {
            var assets = new ConsoleTextAssetLoader();
            var database = new MobaConfigDatabase(textAssetLoader: assets);
            database.LoadFromGroups(MobaLubanConfigGroups.Create(
                assets,
                new[] { "characters", "attribute_templates", "models" }));

            Assert.True(database.TryGetCharacter(MobaMinionArchetypeCatalog.MeleeCharacterId, out var melee));
            Assert.True(database.TryGetCharacter(MobaMinionArchetypeCatalog.RangedCharacterId, out var ranged));
            Assert.NotNull(melee);
            Assert.NotNull(ranged);
            Assert.Equal(2001, melee.AttributeTemplateId);
            Assert.Equal(2002, ranged.AttributeTemplateId);
            Assert.Equal(1101, melee.ModelId);
            Assert.Equal(1102, ranged.ModelId);

            Assert.True(database.TryGetAttributeTemplate(melee.AttributeTemplateId, out var meleeAttributes));
            Assert.True(database.TryGetAttributeTemplate(ranged.AttributeTemplateId, out var rangedAttributes));
            Assert.True(database.TryGetModel(melee.ModelId, out var meleeModel));
            Assert.True(database.TryGetModel(ranged.ModelId, out var rangedModel));
            Assert.NotNull(meleeAttributes);
            Assert.NotNull(rangedAttributes);
            Assert.NotNull(meleeModel);
            Assert.NotNull(rangedModel);
            Assert.Equal(1200, meleeAttributes.Hp);
            Assert.Equal(780, rangedAttributes.Hp);
            Assert.Equal(48, meleeAttributes.PhysicsAttack);
            Assert.Equal(62, rangedAttributes.PhysicsAttack);
            Assert.Equal(1, meleeAttributes.AttackRange);
            Assert.Equal(5, rangedAttributes.AttackRange);
            Assert.Equal(3.2f, meleeAttributes.MoveSpeed);
            Assert.Equal(3.0f, rangedAttributes.MoveSpeed);
            Assert.Equal("character/character8", meleeModel.PrefabPath);
            Assert.Equal("character/character8", rangedModel.PrefabPath);
            Assert.Equal(0.72f, meleeModel.Scale);
            Assert.Equal(0.62f, rangedModel.Scale);
        }

        [Theory]
        [InlineData(MobaMinionArchetype.Melee, 2001)]
        [InlineData(MobaMinionArchetype.Ranged, 2002)]
        public void MinionArchetypeUsesPublishedCharacterIdentity(
            MobaMinionArchetype archetype,
            int expectedCharacterId)
        {
            Assert.Equal(expectedCharacterId, MobaMinionArchetypeCatalog.GetCharacterId(archetype));
        }

        [Fact]
        public void MinionRuntimeViewKeepsWaveTeamArchetypeAndAiState()
        {
            var runtime = new MobaMinionRuntimeView(
                actorId: 42,
                waveId: 7L << 32,
                team: Team.Team1,
                archetype: MobaMinionArchetype.Melee,
                slot: 2,
                aiState: MobaMinionAiState.Attacking);

            Assert.Equal(42, runtime.ActorId);
            Assert.Equal(7L << 32, runtime.WaveId);
            Assert.Equal(Team.Team1, runtime.Team);
            Assert.Equal(MobaMinionArchetype.Melee, runtime.Archetype);
            Assert.Equal(2, runtime.Slot);
            Assert.Equal(MobaMinionAiState.Attacking, runtime.AiState);
        }

        [Theory]
        [InlineData(MobaMinionAiState.FollowingRoute, true)]
        [InlineData(MobaMinionAiState.AcquiringTarget, true)]
        [InlineData(MobaMinionAiState.Attacking, false)]
        [InlineData(MobaMinionAiState.Dead, false)]
        [InlineData(MobaMinionAiState.Terminal, false)]
        public void RouteMovementPolicyOnlyAdvancesForMovingStates(
            MobaMinionAiState state,
            bool expected)
        {
            Assert.Equal(expected, MobaMinionAiStatePolicy.ShouldFollowRoute(state));
        }

        [Fact]
        public void MinionKilledEventUsesZeroRewardBaseline()
        {
            var killed = new MobaMinionKilledEvent(
                actorId: 42,
                killerActorId: 99,
                waveId: 7L << 32,
                archetype: MobaMinionArchetype.Ranged,
                frame: 128);

            Assert.Equal(42, killed.ActorId);
            Assert.Equal(99, killed.KillerActorId);
            Assert.Equal(MobaMinionArchetype.Ranged, killed.Archetype);
            Assert.Equal(0, killed.RewardPoints);
        }
    }
}
