using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.ECS.Entitas;
using AbilityKit.ECS;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Game.Flow.Battle.Replay;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class SessionDiagnosticsReplicationTests
    {
        [Test]
        public void Diagnostics_Dispose_ClearsOwnedPublicationsAndIsIdempotent()
        {
            var diagnostics = new BattleSessionDiagnostics(
                new BattleReplicationRuntime());
            var jitter = new JitterBufferStatsSnapshot { DelayFrames = 3 };
            var timeSync = new TimeSyncStatsSnapshot { Samples = 5 };
            var timeSyncByWorld = new Dictionary<string, TimeSyncStatsSnapshot>
            {
                ["world-a"] = timeSync,
            };

            diagnostics.PublishJitterBuffer(jitter);
            diagnostics.PublishTimeSync(timeSync, timeSyncByWorld);
            diagnostics.InitializeConfirmedAuthority("world-a");
            diagnostics.UpdateConfirmedAuthority(
                10,
                12,
                13,
                14,
                15,
                2,
                new[] { "spawn", "hit" });
            var authority = BattleFlowDebugProvider.ConfirmedAuthorityWorldStats;

            Assert.That(BattleFlowDebugProvider.JitterBufferStats, Is.SameAs(jitter));
            Assert.That(BattleFlowDebugProvider.TimeSyncStats, Is.SameAs(timeSync));
            Assert.That(BattleFlowDebugProvider.TimeSyncStatsByWorld, Is.SameAs(timeSyncByWorld));
            Assert.That(authority.WorldId, Is.EqualTo("world-a"));
            Assert.That(authority.ConfirmedFrame, Is.EqualTo(10));
            Assert.That(authority.RecentViewEvents, Is.EqualTo(new[] { "spawn", "hit" }));

            diagnostics.Dispose();
            diagnostics.Dispose();

            Assert.That(BattleFlowDebugProvider.JitterBufferStats, Is.Null);
            Assert.That(BattleFlowDebugProvider.TimeSyncStats, Is.Null);
            Assert.That(BattleFlowDebugProvider.TimeSyncStatsByWorld, Is.Null);
            Assert.That(BattleFlowDebugProvider.ConfirmedAuthorityWorldStats, Is.Null);
        }

        [Test]
        public void Diagnostics_ScopedPublication_SeparatesWorlds()
        {
            var first = new BattleSessionDiagnostics(new BattleReplicationRuntime());
            var second = new BattleSessionDiagnostics(new BattleReplicationRuntime());
            var firstJitter = new JitterBufferStatsSnapshot { DelayFrames = 2 };
            var secondJitter = new JitterBufferStatsSnapshot { DelayFrames = 5 };
            var firstTimeSync = new TimeSyncStatsSnapshot { Samples = 3 };
            var secondTimeSync = new TimeSyncStatsSnapshot { Samples = 7 };
            var firstByWorld = new Dictionary<string, TimeSyncStatsSnapshot>
            {
                ["world-a"] = firstTimeSync,
            };
            var secondByWorld = new Dictionary<string, TimeSyncStatsSnapshot>
            {
                ["world-b"] = secondTimeSync,
            };
            try
            {
                first.BindScope("world-a");
                second.BindScope("world-b");
                first.PublishJitterBuffer(firstJitter);
                second.PublishJitterBuffer(secondJitter);
                first.PublishTimeSync(firstTimeSync, firstByWorld);
                second.PublishTimeSync(secondTimeSync, secondByWorld);
                first.InitializeConfirmedAuthority("world-a");
                second.InitializeConfirmedAuthority("world-b");

                Assert.That(BattleFlowDebugProvider.TryGetJitterBufferStats("world-a", out var firstJitterResult), Is.True);
                Assert.That(BattleFlowDebugProvider.TryGetJitterBufferStats("world-b", out var secondJitterResult), Is.True);
                Assert.That(firstJitterResult, Is.SameAs(firstJitter));
                Assert.That(secondJitterResult, Is.SameAs(secondJitter));
                Assert.That(BattleFlowDebugProvider.TryGetTimeSyncStats("world-a", out var firstCurrent, out var firstWorlds), Is.True);
                Assert.That(BattleFlowDebugProvider.TryGetTimeSyncStats("world-b", out var secondCurrent, out var secondWorlds), Is.True);
                Assert.That(firstCurrent, Is.SameAs(firstTimeSync));
                Assert.That(firstWorlds, Is.SameAs(firstByWorld));
                Assert.That(secondCurrent, Is.SameAs(secondTimeSync));
                Assert.That(secondWorlds, Is.SameAs(secondByWorld));
                Assert.That(BattleFlowDebugProvider.TryGetConfirmedAuthorityStats("world-a", out var firstAuthority), Is.True);
                Assert.That(BattleFlowDebugProvider.TryGetConfirmedAuthorityStats("world-b", out var secondAuthority), Is.True);
                Assert.That(firstAuthority.WorldId, Is.EqualTo("world-a"));
                Assert.That(secondAuthority.WorldId, Is.EqualTo("world-b"));

                first.Dispose();

                Assert.That(BattleFlowDebugProvider.TryGetJitterBufferStats("world-a", out _), Is.False);
                Assert.That(BattleFlowDebugProvider.TryGetTimeSyncStats("world-a", out _, out _), Is.False);
                Assert.That(BattleFlowDebugProvider.TryGetConfirmedAuthorityStats("world-a", out _), Is.False);
                Assert.That(BattleFlowDebugProvider.TryGetJitterBufferStats("world-b", out var remaining), Is.True);
                Assert.That(remaining, Is.SameAs(secondJitter));
            }
            finally
            {
                first.Dispose();
                second.Dispose();
            }
        }

        [Test]
        public void Diagnostics_ScopedStaleOwner_DoesNotWithdrawReplacement()
        {
            var stale = new BattleSessionDiagnostics(new BattleReplicationRuntime());
            var active = new BattleSessionDiagnostics(new BattleReplicationRuntime());
            var staleJitter = new JitterBufferStatsSnapshot { DelayFrames = 1 };
            var activeJitter = new JitterBufferStatsSnapshot { DelayFrames = 9 };
            try
            {
                stale.BindScope("shared-world");
                active.BindScope("shared-world");
                stale.PublishJitterBuffer(staleJitter);
                active.PublishJitterBuffer(activeJitter);

                stale.Dispose();

                Assert.That(BattleFlowDebugProvider.TryGetJitterBufferStats("shared-world", out var current), Is.True);
                Assert.That(current, Is.SameAs(activeJitter));
            }
            finally
            {
                stale.Dispose();
                active.Dispose();
            }
        }

        [Test]
        public void Diagnostics_StaleOwnerDispose_DoesNotClearReplacementPublications()
        {
            var stale = new BattleSessionDiagnostics(new BattleReplicationRuntime());
            var active = new BattleSessionDiagnostics(new BattleReplicationRuntime());
            var staleJitter = new JitterBufferStatsSnapshot { DelayFrames = 1 };
            var activeJitter = new JitterBufferStatsSnapshot { DelayFrames = 2 };
            var staleTimeSync = new TimeSyncStatsSnapshot { Samples = 1 };
            var activeTimeSync = new TimeSyncStatsSnapshot { Samples = 2 };
            var staleByWorld = new Dictionary<string, TimeSyncStatsSnapshot>
            {
                ["stale"] = staleTimeSync,
            };
            var activeByWorld = new Dictionary<string, TimeSyncStatsSnapshot>
            {
                ["active"] = activeTimeSync,
            };

            stale.PublishJitterBuffer(staleJitter);
            stale.PublishTimeSync(staleTimeSync, staleByWorld);
            stale.InitializeConfirmedAuthority("stale");
            active.PublishJitterBuffer(activeJitter);
            active.PublishTimeSync(activeTimeSync, activeByWorld);
            active.InitializeConfirmedAuthority("active");
            var activeAuthority = BattleFlowDebugProvider.ConfirmedAuthorityWorldStats;

            stale.Dispose();

            Assert.That(BattleFlowDebugProvider.JitterBufferStats, Is.SameAs(activeJitter));
            Assert.That(BattleFlowDebugProvider.TimeSyncStats, Is.SameAs(activeTimeSync));
            Assert.That(BattleFlowDebugProvider.TimeSyncStatsByWorld, Is.SameAs(activeByWorld));
            Assert.That(BattleFlowDebugProvider.ConfirmedAuthorityWorldStats, Is.SameAs(activeAuthority));

            active.Dispose();
        }

        [Test]
        public void Diagnostics_SeparateSessions_PublishIndependentSnapshots()
        {
            var first = new BattleSessionRuntime();
            var second = new BattleSessionRuntime();
            var firstJitter = new JitterBufferStatsSnapshot { DelayFrames = 4 };
            var secondJitter = new JitterBufferStatsSnapshot { DelayFrames = 7 };

            first.Diagnostics.PublishJitterBuffer(firstJitter);
            second.Diagnostics.PublishJitterBuffer(secondJitter);

            Assert.That(first.Diagnostics, Is.Not.SameAs(second.Diagnostics));
            Assert.That(BattleFlowDebugProvider.JitterBufferStats, Is.SameAs(secondJitter));

            first.Diagnostics.Dispose();

            Assert.That(BattleFlowDebugProvider.JitterBufferStats, Is.SameAs(secondJitter));

            second.Diagnostics.Dispose();
            Assert.That(BattleFlowDebugProvider.JitterBufferStats, Is.Null);
        }

        [Test]
        public void InputSubmissionDiagnostics_ScopedPublication_SeparatesWorlds()
        {
            var firstTransport = CreateNetworkTransport();
            var secondTransport = CreateNetworkTransport();
            var first = new InputSubmissionDiagnosticsBinding();
            var second = new InputSubmissionDiagnosticsBinding();
            try
            {
                first.Bind(firstTransport, "world-a");
                second.Bind(secondTransport, "world-b");

                Assert.That(InputSubmissionStatsProvider.TryGet("world-a", out var firstStats), Is.True);
                Assert.That(InputSubmissionStatsProvider.TryGet("world-b", out var secondStats), Is.True);
                Assert.That(firstStats, Is.Not.SameAs(secondStats));
                Assert.That(InputSubmissionStatsProvider.Current, Is.SameAs(secondStats));

                first.Dispose();

                Assert.That(InputSubmissionStatsProvider.TryGet("world-a", out _), Is.False);
                Assert.That(InputSubmissionStatsProvider.TryGet("world-b", out var remaining), Is.True);
                Assert.That(remaining, Is.SameAs(secondStats));
                Assert.That(InputSubmissionStatsProvider.Current, Is.SameAs(secondStats));
            }
            finally
            {
                first.Dispose();
                second.Dispose();
                firstTransport.Dispose();
                secondTransport.Dispose();
            }
        }

        [Test]
        public void InputSubmissionDiagnostics_StaleScopeOwner_DoesNotWithdrawReplacement()
        {
            var staleTransport = CreateNetworkTransport();
            var activeTransport = CreateNetworkTransport();
            var stale = new InputSubmissionDiagnosticsBinding();
            var active = new InputSubmissionDiagnosticsBinding();
            try
            {
                stale.Bind(staleTransport, "shared-world");
                active.Bind(activeTransport, "shared-world");
                Assert.That(InputSubmissionStatsProvider.TryGet("shared-world", out var replacement), Is.True);

                stale.Dispose();

                Assert.That(InputSubmissionStatsProvider.TryGet("shared-world", out var current), Is.True);
                Assert.That(current, Is.SameAs(replacement));
            }
            finally
            {
                stale.Dispose();
                active.Dispose();
                staleTransport.Dispose();
                activeTransport.Dispose();
            }
        }

        [Test]
        public void BattleFlowDebugProvider_ScopedPublication_SeparatesScopesAndProtectsReplacement()
        {
            var firstContext = new BattleContext();
            var staleContext = new BattleContext();
            var replacementContext = new BattleContext();
            var firstHud = new BattleHudFeature();
            var replacementHud = new BattleHudFeature();
            var firstView = new BattleViewFeature();
            var replacementView = new BattleViewFeature();
            var firstConfirmed = new ConfirmedBattleViewFeature(firstContext);
            var replacementConfirmed = new ConfirmedBattleViewFeature(replacementContext);
            try
            {
                BattleFlowDebugProvider.PublishContext("world-a", firstContext);
                BattleFlowDebugProvider.PublishHud("world-a", firstHud);
                BattleFlowDebugProvider.PublishView("world-a", firstView);
                BattleFlowDebugProvider.PublishConfirmedView("world-a", firstConfirmed);
                BattleFlowDebugProvider.PublishContext("world-b", staleContext);
                BattleFlowDebugProvider.PublishContext("world-b", replacementContext);
                BattleFlowDebugProvider.PublishHud("world-b", replacementHud);
                BattleFlowDebugProvider.PublishView("world-b", replacementView);
                BattleFlowDebugProvider.PublishConfirmedView("world-b", replacementConfirmed);

                Assert.That(BattleFlowDebugProvider.TryGetContext("world-a", out var contextA), Is.True);
                Assert.That(BattleFlowDebugProvider.TryGetContext("world-b", out var contextB), Is.True);
                Assert.That(BattleFlowDebugProvider.TryGetHud("world-a", out var hudA), Is.True);
                Assert.That(BattleFlowDebugProvider.TryGetHud("world-b", out var hudB), Is.True);
                Assert.That(BattleFlowDebugProvider.TryGetView("world-a", out var viewA), Is.True);
                Assert.That(BattleFlowDebugProvider.TryGetView("world-b", out var viewB), Is.True);
                Assert.That(BattleFlowDebugProvider.TryGetConfirmedView("world-a", out var confirmedA), Is.True);
                Assert.That(BattleFlowDebugProvider.TryGetConfirmedView("world-b", out var confirmedB), Is.True);
                Assert.That(contextA, Is.SameAs(firstContext));
                Assert.That(contextB, Is.SameAs(replacementContext));
                Assert.That(hudA, Is.SameAs(firstHud));
                Assert.That(hudB, Is.SameAs(replacementHud));
                Assert.That(viewA, Is.SameAs(firstView));
                Assert.That(viewB, Is.SameAs(replacementView));
                Assert.That(confirmedA, Is.SameAs(firstConfirmed));
                Assert.That(confirmedB, Is.SameAs(replacementConfirmed));

                BattleFlowDebugProvider.WithdrawContext("world-b", staleContext);

                Assert.That(BattleFlowDebugProvider.TryGetContext("world-b", out contextB), Is.True);
                Assert.That(contextB, Is.SameAs(replacementContext));
            }
            finally
            {
                BattleFlowDebugProvider.WithdrawConfirmedView("world-a", firstConfirmed);
                BattleFlowDebugProvider.WithdrawView("world-a", firstView);
                BattleFlowDebugProvider.WithdrawHud("world-a", firstHud);
                BattleFlowDebugProvider.WithdrawContext("world-a", firstContext);
                BattleFlowDebugProvider.WithdrawConfirmedView("world-b", replacementConfirmed);
                BattleFlowDebugProvider.WithdrawView("world-b", replacementView);
                BattleFlowDebugProvider.WithdrawHud("world-b", replacementHud);
                BattleFlowDebugProvider.WithdrawContext("world-b", staleContext);
                BattleFlowDebugProvider.WithdrawContext("world-b", replacementContext);
            }
        }

        [Test]
        public void BattleDebugPublicationOwner_LateContext_PublishesPreviouslyDiscoveredViews()
        {
            var owner = new BattleDebugPublicationOwner();
            var features = new TestGameFeatureStore();
            var hud = new BattleHudFeature();
            var view = new BattleViewFeature();
            var confirmedContext = CreateBattleContext("world-a-confirmed");
            var confirmedView = new ConfirmedBattleViewFeature(confirmedContext);
            var context = CreateBattleContext("world-a");
            var phase = new GamePhaseContext(null, features, null);
            try
            {
                features.Set(hud);
                features.Set(view);
                features.Set(confirmedView);
                owner.Refresh(in phase);

                Assert.That(BattleFlowDebugProvider.TryGetHud("world-a", out _), Is.False);
                Assert.That(BattleFlowDebugProvider.TryGetView("world-a", out _), Is.False);
                Assert.That(BattleFlowDebugProvider.TryGetConfirmedView("world-a", out _), Is.False);

                features.Set(context);
                owner.Refresh(in phase);

                Assert.That(BattleFlowDebugProvider.TryGetContext("world-a", out var currentContext), Is.True);
                Assert.That(BattleFlowDebugProvider.TryGetHud("world-a", out var currentHud), Is.True);
                Assert.That(BattleFlowDebugProvider.TryGetView("world-a", out var currentView), Is.True);
                Assert.That(BattleFlowDebugProvider.TryGetConfirmedView("world-a", out var currentConfirmedView), Is.True);
                Assert.That(currentContext, Is.SameAs(context));
                Assert.That(currentHud, Is.SameAs(hud));
                Assert.That(currentView, Is.SameAs(view));
                Assert.That(currentConfirmedView, Is.SameAs(confirmedView));
            }
            finally
            {
                owner.Dispose();
            }
        }

        [Test]
        public void BattleDebugPublicationOwner_ScopeRebind_MigratesViewsAndProtectsReplacement()
        {
            var staleOwner = new BattleDebugPublicationOwner();
            var activeOwner = new BattleDebugPublicationOwner();
            var staleFeatures = new TestGameFeatureStore();
            var activeFeatures = new TestGameFeatureStore();
            var staleHud = new BattleHudFeature();
            var staleView = new BattleViewFeature();
            var staleConfirmed = new ConfirmedBattleViewFeature(
                CreateBattleContext("stale-confirmed"));
            var activeContext = CreateBattleContext("world-b");
            var activeHud = new BattleHudFeature();
            var activeView = new BattleViewFeature();
            var activeConfirmed = new ConfirmedBattleViewFeature(activeContext);
            var stalePhase = new GamePhaseContext(null, staleFeatures, null);
            var activePhase = new GamePhaseContext(null, activeFeatures, null);
            try
            {
                staleFeatures.Set(CreateBattleContext("world-a"));
                staleFeatures.Set(staleHud);
                staleFeatures.Set(staleView);
                staleFeatures.Set(staleConfirmed);
                staleOwner.Refresh(in stalePhase);

                staleFeatures.Set(CreateBattleContext("world-b"));
                staleOwner.Refresh(in stalePhase);

                Assert.That(BattleFlowDebugProvider.TryGetContext("world-a", out _), Is.False);
                Assert.That(BattleFlowDebugProvider.TryGetHud("world-a", out _), Is.False);
                Assert.That(BattleFlowDebugProvider.TryGetView("world-a", out _), Is.False);
                Assert.That(BattleFlowDebugProvider.TryGetConfirmedView("world-a", out _), Is.False);
                Assert.That(BattleFlowDebugProvider.TryGetHud("world-b", out var reboundHud), Is.True);
                Assert.That(BattleFlowDebugProvider.TryGetView("world-b", out var reboundView), Is.True);
                Assert.That(BattleFlowDebugProvider.TryGetConfirmedView("world-b", out var reboundConfirmed), Is.True);
                Assert.That(reboundHud, Is.SameAs(staleHud));
                Assert.That(reboundView, Is.SameAs(staleView));
                Assert.That(reboundConfirmed, Is.SameAs(staleConfirmed));

                activeFeatures.Set(activeContext);
                activeFeatures.Set(activeHud);
                activeFeatures.Set(activeView);
                activeFeatures.Set(activeConfirmed);
                activeOwner.Refresh(in activePhase);

                staleOwner.Dispose();

                Assert.That(BattleFlowDebugProvider.TryGetContext("world-b", out var currentContext), Is.True);
                Assert.That(BattleFlowDebugProvider.TryGetHud("world-b", out var currentHud), Is.True);
                Assert.That(BattleFlowDebugProvider.TryGetView("world-b", out var currentView), Is.True);
                Assert.That(BattleFlowDebugProvider.TryGetConfirmedView("world-b", out var currentConfirmed), Is.True);
                Assert.That(currentContext, Is.SameAs(activeContext));
                Assert.That(currentHud, Is.SameAs(activeHud));
                Assert.That(currentView, Is.SameAs(activeView));
                Assert.That(currentConfirmed, Is.SameAs(activeConfirmed));
            }
            finally
            {
                staleOwner.Dispose();
                activeOwner.Dispose();
            }
        }

        [Test]
        public void DebugFacadeProvider_ScopedPublication_SeparatesScopesAndProtectsReplacement()
        {
            var first = new StubDebugFacade();
            var stale = new StubDebugFacade();
            var replacement = new StubDebugFacade();
            try
            {
                BattleDebugFacadeProvider.Publish("world-a", first);
                BattleDebugFacadeProvider.Publish("world-b", stale);
                BattleDebugFacadeProvider.Publish("world-b", replacement);

                Assert.That(BattleDebugFacadeProvider.TryGet("world-a", out var firstResult), Is.True);
                Assert.That(BattleDebugFacadeProvider.TryGet("world-b", out var secondResult), Is.True);
                Assert.That(firstResult, Is.SameAs(first));
                Assert.That(secondResult, Is.SameAs(replacement));

                BattleDebugFacadeProvider.Withdraw("world-b", stale);

                Assert.That(BattleDebugFacadeProvider.TryGet("world-b", out secondResult), Is.True);
                Assert.That(secondResult, Is.SameAs(replacement));
            }
            finally
            {
                BattleDebugFacadeProvider.Withdraw("world-a", first);
                BattleDebugFacadeProvider.Withdraw("world-b", stale);
                BattleDebugFacadeProvider.Withdraw("world-b", replacement);
            }
        }

        [Test]
        public void ReplayControlProvider_ScopedPublication_SeparatesScopesAndProtectsReplacement()
        {
            var first = new StubReplayControl();
            var stale = new StubReplayControl();
            var replacement = new StubReplayControl();
            try
            {
                BattleReplayControlProvider.Publish("world-a", first);
                BattleReplayControlProvider.Publish("world-b", stale);
                BattleReplayControlProvider.Publish("world-b", replacement);

                Assert.That(BattleReplayControlProvider.TryGet("world-a", out var firstResult), Is.True);
                Assert.That(BattleReplayControlProvider.TryGet("world-b", out var secondResult), Is.True);
                Assert.That(firstResult, Is.SameAs(first));
                Assert.That(secondResult, Is.SameAs(replacement));

                BattleReplayControlProvider.Withdraw("world-b", stale);

                Assert.That(BattleReplayControlProvider.TryGet("world-b", out secondResult), Is.True);
                Assert.That(secondResult, Is.SameAs(replacement));
            }
            finally
            {
                BattleReplayControlProvider.Withdraw("world-a", first);
                BattleReplayControlProvider.Withdraw("world-b", stale);
                BattleReplayControlProvider.Withdraw("world-b", replacement);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Test]
        public void Diagnostics_DebugControlFacade_UpdatesActiveSessionOwner()
        {
            var diagnostics = new BattleSessionDiagnostics(new BattleReplicationRuntime());
            try
            {
                BattleSessionFeature.DebugForceClientHashMismatch = false;
                diagnostics.PublishDebugControls();

                BattleSessionFeature.DebugForceClientHashMismatch = true;

                Assert.That(diagnostics.ShouldForceClientHashMismatch, Is.True);
            }
            finally
            {
                diagnostics.Dispose();
                BattleSessionFeature.DebugForceClientHashMismatch = false;
            }
        }

        [Test]
        public void Diagnostics_DebugControlReplacement_AdoptsLatestCompatibilityValue()
        {
            var stale = new BattleSessionDiagnostics(new BattleReplicationRuntime());
            var active = new BattleSessionDiagnostics(new BattleReplicationRuntime());
            try
            {
                BattleSessionFeature.DebugForceClientHashMismatch = true;
                stale.PublishDebugControls();
                active.PublishDebugControls();

                Assert.That(stale.ShouldForceClientHashMismatch, Is.True);
                Assert.That(active.ShouldForceClientHashMismatch, Is.True);
            }
            finally
            {
                stale.Dispose();
                active.Dispose();
                BattleSessionFeature.DebugForceClientHashMismatch = false;
            }
        }

        [Test]
        public void Diagnostics_DebugControlStaleOwnerDispose_DoesNotAffectReplacement()
        {
            var stale = new BattleSessionDiagnostics(new BattleReplicationRuntime());
            var active = new BattleSessionDiagnostics(new BattleReplicationRuntime());
            try
            {
                BattleSessionFeature.DebugForceClientHashMismatch = false;
                stale.PublishDebugControls();
                active.PublishDebugControls();
                BattleSessionFeature.DebugForceClientHashMismatch = true;

                stale.Dispose();

                Assert.That(active.ShouldForceClientHashMismatch, Is.True);
                Assert.That(BattleSessionFeature.DebugForceClientHashMismatch, Is.True);
            }
            finally
            {
                stale.Dispose();
                active.Dispose();
                BattleSessionFeature.DebugForceClientHashMismatch = false;
            }
        }
#endif

        [Test]
        public void Diagnostics_HealthFacade_ReflectsReplicationState()
        {
            var replication = new BattleReplicationRuntime();
            var diagnostics = new BattleSessionDiagnostics(replication);
            var health = new MobaSynchronizationHealthSnapshot(
                MobaSynchronizationHealthLevel.Degraded,
                3,
                2,
                0,
                1,
                0,
                0,
                1,
                default);
            var report = new SyncHealthReport(
                3,
                1,
                1,
                1,
                0,
                0,
                null,
                null);

            replication.SynchronizationHealth = health;
            replication.SynchronizationHealthReport = report;

            Assert.That(diagnostics.SynchronizationHealth.PressureScore, Is.EqualTo(3));
            Assert.That(diagnostics.SynchronizationHealth.Level, Is.EqualTo(MobaSynchronizationHealthLevel.Degraded));
            Assert.That(diagnostics.SynchronizationHealthReport, Is.SameAs(report));
        }

        [Test]
        public void ReplicationRuntime_BuildAndDispose_OwnsBindingsAndRestoresOptions()
        {
            var transport = CreateNetworkTransport();
            var options = transport.Options;
            Func<string> previousEpoch = () => "previous";
            Func<long> previousSequence = () => 17L;
            Action<int> previousAck = _ => { };
            options.GetReliableEventEpoch = previousEpoch;
            options.GetReliableEventLastAcknowledgedSequence = previousSequence;
            options.OnSubmitInputAck = previousAck;
            var owner = new BattleReplicationRuntime();
            var connected = 0;
            var disconnected = 0;

            var checkpointAccepted = owner.Build(
                transport,
                30,
                42UL,
                "battle",
                default,
                _ => { },
                _ => { },
                () => disconnected++,
                () => connected++);

            Assert.That(checkpointAccepted, Is.True);
            Assert.That(owner.IsBuilt, Is.True);
            Assert.That(owner.Transport, Is.SameAs(transport));
            Assert.That(owner.InterpolationController, Is.Not.Null);
            Assert.That(owner.ReplicationPipeline, Is.Not.Null);
            Assert.That(owner.SnapshotAdmission, Is.Not.Null);
            Assert.That(owner.AuthoritativeSnapshotState, Is.Not.Null);
            Assert.That(owner.ReliableEventCursor, Is.Not.Null);
            Assert.That(owner.PendingStateImport, Is.True);
            Assert.That(options.GetReliableEventEpoch, Is.Not.SameAs(previousEpoch));
            Assert.That(options.GetReliableEventLastAcknowledgedSequence, Is.Not.SameAs(previousSequence));
            Assert.That(options.OnSubmitInputAck, Is.Not.SameAs(previousAck));

            options.OnSubmitInputAck(12);
            InvokePrivate(transport, "OnConnected");
            InvokePrivate(transport, "OnDisconnected");

            Assert.That(owner.LastServerAckFrame, Is.EqualTo(12));
            Assert.That(connected, Is.EqualTo(1));
            Assert.That(disconnected, Is.EqualTo(1));

            owner.Dispose();
            owner.Dispose();

            Assert.That(owner.IsBuilt, Is.False);
            Assert.That(owner.InterpolationController, Is.Null);
            Assert.That(owner.PendingReliableEventBatches, Is.Empty);
            Assert.That(options.GetReliableEventEpoch, Is.SameAs(previousEpoch));
            Assert.That(options.GetReliableEventLastAcknowledgedSequence, Is.SameAs(previousSequence));
            Assert.That(options.OnSubmitInputAck, Is.SameAs(previousAck));
            transport.Dispose();
        }

        [Test]
        public void ReplicationRuntime_Rebuild_DetachesOldGeneration()
        {
            var firstTransport = CreateNetworkTransport();
            var secondTransport = CreateNetworkTransport();
            var owner = new BattleReplicationRuntime();
            var firstConnected = 0;
            var secondConnected = 0;

            owner.Build(
                firstTransport, 30, 1UL, "first", default,
                _ => { }, _ => { }, () => { }, () => firstConnected++);
            owner.Build(
                secondTransport, 30, 2UL, "second", default,
                _ => { }, _ => { }, () => { }, () => secondConnected++);

            InvokePrivate(firstTransport, "OnConnected");
            InvokePrivate(secondTransport, "OnConnected");

            Assert.That(firstConnected, Is.Zero);
            Assert.That(secondConnected, Is.EqualTo(1));
            Assert.That(owner.Transport, Is.SameAs(secondTransport));

            owner.Dispose();
            firstTransport.Dispose();
            secondTransport.Dispose();
        }

        [Test]
        public void ReplicationRuntime_InvalidRebuild_PreservesCurrentGeneration()
        {
            var transport = CreateNetworkTransport();
            var owner = new BattleReplicationRuntime();
            var connected = 0;
            owner.Build(
                transport, 30, 1UL, "battle", default,
                _ => { }, _ => { }, () => { }, () => connected++);

            Assert.Throws<ArgumentNullException>(() => owner.Build(
                transport, 30, 1UL, "battle", default,
                null, _ => { }, () => { }, () => { }));
            InvokePrivate(transport, "OnConnected");

            Assert.That(owner.IsBuilt, Is.True);
            Assert.That(owner.Transport, Is.SameAs(transport));
            Assert.That(connected, Is.EqualTo(1));

            owner.Dispose();
            transport.Dispose();
        }

        [Test]
        public void ReplicationRuntime_Dispose_DoesNotOverwriteExternallyReplacedOptions()
        {
            var transport = CreateNetworkTransport();
            var owner = new BattleReplicationRuntime();
            owner.Build(
                transport, 30, 1UL, "battle", default,
                _ => { }, _ => { }, () => { }, () => { });
            Func<string> replacementEpoch = () => "replacement";
            Func<long> replacementSequence = () => 99L;
            Action<int> replacementAck = _ => { };
            transport.Options.GetReliableEventEpoch = replacementEpoch;
            transport.Options.GetReliableEventLastAcknowledgedSequence = replacementSequence;
            transport.Options.OnSubmitInputAck = replacementAck;

            owner.Dispose();

            Assert.That(transport.Options.GetReliableEventEpoch, Is.SameAs(replacementEpoch));
            Assert.That(transport.Options.GetReliableEventLastAcknowledgedSequence, Is.SameAs(replacementSequence));
            Assert.That(transport.Options.OnSubmitInputAck, Is.SameAs(replacementAck));
            transport.Dispose();
        }

        private static BattleContext CreateBattleContext(string worldId)
        {
            return new BattleContext
            {
                Plan = BattleStartPlanBuilder
                    .ForWorld(
                        worldId,
                        "battle",
                        "debug-client",
                        "debug-player",
                        tickRate: 30,
                        inputDelayFrames: 0)
                    .Build(),
            };
        }

        private sealed class TestGameFeatureStore : IGameFeatureStore
        {
            private readonly Dictionary<Type, object> _features =
                new Dictionary<Type, object>();

            public bool TryGet<T>(out T component) where T : class
            {
                if (_features.TryGetValue(typeof(T), out var value))
                {
                    component = value as T;
                    return component != null;
                }

                component = null;
                return false;
            }

            public void Set<T>(T component) where T : class
            {
                _features[typeof(T)] = component;
            }

            public void Remove<T>() where T : class
            {
                _features.Remove(typeof(T));
            }

            public void Remove(Type componentType)
            {
                _features.Remove(componentType);
            }
        }

        private sealed class StubReplayControl : IBattleReplayControl
        {
            public bool IsReplaySession => false;
            public bool IsPlaying => false;
            public bool RenderPresentation => false;
            public int CurrentFrame => 0;
            public int LastFrame => 0;
            public float PlaybackSpeed { get; set; } = 1f;
            public string ReplayPath => string.Empty;
            public bool TryLoad(string path, bool renderPresentation, out string error)
            {
                error = string.Empty;
                return false;
            }
            public void Play() { }
            public void Pause() { }
            public bool StepForward() => false;
            public bool StepBackward() => false;
            public bool SeekToFrame(int frame) => false;
        }

        private sealed class StubDebugFacade : IBattleDebugFacade
        {
            public bool TryGetSession(out BattleLogicSession session)
            {
                session = null;
                return false;
            }

            public bool TryListEntities(out IReadOnlyList<BattleDebugEntityId> ids)
            {
                ids = Array.Empty<BattleDebugEntityId>();
                return true;
            }

            public bool TryResolveUnit(BattleDebugEntityId id, out IUnitFacade unit)
            {
                unit = null;
                return false;
            }
        }

        private static NetworkTransport CreateNetworkTransport()
        {
            return new NetworkTransport(new NetworkTransportOptions
            {
                ConnectionFactory = () => new TrackingConnection()
            });
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Private method '{methodName}' was not found.");
            method.Invoke(target, null);
        }

        private sealed class TrackingConnection : IConnection
        {
            public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
            public bool IsConnected => State == ConnectionState.Connected;
            public int OpenCount { get; private set; }
            public int TickCount { get; private set; }
            public int DisposeCount { get; private set; }
            public string LastHost { get; private set; }
            public int LastPort { get; private set; }
            public float LastDeltaTime { get; private set; }

            public event Action Connected;
            public event Action Disconnected;
            public event Action<Exception> Error;
            public event Action<uint, uint, ArraySegment<byte>> PacketReceived;
            public event Action<uint, ArraySegment<byte>> ServerPushReceived;
            public event Action<string, string> Kicked;

            public void Open(string host, int port)
            {
                LastHost = host;
                LastPort = port;
                OpenCount++;
                State = ConnectionState.Connected;
                Connected?.Invoke();
            }

            public void Close()
            {
                State = ConnectionState.Disconnected;
                Disconnected?.Invoke();
            }

            public void Tick(float deltaTime)
            {
                LastDeltaTime = deltaTime;
                TickCount++;
            }

            public void Send(
                uint opCode,
                ArraySegment<byte> payload,
                ushort flags = 0,
                uint seq = 0)
            {
            }

            public void Dispose()
            {
                DisposeCount++;
                Close();
            }
        }
    }
}
