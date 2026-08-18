using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Editor;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class BattleDebugDiagnosticViewModelTests
    {
        [Test]
        public void EventsCacheKey_IncludesRevisionAndEveryFilterOrSelectionInput()
        {
            var session = new RecordingSession();
            var viewModel = new BattleDebugDiagnosticEventsViewModel();

            viewModel.RefreshIfNeeded(session, 10, true);
            Assert.That(session.EventQueryCount, Is.EqualTo(1));

            viewModel.RefreshIfNeeded(session, 10, true);
            Assert.That(session.EventQueryCount, Is.EqualTo(1));

            session.EventStoreRevision++;
            viewModel.RefreshIfNeeded(session, 10, true);
            Assert.That(session.EventQueryCount, Is.EqualTo(2));

            viewModel.FilterBySelectedActor = false;
            viewModel.RefreshIfNeeded(session, 10, true);
            Assert.That(session.EventQueryCount, Is.EqualTo(3));

            viewModel.ActorRelation = BattleDiagnosticActorRelation.Source;
            viewModel.RefreshIfNeeded(session, 10, true);
            Assert.That(session.EventQueryCount, Is.EqualTo(4));

            viewModel.FailuresOnly = true;
            viewModel.RefreshIfNeeded(session, 10, true);
            Assert.That(session.EventQueryCount, Is.EqualTo(5));

            viewModel.EventScope = BattleDebugDiagnosticEventScope.All;
            viewModel.RefreshIfNeeded(session, 10, true);
            Assert.That(session.EventQueryCount, Is.EqualTo(6));

            viewModel.RecentFrameCount = 120;
            viewModel.RefreshIfNeeded(session, 10, true);
            Assert.That(session.EventQueryCount, Is.EqualTo(7));

            viewModel.SearchText = "damage";
            viewModel.RefreshIfNeeded(session, 10, true);
            Assert.That(session.EventQueryCount, Is.EqualTo(8));

            viewModel.TriggerStage = BattleDiagnosticTriggerAnalysisStage.Conditions;
            viewModel.RefreshIfNeeded(session, 10, true);
            Assert.That(session.EventQueryCount, Is.EqualTo(9));
            Assert.That(session.LastEventQuery.Filter.TriggerStage, Is.EqualTo(BattleDiagnosticTriggerAnalysisStage.Conditions));

            viewModel.TriggerResult = BattleDiagnosticTriggerAnalysisResult.Failed;
            viewModel.TriggerContextKind = 2;
            viewModel.TriggerOriginKind = 3;
            viewModel.RefreshIfNeeded(session, 10, true);
            Assert.That(session.EventQueryCount, Is.EqualTo(10));
            Assert.That(session.LastEventQuery.Filter.TriggerResult, Is.EqualTo(BattleDiagnosticTriggerAnalysisResult.Failed));
            Assert.That(session.LastEventQuery.Filter.TriggerContextKind, Is.EqualTo(2));
            Assert.That(session.LastEventQuery.Filter.TriggerOriginKind, Is.EqualTo(3));

            viewModel.RefreshIfNeeded(session, 11, true);
            Assert.That(session.EventQueryCount, Is.EqualTo(11));

            viewModel.RefreshIfNeeded(session, 11, false);
            Assert.That(session.EventQueryCount, Is.EqualTo(12));
        }

        [Test]
        public void EventsWorkspaceFilter_MergesSharedAndLocalConditionsWithDocumentedPrecedence()
        {
            var viewModel = new BattleDebugDiagnosticEventsViewModel
            {
                FilterBySelectedActor = true,
                ActorRelation = BattleDiagnosticActorRelation.Target,
                FailuresOnly = true,
                EventScope = BattleDebugDiagnosticEventScope.Effects,
                ConfigId = 800,
                SearchText = "local",
                TriggerStage = BattleDiagnosticTriggerAnalysisStage.Conditions,
                TriggerResult = BattleDiagnosticTriggerAnalysisResult.Failed,
                TriggerContextKind = 8,
                TriggerOriginKind = 9
            };
            var shared = new BattleDiagnosticFilter(
                new BattleDiagnosticFrameFilter(10, 20),
                BattleDiagnosticEventChannel.Skill | BattleDiagnosticEventChannel.Effect,
                actorId: 42,
                actorRelation: BattleDiagnosticActorRelation.Source,
                configId: 700,
                rootContextId: 100,
                contextId: 110,
                skillRuntimeId: 120,
                attackId: 130,
                unfinishedOnly: true,
                searchText: "shared",
                triggerStage: BattleDiagnosticTriggerAnalysisStage.Budget,
                triggerResult: BattleDiagnosticTriggerAnalysisResult.Blocked,
                triggerContextKind: 4,
                triggerOriginKind: 5);

            var effective = viewModel.BuildEffectiveFilter(99, true, in shared);

            Assert.That(effective.Frames, Is.EqualTo(shared.Frames));
            Assert.That(effective.Channels, Is.EqualTo(BattleDiagnosticEventChannel.Effect));
            Assert.That(effective.ActorId, Is.EqualTo(42));
            Assert.That(effective.ActorRelation, Is.EqualTo(BattleDiagnosticActorRelation.Source));
            Assert.That(effective.ConfigId, Is.EqualTo(700));
            Assert.That(effective.RootContextId, Is.EqualTo(100));
            Assert.That(effective.ContextId, Is.EqualTo(110));
            Assert.That(effective.SkillRuntimeId, Is.EqualTo(120));
            Assert.That(effective.AttackId, Is.EqualTo(130));
            Assert.That(effective.FailuresOnly, Is.True);
            Assert.That(effective.UnfinishedOnly, Is.True);
            Assert.That(effective.SearchText, Is.EqualTo("shared"));
            Assert.That(effective.TriggerStage, Is.EqualTo(BattleDiagnosticTriggerAnalysisStage.Budget));
            Assert.That(effective.TriggerResult, Is.EqualTo(BattleDiagnosticTriggerAnalysisResult.Blocked));
            Assert.That(effective.TriggerContextKind, Is.EqualTo(4));
            Assert.That(effective.TriggerOriginKind, Is.EqualTo(5));
        }

        [Test]
        public void EventsWorkspaceFilter_ChangesCacheKeyAndInvalidatesFixedRevisionPaging()
        {
            var events = new List<BattleDiagnosticEvent>();
            for (var frame = 201; frame >= 1; frame--)
            {
                events.Add(new BattleDiagnosticEvent(
                    RecordingSession.Scope,
                    frame,
                    frame,
                    frame,
                    BattleDiagnosticEventKind.Damage,
                    BattleDiagnosticEventChannel.DamageAndHeal,
                    BattleDiagnosticEventOutcome.Succeeded));
            }

            var session = new RecordingSession
            {
                EventStoreRevision = 7,
                Events = events,
                PageEventResults = true
            };
            var viewModel = new BattleDebugDiagnosticEventsViewModel
            {
                FilterBySelectedActor = false,
                EventScope = BattleDebugDiagnosticEventScope.All,
                RecentFrameCount = 0
            };
            var initial = BattleDiagnosticFilter.Default;
            viewModel.RefreshIfNeeded(session, 0, false, in initial);
            viewModel.RefreshIfNeeded(session, 0, false, in initial);
            Assert.That(session.EventQueryCount, Is.EqualTo(1));
            Assert.That(viewModel.HasMore, Is.True);

            var changed = initial.WithSearchText("damage");
            Assert.That(viewModel.LoadMore(session, 0, false, in changed), Is.False);
            Assert.That(session.EventQueryCount, Is.EqualTo(1));
            Assert.That(viewModel.PagingStatusMessage, Does.Contain("筛选条件已变化"));

            viewModel.RefreshIfNeeded(session, 0, false, in changed);
            Assert.That(session.EventQueryCount, Is.EqualTo(2));
            Assert.That(session.LastEventQuery.Filter.SearchText, Is.EqualTo("damage"));
        }

        [Test]
        public void EventsClearLocalFilters_DoesNotMutateWorkspaceFilter()
        {
            var workspace = new BattleDiagnosticWorkspaceState();
            var shared = BattleDiagnosticFilter.Default.WithSearchText("shared");
            workspace.SetFilter(shared);
            var viewModel = new BattleDebugDiagnosticEventsViewModel
            {
                FilterBySelectedActor = true,
                FailuresOnly = true,
                EventScope = BattleDebugDiagnosticEventScope.Effects,
                RecentFrameCount = 600,
                ConfigId = 7,
                SearchText = "local"
            };

            viewModel.ClearLocalFilters();

            Assert.That(workspace.Filter, Is.EqualTo(shared));
            Assert.That(viewModel.HasActiveFilter, Is.False);
            Assert.That(viewModel.RecentFrameCount, Is.Zero);
            Assert.That(viewModel.BuildLocalFilter(99, true), Is.EqualTo(BattleDiagnosticFilter.Default));
        }

        [Test]
        public void EventsIssueGroups_AggregateStructuredTriggerFailuresAndFocusTheSelectedGroup()
        {
            var first = new BattleDiagnosticTriggerAnalysisPayload(
                7001,
                contextKind: 2,
                originKind: 3,
                BattleDiagnosticTriggerAnalysisStage.Conditions,
                BattleDiagnosticTriggerAnalysisResult.Failed,
                failureKey: "missingMana",
                reason: "Mana is insufficient.");
            var second = new BattleDiagnosticTriggerAnalysisPayload(
                7001,
                contextKind: 2,
                originKind: 3,
                BattleDiagnosticTriggerAnalysisStage.Conditions,
                BattleDiagnosticTriggerAnalysisResult.Failed,
                failureKey: "missingMana",
                reason: "Mana is insufficient.");
            var blocked = new BattleDiagnosticTriggerAnalysisPayload(
                7002,
                contextKind: 2,
                originKind: 3,
                BattleDiagnosticTriggerAnalysisStage.Budget,
                BattleDiagnosticTriggerAnalysisResult.Blocked,
                failureKey: "DepthLimit",
                reason: "Trigger depth limit reached.");
            var session = new RecordingSession
            {
                EventStoreRevision = 3,
                Events = new[]
                {
                    TriggerEvent(frame: 14, sequence: 3, configId: 7002, in blocked),
                    TriggerEvent(frame: 13, sequence: 2, configId: 7001, in second),
                    TriggerEvent(frame: 11, sequence: 1, configId: 7001, in first)
                }
            };
            var viewModel = new BattleDebugDiagnosticEventsViewModel
            {
                FilterBySelectedActor = false,
                EventScope = BattleDebugDiagnosticEventScope.All,
                RecentFrameCount = 0
            };

            viewModel.RefreshIfNeeded(session, 0, false);

            Assert.That(viewModel.IssueGroups, Has.Count.EqualTo(2));
            var missingMana = viewModel.IssueGroups[0];
            Assert.That(missingMana.Count, Is.EqualTo(2));
            Assert.That(missingMana.FirstFrame, Is.EqualTo(11));
            Assert.That(missingMana.LatestFrame, Is.EqualTo(13));
            Assert.That(missingMana.FrameSpan, Is.EqualTo(2));
            Assert.That(missingMana.ConfigId, Is.EqualTo(7001));
            Assert.That(missingMana.SearchText, Is.EqualTo("missingMana"));
            Assert.That(missingMana.TriggerStage, Is.EqualTo(BattleDiagnosticTriggerAnalysisStage.Conditions));
            Assert.That(missingMana.TriggerResult, Is.EqualTo(BattleDiagnosticTriggerAnalysisResult.Failed));

            viewModel.FocusIssueGroup(in missingMana);
            viewModel.RefreshIfNeeded(session, 0, false);

            Assert.That(viewModel.FilterBySelectedActor, Is.False);
            Assert.That(viewModel.FailuresOnly, Is.True);
            Assert.That(viewModel.EventScope, Is.EqualTo(BattleDebugDiagnosticEventScope.All));
            Assert.That(viewModel.RecentFrameCount, Is.Zero);
            Assert.That(session.LastEventQuery.Filter.ConfigId, Is.EqualTo(7001));
            Assert.That(session.LastEventQuery.Filter.SearchText, Is.EqualTo("missingMana"));
            Assert.That(session.LastEventQuery.Filter.TriggerStage, Is.EqualTo(BattleDiagnosticTriggerAnalysisStage.Conditions));
            Assert.That(session.LastEventQuery.Filter.TriggerResult, Is.EqualTo(BattleDiagnosticTriggerAnalysisResult.Failed));
        }

        [Test]
        public void EventsIssueGroups_AggregateSkillFailuresByStableFieldsAndFocusCode()
        {
            const string stableCode = "Cast.TargetOutOfRange";
            var firstFailure = new BattleDiagnosticSkillFailurePayload(
                slot: 2,
                source: "Cast",
                stage: "Preparation",
                code: stableCode,
                message: "Target is outside cast range.");
            var secondFailure = new BattleDiagnosticSkillFailurePayload(
                slot: 2,
                source: "Cast",
                stage: "Preparation",
                code: stableCode,
                message: "The selected target moved out of range.");
            var firstPayload = BattleDiagnosticEventPayload.FromSkillFailure(in firstFailure);
            var secondPayload = BattleDiagnosticEventPayload.FromSkillFailure(in secondFailure);
            var session = new RecordingSession
            {
                EventStoreRevision = 4,
                Events = new[]
                {
                    new BattleDiagnosticEvent(
                        RecordingSession.Scope,
                        15,
                        2,
                        2,
                        BattleDiagnosticEventKind.SkillFailure,
                        BattleDiagnosticEventChannel.Skill,
                        BattleDiagnosticEventOutcome.Failed,
                        configId: 101,
                        payloadVersion: BattleDiagnosticSkillFailurePayload.CurrentSchemaVersion,
                        summary: "Second localized summary",
                        payload: secondPayload),
                    new BattleDiagnosticEvent(
                        RecordingSession.Scope,
                        12,
                        1,
                        1,
                        BattleDiagnosticEventKind.SkillFailure,
                        BattleDiagnosticEventChannel.Skill,
                        BattleDiagnosticEventOutcome.Failed,
                        configId: 101,
                        payloadVersion: BattleDiagnosticSkillFailurePayload.CurrentSchemaVersion,
                        summary: "First localized summary",
                        payload: firstPayload)
                }
            };
            var viewModel = new BattleDebugDiagnosticEventsViewModel
            {
                FilterBySelectedActor = false,
                EventScope = BattleDebugDiagnosticEventScope.All,
                RecentFrameCount = 0
            };

            viewModel.RefreshIfNeeded(session, 0, false);

            Assert.That(viewModel.IssueGroups, Has.Count.EqualTo(1));
            var group = viewModel.IssueGroups[0];
            Assert.That(group.Count, Is.EqualTo(2));
            Assert.That(group.FirstFrame, Is.EqualTo(12));
            Assert.That(group.LatestFrame, Is.EqualTo(15));
            Assert.That(group.FrameSpan, Is.EqualTo(3));
            Assert.That(group.ConfigId, Is.EqualTo(101));
            Assert.That(group.Label, Does.Contain(stableCode));
            Assert.That(group.SearchText, Is.EqualTo(stableCode));
            Assert.That(
                group.TriggerStage,
                Is.EqualTo(BattleDiagnosticTriggerAnalysisStage.Unknown));
            Assert.That(
                group.TriggerResult,
                Is.EqualTo(BattleDiagnosticTriggerAnalysisResult.Unknown));

            viewModel.FocusIssueGroup(in group);
            viewModel.RefreshIfNeeded(session, 0, false);

            Assert.That(session.LastEventQuery.Filter.SearchText, Is.EqualTo(stableCode));
            Assert.That(session.LastEventQuery.Filter.ConfigId, Is.EqualTo(101));
            Assert.That(
                session.LastEventQuery.Filter.TriggerStage,
                Is.EqualTo(BattleDiagnosticTriggerAnalysisStage.Unknown));
            Assert.That(
                session.LastEventQuery.Filter.TriggerResult,
                Is.EqualTo(BattleDiagnosticTriggerAnalysisResult.Unknown));
        }

        [Test]
        public void EventsWorkset_LoadMoreUsesFixedRevisionAndExpandsIssueRange()
        {
            var failure = new BattleDiagnosticTriggerAnalysisPayload(
                7001,
                contextKind: 2,
                originKind: 3,
                BattleDiagnosticTriggerAnalysisStage.Conditions,
                BattleDiagnosticTriggerAnalysisResult.Failed,
                failureKey: "missingMana",
                reason: "Mana is insufficient.");
            var events = new List<BattleDiagnosticEvent>();
            for (var frame = 201; frame >= 1; frame--)
            {
                events.Add(TriggerEvent(frame, frame, 7001, in failure));
            }

            var session = new RecordingSession
            {
                EventStoreRevision = 7,
                Events = events,
                PageEventResults = true
            };
            var viewModel = new BattleDebugDiagnosticEventsViewModel
            {
                FilterBySelectedActor = false,
                EventScope = BattleDebugDiagnosticEventScope.All,
                RecentFrameCount = 0
            };

            viewModel.RefreshIfNeeded(session, 0, false);

            Assert.That(viewModel.LoadedCount, Is.EqualTo(200));
            Assert.That(viewModel.HasMore, Is.True);
            Assert.That(viewModel.WorksetRevision, Is.EqualTo(7));
            Assert.That(session.LastEventQuery.Page.Offset, Is.Zero);
            Assert.That(session.LastEventQuery.Page.Limit, Is.EqualTo(200));
            Assert.That(session.LastEventQuery.Page.StoreRevision, Is.EqualTo(7));

            session.EventStoreRevision = 8;
            Assert.That(viewModel.LoadMore(session, 0, false), Is.True);

            Assert.That(session.LastEventQuery.Page.Offset, Is.EqualTo(200));
            Assert.That(session.LastEventQuery.Page.StoreRevision, Is.EqualTo(7));
            Assert.That(viewModel.LoadedCount, Is.EqualTo(201));
            Assert.That(viewModel.HasMore, Is.False);
            Assert.That(viewModel.Items[0].Frame, Is.EqualTo(201));
            Assert.That(viewModel.Items[200].Frame, Is.EqualTo(1));
            Assert.That(viewModel.IssueGroups, Has.Count.EqualTo(1));
            Assert.That(viewModel.IssueGroups[0].Count, Is.EqualTo(201));
            Assert.That(viewModel.IssueGroups[0].FirstFrame, Is.EqualTo(1));
            Assert.That(viewModel.IssueGroups[0].LatestFrame, Is.EqualTo(201));
            Assert.That(viewModel.IssueGroups[0].FrameSpan, Is.EqualTo(200));
        }

        [Test]
        public void EventsWorkset_LiveRevisionRefreshRebuildsFirstPage()
        {
            var session = new RecordingSession
            {
                EventStoreRevision = 10,
                Events = new[]
                {
                    new BattleDiagnosticEvent(
                        RecordingSession.Scope,
                        10,
                        10,
                        10,
                        BattleDiagnosticEventKind.Damage,
                        BattleDiagnosticEventChannel.DamageAndHeal,
                        BattleDiagnosticEventOutcome.Succeeded,
                        summary: "old")
                },
                PageEventResults = true
            };
            var viewModel = new BattleDebugDiagnosticEventsViewModel
            {
                FilterBySelectedActor = false,
                EventScope = BattleDebugDiagnosticEventScope.All,
                RecentFrameCount = 0
            };
            viewModel.RefreshIfNeeded(session, 0, false);

            session.EventStoreRevision = 11;
            session.Events = new[]
            {
                new BattleDiagnosticEvent(
                    RecordingSession.Scope,
                    20,
                    20,
                    20,
                    BattleDiagnosticEventKind.Damage,
                    BattleDiagnosticEventChannel.DamageAndHeal,
                    BattleDiagnosticEventOutcome.Succeeded,
                    summary: "new")
            };
            viewModel.RefreshIfNeeded(session, 0, false);

            Assert.That(viewModel.WorksetRevision, Is.EqualTo(11));
            Assert.That(viewModel.LoadedCount, Is.EqualTo(1));
            Assert.That(viewModel.Items[0].Summary, Is.EqualTo("new"));
            Assert.That(session.LastEventQuery.Page.Offset, Is.Zero);
            Assert.That(session.LastEventQuery.Page.StoreRevision, Is.EqualTo(11));
        }

        [Test]
        public void EventsWorkset_EvictedRevisionKeepsLoadedItems()
        {
            var events = new List<BattleDiagnosticEvent>();
            for (var frame = 201; frame >= 1; frame--)
            {
                events.Add(new BattleDiagnosticEvent(
                    RecordingSession.Scope,
                    frame,
                    frame,
                    frame,
                    BattleDiagnosticEventKind.Damage,
                    BattleDiagnosticEventChannel.DamageAndHeal,
                    BattleDiagnosticEventOutcome.Succeeded));
            }

            var session = new RecordingSession
            {
                EventStoreRevision = 12,
                Events = events,
                PageEventResults = true
            };
            var viewModel = new BattleDebugDiagnosticEventsViewModel
            {
                FilterBySelectedActor = false,
                EventScope = BattleDebugDiagnosticEventScope.All,
                RecentFrameCount = 0
            };
            viewModel.RefreshIfNeeded(session, 0, false);
            session.EvictedEventRevision = 12;
            session.EventStoreRevision = 13;

            Assert.That(viewModel.LoadMore(session, 0, false), Is.False);

            Assert.That(viewModel.LoadedCount, Is.EqualTo(200));
            Assert.That(viewModel.Items[0].Frame, Is.EqualTo(201));
            Assert.That(viewModel.HasMore, Is.False);
            Assert.That(viewModel.WorksetRevision, Is.EqualTo(12));
            Assert.That(viewModel.PagingStatusMessage, Does.Contain("已被淘汰"));
            Assert.That(viewModel.PagingStatusMessage, Does.Contain("已保留 200 条"));
        }

        [Test]
        public void EventsFailurePresets_BuildExpectedQueries()
        {
            var session = new RecordingSession();
            var viewModel = new BattleDebugDiagnosticEventsViewModel();

            viewModel.FocusRecentFailures();
            viewModel.RefreshIfNeeded(session, 10, true);
            Assert.That(session.LastEventQuery.Filter.FailuresOnly, Is.True);
            Assert.That(session.LastEventQuery.Filter.Channels, Is.EqualTo(BattleDiagnosticEventChannel.All));
            Assert.That(session.LastEventQuery.RecentFrameCount, Is.EqualTo(600));

            viewModel.FocusConditionFailures();
            viewModel.RefreshIfNeeded(session, 10, true);
            Assert.That(session.LastEventQuery.Filter.Channels, Is.EqualTo(BattleDiagnosticEventChannel.Effect));
            Assert.That(session.LastEventQuery.Filter.TriggerStage, Is.EqualTo(BattleDiagnosticTriggerAnalysisStage.Conditions));
            Assert.That(session.LastEventQuery.Filter.TriggerResult, Is.EqualTo(BattleDiagnosticTriggerAnalysisResult.Failed));

            viewModel.FocusTriggerBlocks();
            viewModel.RefreshIfNeeded(session, 10, true);
            Assert.That(session.LastEventQuery.Filter.Channels, Is.EqualTo(BattleDiagnosticEventChannel.Effect));
            Assert.That(session.LastEventQuery.Filter.TriggerStage, Is.EqualTo(BattleDiagnosticTriggerAnalysisStage.Budget));
            Assert.That(session.LastEventQuery.Filter.TriggerResult, Is.EqualTo(BattleDiagnosticTriggerAnalysisResult.Blocked));
        }

        [Test]
        public void Events_DefaultQuery_FocusesRecentDamageAndEffectsNewestFirst()
        {
            var session = new RecordingSession();
            var viewModel = new BattleDebugDiagnosticEventsViewModel();

            viewModel.RefreshIfNeeded(session, 12, true);

            Assert.That(session.LastEventQuery.NewestFirst, Is.True);
            Assert.That(session.LastEventQuery.RecentFrameCount, Is.EqualTo(600));
            Assert.That(
                session.LastEventQuery.Filter.Channels,
                Is.EqualTo(BattleDiagnosticEventChannel.DamageAndHeal | BattleDiagnosticEventChannel.Effect));
            Assert.That(session.LastEventQuery.Filter.ActorId, Is.EqualTo(12));
            Assert.That(session.LastEventQuery.Filter.ActorRelation, Is.EqualTo(BattleDiagnosticActorRelation.Either));
        }

        [Test]
        public void EventsOpenEvent_PinsExactEventAndUpdatesWorkspaceSelection()
        {
            var diagnosticEvent = new BattleDiagnosticEvent(
                RecordingSession.Scope,
                42,
                7,
                8,
                BattleDiagnosticEventKind.BuffRemoved,
                BattleDiagnosticEventChannel.Buff,
                BattleDiagnosticEventOutcome.Succeeded,
                targetActorId: 12,
                configId: 13,
                rootContextId: 14,
                contextId: 15);
            var workspace = new BattleDiagnosticWorkspaceState();
            workspace.AttachSession(RecordingSession.Scope, 50);
            var panel = new BattleDebugDiagnosticEventsPanel();

            panel.OpenEvent(in diagnosticEvent, workspace);

            Assert.That(panel.SelectedEvent, Is.EqualTo(diagnosticEvent));
            Assert.That(workspace.Selection.Kind, Is.EqualTo(BattleDiagnosticSelectionKind.Event));
            Assert.That(workspace.Selection.Id, Is.EqualTo(7));
            Assert.That(workspace.Selection.Frame, Is.EqualTo(42));
            Assert.That(workspace.Selection.RelatedId, Is.EqualTo(14));
            Assert.That(workspace.FrameCursor.Frame, Is.EqualTo(42));
            Assert.That(workspace.FrameCursor.FollowsLive, Is.False);
            Assert.That(workspace.Navigation.Count, Is.EqualTo(1));
        }

        [Test]
        public void EventsClipboardText_IncludesNavigationAndCorrelationFields()
        {
            var diagnosticEvent = new BattleDiagnosticEvent(
                RecordingSession.Scope,
                42,
                7,
                8,
                BattleDiagnosticEventKind.Damage,
                BattleDiagnosticEventChannel.DamageAndHeal,
                BattleDiagnosticEventOutcome.Succeeded,
                sourceActorId: 11,
                targetActorId: 12,
                configId: 13,
                rootContextId: 14,
                contextId: 15,
                skillRuntime: new BattleDiagnosticRuntimeHandle(16, 2),
                attackId: 17,
                summary: "damage result");

            var text = BattleDebugDiagnosticEventsPanel.BuildClipboardText(in diagnosticEvent);

            Assert.That(text, Does.Contain("Sequence=7"));
            Assert.That(text, Does.Contain("Frame=42"));
            Assert.That(text, Does.Contain("SourceActorId=11"));
            Assert.That(text, Does.Contain("TargetActorId=12"));
            Assert.That(text, Does.Contain("RootContextId=14"));
            Assert.That(text, Does.Contain("ContextId=15"));
            Assert.That(text, Does.Contain("SkillRuntime=16:2"));
            Assert.That(text, Does.Contain("AttackId=17"));
            Assert.That(text, Does.EndWith("Summary=damage result"));
        }

        [Test]
        public void EventsClipboardText_IncludesTriggerAnalysisPayload()
        {
            var trigger = new BattleDiagnosticTriggerAnalysisPayload(
                701,
                contextKind: 2,
                originKind: 3,
                BattleDiagnosticTriggerAnalysisStage.Conditions,
                BattleDiagnosticTriggerAnalysisResult.Failed,
                detailCode: 11,
                currentDepth: 1,
                currentFrameCount: 2,
                currentRootCount: 3,
                currentSameTriggerCount: 4,
                failureKey: "missingMana",
                reason: "Missing mana for trigger.");
            var payload = BattleDiagnosticEventPayload.FromTriggerAnalysis(in trigger);
            var diagnosticEvent = new BattleDiagnosticEvent(
                RecordingSession.Scope,
                42,
                7,
                8,
                BattleDiagnosticEventKind.TriggerAnalysis,
                BattleDiagnosticEventChannel.Effect,
                BattleDiagnosticEventOutcome.Failed,
                configId: 701,
                payloadVersion: BattleDiagnosticTriggerAnalysisPayload.CurrentSchemaVersion,
                payload: payload);

            var text = BattleDebugDiagnosticEventsPanel.BuildClipboardText(in diagnosticEvent);

            Assert.That(text, Does.Contain("PayloadKind=TriggerAnalysis"));
            Assert.That(text, Does.Contain("TriggerId=701"));
            Assert.That(text, Does.Contain("TriggerStage=Conditions"));
            Assert.That(text, Does.Contain("TriggerResult=Failed"));
            Assert.That(text, Does.Contain("TriggerFailureKey=missingMana"));
            Assert.That(text, Does.Contain("TriggerReason=Missing mana for trigger."));
        }

        [Test]
        public void EventsClipboardText_IncludesSkillFailurePayload()
        {
            var failure = new BattleDiagnosticSkillFailurePayload(
                slot: 2,
                source: "Cast",
                stage: "Preparation",
                code: "Cast.TargetOutOfRange",
                message: "Target is outside cast range.");
            var payload = BattleDiagnosticEventPayload.FromSkillFailure(in failure);
            var diagnosticEvent = new BattleDiagnosticEvent(
                RecordingSession.Scope,
                42,
                7,
                8,
                BattleDiagnosticEventKind.SkillFailure,
                BattleDiagnosticEventChannel.Skill,
                BattleDiagnosticEventOutcome.Failed,
                configId: 701,
                payloadVersion: BattleDiagnosticSkillFailurePayload.CurrentSchemaVersion,
                payload: payload);

            var text = BattleDebugDiagnosticEventsPanel.BuildClipboardText(in diagnosticEvent);

            Assert.That(text, Does.Contain("PayloadKind=SkillFailure"));
            Assert.That(text, Does.Contain("PayloadSchemaVersion=1"));
            Assert.That(text, Does.Contain("SkillFailureSlot=2"));
            Assert.That(text, Does.Contain("SkillFailureSource=Cast"));
            Assert.That(text, Does.Contain("SkillFailureStage=Preparation"));
            Assert.That(text, Does.Contain("SkillFailureCode=Cast.TargetOutOfRange"));
            Assert.That(text, Does.Contain("SkillFailureMessage=Target is outside cast range."));
        }

        [Test]
        public void EventsClipboardText_IncludesBuffLifecyclePayload()
        {
            var lifecycle = new BattleDiagnosticBuffLifecyclePayload(
                BattleDiagnosticBuffLifecycleStage.Removed,
                stackCount: 3,
                previousStackCount: 4,
                durationMilliseconds: 12000,
                remainingMilliseconds: 250,
                intervalRemainingMilliseconds: 750,
                maxStacks: 5,
                modifierBindingCount: 2,
                modifierSourceId: 77,
                removeReason: 9);
            var payload = BattleDiagnosticEventPayload.FromBuffLifecycle(in lifecycle);
            var diagnosticEvent = new BattleDiagnosticEvent(
                RecordingSession.Scope,
                42,
                7,
                8,
                BattleDiagnosticEventKind.BuffRemoved,
                BattleDiagnosticEventChannel.Buff,
                BattleDiagnosticEventOutcome.Succeeded,
                configId: 701,
                payloadVersion: BattleDiagnosticBuffLifecyclePayload.CurrentSchemaVersion,
                payload: payload);

            var text = BattleDebugDiagnosticEventsPanel.BuildClipboardText(in diagnosticEvent);

            Assert.That(text, Does.Contain("PayloadKind=BuffLifecycle"));
            Assert.That(text, Does.Contain("PayloadSchemaVersion=1"));
            Assert.That(text, Does.Contain("BuffLifecycleStage=Removed"));
            Assert.That(text, Does.Contain("BuffLifecycleStackCount=3"));
            Assert.That(text, Does.Contain("BuffLifecyclePreviousStackCount=4"));
            Assert.That(text, Does.Contain("BuffLifecycleDurationMilliseconds=12000"));
            Assert.That(text, Does.Contain("BuffLifecycleRemainingMilliseconds=250"));
            Assert.That(text, Does.Contain("BuffLifecycleIntervalRemainingMilliseconds=750"));
            Assert.That(text, Does.Contain("BuffLifecycleMaxStacks=5"));
            Assert.That(text, Does.Contain("BuffLifecycleModifierBindingCount=2"));
            Assert.That(text, Does.Contain("BuffLifecycleModifierSourceId=77"));
            Assert.That(text, Does.Contain("BuffLifecycleRemoveReason=9"));
        }

        [Test]
        public void EventsFocusRelated_PrefersRootTraceAndResetsCompetingFilters()
        {
            var session = new RecordingSession();
            var viewModel = new BattleDebugDiagnosticEventsViewModel
            {
                FilterBySelectedActor = true,
                FailuresOnly = true,
                EventScope = BattleDebugDiagnosticEventScope.Warnings,
                RecentFrameCount = 120,
                SearchText = "failed"
            };
            var diagnosticEvent = new BattleDiagnosticEvent(
                RecordingSession.Scope,
                10,
                1,
                1,
                BattleDiagnosticEventKind.SkillRuntimeStarted,
                BattleDiagnosticEventChannel.Skill,
                BattleDiagnosticEventOutcome.Succeeded,
                rootContextId: 100,
                contextId: 110,
                skillRuntime: new BattleDiagnosticRuntimeHandle(120, 0),
                attackId: 130);

            Assert.That(viewModel.FocusRelated(in diagnosticEvent), Is.True);
            viewModel.RefreshIfNeeded(session, 99, true);

            Assert.That(viewModel.CorrelationFocusLabel, Is.EqualTo("Root Trace=100"));
            Assert.That(viewModel.FilterBySelectedActor, Is.False);
            Assert.That(viewModel.FailuresOnly, Is.False);
            Assert.That(viewModel.EventScope, Is.EqualTo(BattleDebugDiagnosticEventScope.All));
            Assert.That(viewModel.RecentFrameCount, Is.Zero);
            Assert.That(viewModel.SearchText, Is.Empty);
            Assert.That(session.LastEventQuery.Filter.RootContextId, Is.EqualTo(100));
            Assert.That(session.LastEventQuery.Filter.ContextId, Is.Zero);
            Assert.That(session.LastEventQuery.Filter.SkillRuntimeId, Is.Zero);
            Assert.That(session.LastEventQuery.Filter.AttackId, Is.Zero);
            Assert.That(session.LastEventQuery.Filter.ActorId, Is.Zero);
        }

        [Test]
        public void EventsFocusRelated_FallsBackThroughRuntimeAttackAndContext()
        {
            var viewModel = new BattleDebugDiagnosticEventsViewModel();
            var runtimeEvent = new BattleDiagnosticEvent(
                RecordingSession.Scope, 10, 1, 1,
                BattleDiagnosticEventKind.SkillRuntimeStarted,
                BattleDiagnosticEventChannel.Skill,
                BattleDiagnosticEventOutcome.Succeeded,
                contextId: 110,
                skillRuntime: new BattleDiagnosticRuntimeHandle(120, 0),
                attackId: 130);
            var attackEvent = new BattleDiagnosticEvent(
                RecordingSession.Scope, 10, 2, 2,
                BattleDiagnosticEventKind.Damage,
                BattleDiagnosticEventChannel.DamageAndHeal,
                BattleDiagnosticEventOutcome.Succeeded,
                contextId: 110,
                attackId: 130);
            var contextEvent = new BattleDiagnosticEvent(
                RecordingSession.Scope, 10, 3, 3,
                BattleDiagnosticEventKind.EffectStarted,
                BattleDiagnosticEventChannel.Effect,
                BattleDiagnosticEventOutcome.Succeeded,
                contextId: 110);

            Assert.That(viewModel.FocusRelated(in runtimeEvent), Is.True);
            Assert.That(viewModel.SkillRuntimeId, Is.EqualTo(120));
            Assert.That(viewModel.AttackId, Is.Zero);
            Assert.That(viewModel.ContextId, Is.Zero);

            Assert.That(viewModel.FocusRelated(in attackEvent), Is.True);
            Assert.That(viewModel.SkillRuntimeId, Is.Zero);
            Assert.That(viewModel.AttackId, Is.EqualTo(130));
            Assert.That(viewModel.ContextId, Is.Zero);

            Assert.That(viewModel.FocusRelated(in contextEvent), Is.True);
            Assert.That(viewModel.AttackId, Is.Zero);
            Assert.That(viewModel.ContextId, Is.EqualTo(110));
        }

        [Test]
        public void EventsCorrelationFocus_NoIdentifierReturnsFalse_AndClearInvalidatesCache()
        {
            var session = new RecordingSession();
            var viewModel = new BattleDebugDiagnosticEventsViewModel();
            var noCorrelationEvent = new BattleDiagnosticEvent(
                RecordingSession.Scope, 10, 1, 1,
                BattleDiagnosticEventKind.Damage,
                BattleDiagnosticEventChannel.DamageAndHeal,
                BattleDiagnosticEventOutcome.Succeeded);
            var contextEvent = new BattleDiagnosticEvent(
                RecordingSession.Scope, 10, 2, 2,
                BattleDiagnosticEventKind.EffectStarted,
                BattleDiagnosticEventChannel.Effect,
                BattleDiagnosticEventOutcome.Succeeded,
                contextId: 110);

            Assert.That(viewModel.FocusRelated(in noCorrelationEvent), Is.False);
            Assert.That(viewModel.HasCorrelationFocus, Is.False);

            Assert.That(viewModel.FocusRelated(in contextEvent), Is.True);
            viewModel.RefreshIfNeeded(session, 0, false);
            Assert.That(session.EventQueryCount, Is.EqualTo(1));
            Assert.That(session.LastEventQuery.Filter.ContextId, Is.EqualTo(110));

            viewModel.ClearCorrelationFocus();
            Assert.That(viewModel.HasCorrelationFocus, Is.False);
            Assert.That(viewModel.CorrelationFocusLabel, Is.Empty);
            viewModel.RefreshIfNeeded(session, 0, false);

            Assert.That(session.EventQueryCount, Is.EqualTo(2));
            Assert.That(session.LastEventQuery.Filter.HasCorrelationFilter, Is.False);
        }

        [Test]
        public void EventsCache_IgnoresStateRevision()
        {
            var session = new RecordingSession();
            var viewModel = new BattleDebugDiagnosticEventsViewModel();
            viewModel.RefreshIfNeeded(session, 0, false);

            session.StateStoreRevision++;
            viewModel.RefreshIfNeeded(session, 0, false);

            Assert.That(session.EventQueryCount, Is.EqualTo(1));
        }

        [Test]
        public void StateCacheKey_IncludesStateRevisionAndFrameInput()
        {
            var session = new RecordingSession();
            var viewModel = new BattleDebugDiagnosticStateViewModel();

            viewModel.RefreshIfNeeded(session);
            Assert.That(session.WorldQueryCount, Is.EqualTo(1));
            Assert.That(session.ActorQueryCount, Is.EqualTo(1));

            viewModel.RefreshIfNeeded(session);
            Assert.That(session.WorldQueryCount, Is.EqualTo(1));

            session.StateStoreRevision++;
            viewModel.RefreshIfNeeded(session);
            Assert.That(session.WorldQueryCount, Is.EqualTo(2));

            viewModel.FrameInput = 5;
            viewModel.RefreshIfNeeded(session);
            Assert.That(session.WorldQueryCount, Is.EqualTo(3));
            Assert.That(session.LastWorldFrame, Is.EqualTo(5));
            Assert.That(session.LastActorFrame, Is.EqualTo(5));
        }

        [Test]
        public void StateCache_IgnoresEventRevisionAndCachesUnavailableResult()
        {
            var session = new RecordingSession();
            var viewModel = new BattleDebugDiagnosticStateViewModel();
            viewModel.RefreshIfNeeded(session);

            session.EventStoreRevision++;
            viewModel.RefreshIfNeeded(session);

            Assert.That(session.WorldQueryCount, Is.EqualTo(1));
            Assert.That(session.ActorQueryCount, Is.EqualTo(1));
        }

        [Test]
        public void AttributeCacheKey_IncludesAttributeRevisionActorAndFrame()
        {
            var session = new RecordingSession();
            var viewModel = new BattleDebugDiagnosticAttributesViewModel();

            viewModel.RefreshIfNeeded(session, 11);
            viewModel.RefreshIfNeeded(session, 11);
            Assert.That(session.AttributeQueryCount, Is.EqualTo(1));
            Assert.That(session.ModifierQueryCount, Is.EqualTo(1));

            session.StateStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);
            Assert.That(session.AttributeQueryCount, Is.EqualTo(1));

            session.ActorAttributeStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);
            viewModel.RefreshIfNeeded(session, 12);
            viewModel.RefreshIfNeeded(session, 12, 5);

            Assert.That(session.AttributeQueryCount, Is.EqualTo(4));
            Assert.That(session.ModifierQueryCount, Is.EqualTo(4));
            Assert.That(session.LastAttributeActorId, Is.EqualTo(12));
            Assert.That(session.LastAttributeFrame, Is.EqualTo(5));
        }

        [Test]
        public void BuffCacheKey_TracksBuffAndEventRevisionsIndependently()
        {
            var session = new RecordingSession();
            var viewModel = new BattleDebugDiagnosticBuffsViewModel();

            viewModel.RefreshIfNeeded(session, 11);
            viewModel.RefreshIfNeeded(session, 11);
            Assert.That(session.BuffQueryCount, Is.EqualTo(1));
            Assert.That(session.EventQueryCount, Is.EqualTo(1));

            session.StateStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);
            Assert.That(session.BuffQueryCount, Is.EqualTo(1));
            Assert.That(session.EventQueryCount, Is.EqualTo(1));

            session.ActorBuffStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);
            Assert.That(session.BuffQueryCount, Is.EqualTo(2));
            Assert.That(session.EventQueryCount, Is.EqualTo(1));

            session.EventStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);
            Assert.That(session.BuffQueryCount, Is.EqualTo(2));
            Assert.That(session.EventQueryCount, Is.EqualTo(2));

            viewModel.RefreshIfNeeded(session, 12);
            viewModel.RefreshIfNeeded(session, 12, 5);

            Assert.That(session.BuffQueryCount, Is.EqualTo(4));
            Assert.That(session.EventQueryCount, Is.EqualTo(4));
            Assert.That(session.LastBuffActorId, Is.EqualTo(12));
            Assert.That(session.LastBuffFrame, Is.EqualTo(5));
        }

        [Test]
        public void BuffTimeline_QueriesTargetActorAndKeepsOnlyStructuredLifecycleEvents()
        {
            var lifecycle = new BattleDiagnosticBuffLifecyclePayload(
                BattleDiagnosticBuffLifecycleStage.Applied,
                stackCount: 1,
                previousStackCount: 0,
                durationMilliseconds: 5000,
                remainingMilliseconds: 5000,
                intervalRemainingMilliseconds: 1000,
                maxStacks: 3,
                modifierBindingCount: 2,
                modifierSourceId: 77,
                removeReason: 0);
            var lifecyclePayload = BattleDiagnosticEventPayload.FromBuffLifecycle(in lifecycle);
            var session = new RecordingSession
            {
                EventStoreRevision = 6,
                Events = new[]
                {
                    new BattleDiagnosticEvent(
                        RecordingSession.Scope,
                        10,
                        1,
                        100,
                        BattleDiagnosticEventKind.BuffAdded,
                        BattleDiagnosticEventChannel.Buff,
                        BattleDiagnosticEventOutcome.Succeeded,
                        sourceActorId: 7,
                        targetActorId: 11,
                        configId: 301,
                        payloadVersion: BattleDiagnosticBuffLifecyclePayload.CurrentSchemaVersion,
                        payload: lifecyclePayload),
                    new BattleDiagnosticEvent(
                        RecordingSession.Scope,
                        10,
                        2,
                        101,
                        BattleDiagnosticEventKind.BuffAdded,
                        BattleDiagnosticEventChannel.Buff,
                        BattleDiagnosticEventOutcome.Succeeded,
                        targetActorId: 11),
                    new BattleDiagnosticEvent(
                        RecordingSession.Scope,
                        10,
                        3,
                        102,
                        BattleDiagnosticEventKind.Damage,
                        BattleDiagnosticEventChannel.DamageAndHeal,
                        BattleDiagnosticEventOutcome.Succeeded,
                        targetActorId: 11)
                }
            };
            var viewModel = new BattleDebugDiagnosticBuffsViewModel();

            viewModel.RefreshIfNeeded(session, 11);

            Assert.That(viewModel.TimelineEvents.Count, Is.EqualTo(1));
            Assert.That(viewModel.TimelineEvents[0].Sequence, Is.EqualTo(1));
            Assert.That(session.LastEventQuery.Filter.Channels, Is.EqualTo(BattleDiagnosticEventChannel.Buff));
            Assert.That(session.LastEventQuery.Filter.ActorId, Is.EqualTo(11));
            Assert.That(session.LastEventQuery.Filter.ActorRelation, Is.EqualTo(BattleDiagnosticActorRelation.Target));
            Assert.That(session.LastEventQuery.Page.StoreRevision, Is.EqualTo(6));
            Assert.That(session.LastEventQuery.NewestFirst, Is.True);
        }

        [Test]
        public void BuffTimeline_ScansNextPageForStructuredLifecycleEventsAtFixedRevision()
        {
            const long eventRevision = 9;
            var events = new List<BattleDiagnosticEvent>();
            for (var i = 0; i < 128; i++)
            {
                events.Add(new BattleDiagnosticEvent(
                    RecordingSession.Scope,
                    200 - i,
                    i + 1,
                    1000 + i,
                    BattleDiagnosticEventKind.BuffAdded,
                    BattleDiagnosticEventChannel.Buff,
                    BattleDiagnosticEventOutcome.Succeeded,
                    targetActorId: 11,
                    configId: 301));
            }

            var lifecycle = new BattleDiagnosticBuffLifecyclePayload(
                BattleDiagnosticBuffLifecycleStage.Removed,
                stackCount: 1,
                previousStackCount: 1,
                durationMilliseconds: 5000,
                remainingMilliseconds: 0,
                intervalRemainingMilliseconds: 0,
                maxStacks: 3,
                modifierBindingCount: 2,
                modifierSourceId: 77,
                removeReason: 8);
            var lifecyclePayload = BattleDiagnosticEventPayload.FromBuffLifecycle(in lifecycle);
            events.Add(new BattleDiagnosticEvent(
                RecordingSession.Scope,
                72,
                129,
                1129,
                BattleDiagnosticEventKind.BuffRemoved,
                BattleDiagnosticEventChannel.Buff,
                BattleDiagnosticEventOutcome.Succeeded,
                targetActorId: 11,
                configId: 301,
                payloadVersion: BattleDiagnosticBuffLifecyclePayload.CurrentSchemaVersion,
                payload: lifecyclePayload));
            var session = new RecordingSession
            {
                EventStoreRevision = eventRevision,
                Events = events,
                PageEventResults = true
            };
            var viewModel = new BattleDebugDiagnosticBuffsViewModel();

            viewModel.RefreshIfNeeded(session, 11);

            Assert.That(viewModel.TimelineEvents.Count, Is.EqualTo(1));
            Assert.That(viewModel.TimelineEvents[0].Sequence, Is.EqualTo(129));
            Assert.That(session.EventQueryCount, Is.EqualTo(2));
            Assert.That(session.LastEventQuery.Page.StoreRevision, Is.EqualTo(eventRevision));
            Assert.That(session.LastEventQuery.Page.Offset, Is.EqualTo(128));
            Assert.That(session.LastEventQuery.NewestFirst, Is.True);
            Assert.That(viewModel.EventQueryStatus.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Ready));
            Assert.That(viewModel.EventStatusMessage, Is.Empty);
        }

        [Test]
        public void TagCacheKey_IncludesTagRevisionActorAndFrame()
        {
            var session = new RecordingSession();
            var viewModel = new BattleDebugDiagnosticTagsViewModel();

            viewModel.RefreshIfNeeded(session, 11);
            viewModel.RefreshIfNeeded(session, 11);
            Assert.That(session.TagQueryCount, Is.EqualTo(1));

            session.StateStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);
            Assert.That(session.TagQueryCount, Is.EqualTo(1));

            session.ActorTagStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);
            viewModel.RefreshIfNeeded(session, 12);
            viewModel.RefreshIfNeeded(session, 12, 5);

            Assert.That(session.TagQueryCount, Is.EqualTo(4));
            Assert.That(session.LastTagActorId, Is.EqualTo(12));
            Assert.That(session.LastTagFrame, Is.EqualTo(5));
        }

        [Test]
        public void EffectCacheKey_IncludesEffectRevisionActorAndFrame()
        {
            var session = new RecordingSession();
            var viewModel = new BattleDebugDiagnosticEffectsViewModel();

            viewModel.RefreshIfNeeded(session, 11);
            viewModel.RefreshIfNeeded(session, 11);
            Assert.That(session.EffectQueryCount, Is.EqualTo(1));

            session.StateStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);
            Assert.That(session.EffectQueryCount, Is.EqualTo(1));

            session.ActorEffectStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);
            viewModel.RefreshIfNeeded(session, 12);
            viewModel.RefreshIfNeeded(session, 12, 5);

            Assert.That(session.EffectQueryCount, Is.EqualTo(4));
            Assert.That(session.LastEffectActorId, Is.EqualTo(12));
            Assert.That(session.LastEffectFrame, Is.EqualTo(5));
        }

        [Test]
        public void ActorDtoViewModels_PreserveQueryStatusAndClearItWhenInvalidated()
        {
            var session = new RecordingSession();
            var attributes = new BattleDebugDiagnosticAttributesViewModel();
            var buffs = new BattleDebugDiagnosticBuffsViewModel();
            var tags = new BattleDebugDiagnosticTagsViewModel();
            var effects = new BattleDebugDiagnosticEffectsViewModel();

            attributes.RefreshIfNeeded(session, 11);
            buffs.RefreshIfNeeded(session, 11);
            tags.RefreshIfNeeded(session, 11);
            effects.RefreshIfNeeded(session, 11);

            Assert.That(attributes.AttributeQueryStatus.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.NotProduced));
            Assert.That(attributes.ModifierQueryStatus.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.NotProduced));
            Assert.That(buffs.QueryStatus.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.NotProduced));
            Assert.That(tags.QueryStatus.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.NotProduced));
            Assert.That(effects.QueryStatus.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.NotProduced));

            attributes.InvalidateCache();
            buffs.InvalidateCache();
            tags.InvalidateCache();
            effects.InvalidateCache();

            Assert.That(attributes.AttributeQueryStatus, Is.EqualTo(default(BattleDiagnosticQueryStatus)));
            Assert.That(attributes.ModifierQueryStatus, Is.EqualTo(default(BattleDiagnosticQueryStatus)));
            Assert.That(buffs.QueryStatus, Is.EqualTo(default(BattleDiagnosticQueryStatus)));
            Assert.That(tags.QueryStatus, Is.EqualTo(default(BattleDiagnosticQueryStatus)));
            Assert.That(effects.QueryStatus, Is.EqualTo(default(BattleDiagnosticQueryStatus)));
        }

        [Test]
        public void OverviewCacheKey_IncludesAllRevisionsActorAndFrame()
        {
            var session = new RecordingSession();
            var viewModel = new BattleDebugDiagnosticOverviewViewModel();

            viewModel.RefreshIfNeeded(session, 11);
            viewModel.RefreshIfNeeded(session, 11);
            Assert.That(session.ActorQueryCount, Is.EqualTo(1));
            Assert.That(session.TagQueryCount, Is.EqualTo(1));
            Assert.That(session.EffectQueryCount, Is.EqualTo(1));

            session.StateStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);
            session.ActorTagStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);
            session.ActorEffectStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);
            viewModel.RefreshIfNeeded(session, 12);
            viewModel.RefreshIfNeeded(session, 12, 5);

            Assert.That(session.ActorQueryCount, Is.EqualTo(6));
            Assert.That(session.TagQueryCount, Is.EqualTo(6));
            Assert.That(session.EffectQueryCount, Is.EqualTo(6));
            Assert.That(session.LastActorFrame, Is.EqualTo(5));
            Assert.That(session.LastTagActorId, Is.EqualTo(12));
            Assert.That(session.LastEffectActorId, Is.EqualTo(12));
        }

        [Test]
        public void Overview_ProjectsSelectedActorCountsAndTagClipboardText()
        {
            var session = new RecordingSession
            {
                Actors = new[]
                {
                    new BattleDiagnosticActorSummary(
                        RecordingSession.Scope, 7, 10, BattleDiagnosticActorKind.Minion,
                        100, 2, 0, 0, 0, 20, 20, true, "Other"),
                    new BattleDiagnosticActorSummary(
                        RecordingSession.Scope, 7, 11, BattleDiagnosticActorKind.Hero,
                        200, 1, 1, 2, 3, 80, 100, true, "Selected")
                },
                Tags = new[]
                {
                    new BattleDiagnosticActorTag(RecordingSession.Scope, 7, 11, 1001, "State.Stunned"),
                    new BattleDiagnosticActorTag(RecordingSession.Scope, 7, 11, 1002)
                },
                Effects = new[]
                {
                    new BattleDiagnosticActorEffect(
                        RecordingSession.Scope, 7, 11, 1,
                        BattleDiagnosticEffectDurationPolicy.Infinite, 1,
                        0, 0, false, 0, false, 0, 0, 0, false)
                }
            };
            var viewModel = new BattleDebugDiagnosticOverviewViewModel();

            viewModel.RefreshIfNeeded(session, 11, 7);

            Assert.That(viewModel.Actor.HasValue, Is.True);
            Assert.That(viewModel.Actor.Value.DisplayName, Is.EqualTo("Selected"));
            Assert.That(viewModel.TagCount, Is.EqualTo(2));
            Assert.That(viewModel.EffectCount, Is.EqualTo(1));
            Assert.That(viewModel.BuildTagList(), Is.EqualTo("State.Stunned\n1002"));
            Assert.That(viewModel.StatusMessage, Is.Empty);
        }

        [Test]
        public void Overview_ProjectsMostRecentActorEvent_AndRefreshesOnEventRevision()
        {
            var session = new RecordingSession
            {
                EventStoreRevision = 3,
                Events = new[]
                {
                    new BattleDiagnosticEvent(
                        RecordingSession.Scope, 4, 7, 7,
                        BattleDiagnosticEventKind.SkillRuntimeStarted,
                        BattleDiagnosticEventChannel.Skill,
                        BattleDiagnosticEventOutcome.Succeeded,
                        sourceActorId: 11,
                        rootContextId: 70,
                        contextId: 71,
                        summary: "Earlier"),
                    new BattleDiagnosticEvent(
                        RecordingSession.Scope, 5, 9, 9,
                        BattleDiagnosticEventKind.SkillRuntimeEnded,
                        BattleDiagnosticEventChannel.Skill,
                        BattleDiagnosticEventOutcome.Failed,
                        targetActorId: 11,
                        rootContextId: 90,
                        contextId: 91,
                        summary: "Latest")
                }
            };
            var viewModel = new BattleDebugDiagnosticOverviewViewModel();

            viewModel.RefreshIfNeeded(session, 11);

            Assert.That(viewModel.RecentEvent.HasValue, Is.True);
            Assert.That(viewModel.RecentEvent.Value.Sequence, Is.EqualTo(9));
            Assert.That(viewModel.RecentEvent.Value.Summary, Is.EqualTo("Latest"));
            Assert.That(session.EventQueryCount, Is.EqualTo(1));

            session.EventStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);

            Assert.That(session.EventQueryCount, Is.EqualTo(2));
        }

        [Test]
        public void TraceCacheKey_IncludesScopeRevisionAndRootButIgnoresOtherRevisions()
        {
            var session = new RecordingSession { TraceNodes = new[] { TraceNode(100, 100, 0, "Root") } };
            var viewModel = new BattleDebugDiagnosticTraceViewModel();

            viewModel.RefreshIfNeeded(session, 100);
            viewModel.RefreshIfNeeded(session, 100);
            Assert.That(session.TraceQueryCount, Is.EqualTo(1));

            session.EventStoreRevision++;
            session.StateStoreRevision++;
            viewModel.RefreshIfNeeded(session, 100);
            Assert.That(session.TraceQueryCount, Is.EqualTo(1));

            session.TraceStoreRevision++;
            viewModel.RefreshIfNeeded(session, 100);
            viewModel.RefreshIfNeeded(session, 200);

            Assert.That(session.TraceQueryCount, Is.EqualTo(3));
            Assert.That(session.LastTraceRootContextId, Is.EqualTo(200));

            var otherSession = new RecordingSession(2) { TraceNodes = session.TraceNodes };
            viewModel.RefreshIfNeeded(otherSession, 200);
            Assert.That(otherSession.TraceQueryCount, Is.EqualTo(1));
        }

        [Test]
        public void Trace_ProjectsDepthOrphansAndSelectedParentPath()
        {
            var session = new RecordingSession
            {
                TraceNodes = new[]
                {
                    TraceNode(100, 100, 0, "Root"),
                    TraceNode(100, 110, 100, "Skill"),
                    TraceNode(100, 111, 110, "Effect"),
                    TraceNode(100, 120, 999, "Orphan")
                }
            };
            var viewModel = new BattleDebugDiagnosticTraceViewModel();

            viewModel.RefreshIfNeeded(session, 100);

            Assert.That(viewModel.Rows.Count, Is.EqualTo(4));
            Assert.That(viewModel.Rows[0].Depth, Is.EqualTo(0));
            Assert.That(viewModel.Rows[1].Depth, Is.EqualTo(1));
            Assert.That(viewModel.Rows[2].Depth, Is.EqualTo(2));
            Assert.That(viewModel.Rows[3].Depth, Is.EqualTo(0));
            Assert.That(viewModel.Rows[3].IsOrphan, Is.True);
            Assert.That(viewModel.SelectedContextId, Is.EqualTo(100));

            Assert.That(viewModel.SelectContext(111), Is.True);
            Assert.That(viewModel.SelectedPath.Count, Is.EqualTo(3));
            Assert.That(viewModel.SelectedPath[0].ContextId, Is.EqualTo(100));
            Assert.That(viewModel.SelectedPath[1].ContextId, Is.EqualTo(110));
            Assert.That(viewModel.SelectedPath[2].ContextId, Is.EqualTo(111));
            Assert.That(viewModel.SelectContext(999), Is.False);
        }

        [Test]
        public void TraceRevision_WhenSelectedNodeDisappears_FallsBackToRoot()
        {
            var session = new RecordingSession
            {
                TraceNodes = new[]
                {
                    TraceNode(100, 100, 0, "Root"),
                    TraceNode(100, 110, 100, "Child")
                }
            };
            var viewModel = new BattleDebugDiagnosticTraceViewModel();
            viewModel.RefreshIfNeeded(session, 100);
            viewModel.SelectContext(110);

            session.TraceStoreRevision++;
            session.TraceNodes = new[] { TraceNode(100, 100, 0, "Root") };
            viewModel.RefreshIfNeeded(session, 100);

            Assert.That(viewModel.SelectedContextId, Is.EqualTo(100));
            Assert.That(viewModel.SelectedPath.Count, Is.EqualTo(1));
            Assert.That(viewModel.SelectedPath[0].ContextId, Is.EqualTo(100));
        }

        [Test]
        public void TraceUnavailable_ClearsPreviousRowsSelectionAndPath()
        {
            var session = new RecordingSession { TraceNodes = new[] { TraceNode(100, 100, 0, "Root") } };
            var viewModel = new BattleDebugDiagnosticTraceViewModel();
            viewModel.RefreshIfNeeded(session, 100);
            Assert.That(viewModel.Rows.Count, Is.EqualTo(1));

            session.TraceStoreRevision++;
            session.TraceNodes = null;
            session.TraceAvailability = BattleDiagnosticDataAvailability.Evicted;
            viewModel.RefreshIfNeeded(session, 100);

            Assert.That(viewModel.Rows, Is.Empty);
            Assert.That(viewModel.SelectedPath, Is.Empty);
            Assert.That(viewModel.SelectedContextId, Is.Zero);
            Assert.That(viewModel.StatusMessage, Does.Contain("Evicted"));
        }

        [Test]
        public void TraceQueryStatus_IsPreservedAndClearedWithCacheInvalidation()
        {
            var session = new RecordingSession
            {
                TraceAvailability = BattleDiagnosticDataAvailability.Evicted
            };
            var viewModel = new BattleDebugDiagnosticTraceViewModel();

            viewModel.RefreshIfNeeded(session, 100);
            Assert.That(viewModel.QueryStatus.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.Evicted));

            viewModel.InvalidateCache();
            Assert.That(viewModel.QueryStatus, Is.EqualTo(default(BattleDiagnosticQueryStatus)));
            Assert.That(viewModel.StatusMessage, Is.Empty);
        }

        [Test]
        public void Trace_CyclicParents_DoNotRecurseForever()
        {
            var session = new RecordingSession
            {
                TraceNodes = new[]
                {
                    TraceNode(100, 100, 110, "A"),
                    TraceNode(100, 110, 100, "B")
                }
            };
            var viewModel = new BattleDebugDiagnosticTraceViewModel();

            Assert.DoesNotThrow(() => viewModel.RefreshIfNeeded(session, 100));
            Assert.That(viewModel.Rows.Count, Is.EqualTo(2));
            Assert.That(viewModel.SelectContext(110), Is.True);
            Assert.That(viewModel.SelectedPath.Count, Is.EqualTo(2));
        }

        [Test]
        public void TraceSearch_IncludesMatchesAndAncestors_AndIgnoresCollapse()
        {
            var session = new RecordingSession
            {
                TraceNodes = new[]
                {
                    TraceNode(100, 100, 0, "Root"),
                    TraceNode(100, 110, 100, "Skill"),
                    TraceNode(100, 111, 110, "DamageEffect"),
                    TraceNode(100, 120, 100, "Unrelated")
                }
            };
            var viewModel = new BattleDebugDiagnosticTraceViewModel();
            viewModel.RefreshIfNeeded(session, 100);
            viewModel.ToggleCollapsed(100);

            Assert.That(viewModel.VisibleRows.Count, Is.EqualTo(1));

            viewModel.SetSearchText("damage");

            Assert.That(viewModel.SearchMatchCount, Is.EqualTo(1));
            Assert.That(viewModel.VisibleRows.Count, Is.EqualTo(3));
            Assert.That(viewModel.VisibleRows[0].Node.ContextId, Is.EqualTo(100));
            Assert.That(viewModel.VisibleRows[1].Node.ContextId, Is.EqualTo(110));
            Assert.That(viewModel.VisibleRows[2].Node.ContextId, Is.EqualTo(111));
            Assert.That(viewModel.IsSearchMatch(111), Is.True);
            Assert.That(viewModel.IsSearchMatch(110), Is.False);

            viewModel.SetSearchText(string.Empty);
            Assert.That(viewModel.VisibleRows.Count, Is.EqualTo(1));
        }

        [Test]
        public void TraceSearchNavigation_SelectsDirectMatchesAndWraps()
        {
            var session = new RecordingSession
            {
                TraceNodes = new[]
                {
                    TraceNode(100, 100, 0, "Root"),
                    TraceNode(100, 110, 100, "DamageSkill"),
                    TraceNode(100, 111, 110, "DamageEffect"),
                    TraceNode(100, 120, 100, "Unrelated")
                }
            };
            var viewModel = new BattleDebugDiagnosticTraceViewModel();
            viewModel.RefreshIfNeeded(session, 100);
            viewModel.SetSearchText("damage");

            Assert.That(viewModel.SelectSearchMatch(1), Is.True);
            Assert.That(viewModel.SelectedContextId, Is.EqualTo(110));
            Assert.That(viewModel.SelectSearchMatch(1), Is.True);
            Assert.That(viewModel.SelectedContextId, Is.EqualTo(111));
            Assert.That(viewModel.SelectSearchMatch(1), Is.True);
            Assert.That(viewModel.SelectedContextId, Is.EqualTo(110));
            Assert.That(viewModel.SelectSearchMatch(-1), Is.True);
            Assert.That(viewModel.SelectedContextId, Is.EqualTo(111));
            Assert.That(viewModel.GetVisibleRowIndex(111), Is.EqualTo(2));
        }

        [Test]
        public void TraceCollapseAll_PreservesSelectedPath_AndExpandAllRestoresRows()
        {
            var session = new RecordingSession
            {
                TraceNodes = new[]
                {
                    TraceNode(100, 100, 0, "Root"),
                    TraceNode(100, 110, 100, "Skill"),
                    TraceNode(100, 111, 110, "SelectedEffect"),
                    TraceNode(100, 120, 100, "OtherBranch"),
                    TraceNode(100, 121, 120, "OtherChild")
                }
            };
            var viewModel = new BattleDebugDiagnosticTraceViewModel();
            viewModel.RefreshIfNeeded(session, 100);
            viewModel.SelectContext(111);

            viewModel.CollapseAllPreservingSelection();

            Assert.That(viewModel.VisibleRows.Count, Is.EqualTo(4));
            Assert.That(viewModel.GetVisibleRowIndex(111), Is.EqualTo(2));
            Assert.That(viewModel.IsCollapsed(100), Is.False);
            Assert.That(viewModel.IsCollapsed(110), Is.False);
            Assert.That(viewModel.IsCollapsed(120), Is.True);
            Assert.That(viewModel.CollapsedBranchCount, Is.EqualTo(1));

            viewModel.ExpandAll();
            Assert.That(viewModel.VisibleRows.Count, Is.EqualTo(5));
            Assert.That(viewModel.CollapsedBranchCount, Is.Zero);
        }

        [Test]
        public void TracePin_ReturnsToPinnedNode_AndReportsEvictedNodeUnavailable()
        {
            var session = new RecordingSession
            {
                TraceNodes = new[]
                {
                    TraceNode(100, 100, 0, "Root"),
                    TraceNode(100, 110, 100, "Skill")
                }
            };
            var viewModel = new BattleDebugDiagnosticTraceViewModel();
            viewModel.RefreshIfNeeded(session, 100);
            viewModel.SelectContext(110);
            viewModel.PinSelection();
            viewModel.SelectContext(100);

            Assert.That(viewModel.PinnedContextId, Is.EqualTo(110));
            Assert.That(viewModel.SelectPinned(), Is.True);
            Assert.That(viewModel.SelectedContextId, Is.EqualTo(110));

            session.TraceStoreRevision++;
            session.TraceNodes = new[] { TraceNode(100, 100, 0, "Root") };
            viewModel.RefreshIfNeeded(session, 100);

            Assert.That(viewModel.PinnedContextId, Is.EqualTo(110));
            Assert.That(viewModel.IsPinnedContextAvailable, Is.False);
            Assert.That(viewModel.SelectPinned(), Is.False);
        }

        [Test]
        public void SelectionInspector_ActorUsesSelectionFrameAndRevisionAwareCache()
        {
            var actor = new BattleDiagnosticActorSummary(
                RecordingSession.Scope,
                27,
                42,
                BattleDiagnosticActorKind.Hero,
                1001,
                1,
                1f,
                2f,
                3f,
                90f,
                100f,
                true,
                "Hero");
            var session = new RecordingSession
            {
                StateStoreRevision = 5,
                Actors = new[] { actor }
            };
            var selection = new BattleDiagnosticSelection(
                session.SessionInfo.Scope,
                BattleDiagnosticSelectionKind.Actor,
                42,
                27);
            var viewModel = new BattleDebugSelectionInspectorViewModel();

            viewModel.RefreshIfNeeded(session, in selection);
            viewModel.RefreshIfNeeded(session, in selection);

            Assert.That(session.ActorQueryCount, Is.EqualTo(1));
            Assert.That(session.LastActorFrame, Is.EqualTo(27));
            Assert.That(viewModel.Actor, Is.EqualTo(actor));

            session.StateStoreRevision++;
            viewModel.RefreshIfNeeded(session, in selection);

            Assert.That(session.ActorQueryCount, Is.EqualTo(2));
        }

        [Test]
        public void SelectionInspector_EventUsesExactFrameAndFixedRevision()
        {
            var diagnosticEvent = new BattleDiagnosticEvent(
                RecordingSession.Scope,
                18,
                99,
                99,
                BattleDiagnosticEventKind.Damage,
                BattleDiagnosticEventChannel.DamageAndHeal,
                BattleDiagnosticEventOutcome.Succeeded,
                summary: "hit");
            var session = new RecordingSession
            {
                EventStoreRevision = 7,
                Events = new[] { diagnosticEvent }
            };
            var selection = new BattleDiagnosticSelection(
                session.SessionInfo.Scope,
                BattleDiagnosticSelectionKind.Event,
                99,
                18);
            var viewModel = new BattleDebugSelectionInspectorViewModel();

            viewModel.RefreshIfNeeded(session, in selection);

            Assert.That(viewModel.Event, Is.EqualTo(diagnosticEvent));
            Assert.That(session.LastEventQuery.Filter.Frames.FirstFrame, Is.EqualTo(18));
            Assert.That(session.LastEventQuery.Filter.Frames.LastFrame, Is.EqualTo(18));
            Assert.That(session.LastEventQuery.Page.StoreRevision, Is.EqualTo(7));
            Assert.That(session.LastEventQuery.NewestFirst, Is.True);
        }

        [Test]
        public void SelectionInspector_EventCanBeRestoredFromSecondPage()
        {
            var events = new List<BattleDiagnosticEvent>();
            for (var i = 0; i <= BattleDiagnosticPageRequest.MaximumPageSize; i++)
            {
                events.Add(new BattleDiagnosticEvent(
                    RecordingSession.Scope,
                    12,
                    i + 1,
                    i + 1,
                    BattleDiagnosticEventKind.Damage,
                    BattleDiagnosticEventChannel.DamageAndHeal,
                    BattleDiagnosticEventOutcome.Succeeded));
            }

            var session = new RecordingSession
            {
                EventStoreRevision = 8,
                Events = events,
                PageEventResults = true
            };
            var selection = new BattleDiagnosticSelection(
                session.SessionInfo.Scope,
                BattleDiagnosticSelectionKind.Event,
                BattleDiagnosticPageRequest.MaximumPageSize + 1,
                12);
            var viewModel = new BattleDebugSelectionInspectorViewModel();

            viewModel.RefreshIfNeeded(session, in selection);

            Assert.That(viewModel.Event.HasValue, Is.True);
            Assert.That(viewModel.Event.Value.Sequence, Is.EqualTo(BattleDiagnosticPageRequest.MaximumPageSize + 1));
            Assert.That(viewModel.EventPagesScanned, Is.EqualTo(2));
            Assert.That(session.EventQueryCount, Is.EqualTo(2));
        }

        [Test]
        public void SelectionInspector_EventLookupStopsAtBoundedScanWindow()
        {
            var count = BattleDebugSelectionInspectorViewModel.MaximumEventPages *
                        BattleDiagnosticPageRequest.MaximumPageSize + 1;
            var events = new List<BattleDiagnosticEvent>(count);
            for (var i = 0; i < count; i++)
            {
                events.Add(new BattleDiagnosticEvent(
                    RecordingSession.Scope,
                    12,
                    i + 1,
                    i + 1,
                    BattleDiagnosticEventKind.Damage,
                    BattleDiagnosticEventChannel.DamageAndHeal,
                    BattleDiagnosticEventOutcome.Succeeded));
            }

            var session = new RecordingSession
            {
                EventStoreRevision = 9,
                Events = events,
                PageEventResults = true
            };
            var selection = new BattleDiagnosticSelection(
                session.SessionInfo.Scope,
                BattleDiagnosticSelectionKind.Event,
                count,
                12);
            var viewModel = new BattleDebugSelectionInspectorViewModel();

            viewModel.RefreshIfNeeded(session, in selection);

            Assert.That(viewModel.Event.HasValue, Is.False);
            Assert.That(viewModel.EventPagesScanned, Is.EqualTo(BattleDebugSelectionInspectorViewModel.MaximumEventPages));
            Assert.That(session.EventQueryCount, Is.EqualTo(BattleDebugSelectionInspectorViewModel.MaximumEventPages));
            Assert.That(viewModel.QueryStatus.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Partial));
            Assert.That(viewModel.QueryStatus.Availability, Is.EqualTo(BattleDiagnosticDataAvailability.Truncated));
        }

        [Test]
        public void SelectionInspector_EventEvictionRemainsVisible()
        {
            var session = new RecordingSession
            {
                EventStoreRevision = 11,
                EvictedEventRevision = 11
            };
            var selection = new BattleDiagnosticSelection(
                session.SessionInfo.Scope,
                BattleDiagnosticSelectionKind.Event,
                77,
                20);
            var viewModel = new BattleDebugSelectionInspectorViewModel();

            viewModel.RefreshIfNeeded(session, in selection);

            Assert.That(viewModel.Event.HasValue, Is.False);
            Assert.That(viewModel.QueryStatus.Availability, Is.EqualTo(BattleDiagnosticDataAvailability.Evicted));
            Assert.That(viewModel.StatusMessage, Does.Contain("Evicted"));
        }

        [Test]
        public void SelectionInspector_TraceUsesRelatedRootAndMatchesContext()
        {
            var root = TraceNode(100, 100, 0, "Skill");
            var child = TraceNode(100, 110, 100, "Effect");
            var session = new RecordingSession
            {
                TraceStoreRevision = 4,
                TraceNodes = new[] { root, child }
            };
            var selection = new BattleDiagnosticSelection(
                session.SessionInfo.Scope,
                BattleDiagnosticSelectionKind.TraceNode,
                110,
                1,
                100);
            var viewModel = new BattleDebugSelectionInspectorViewModel();

            viewModel.RefreshIfNeeded(session, in selection);

            Assert.That(session.LastTraceRootContextId, Is.EqualTo(100));
            Assert.That(viewModel.TraceNode, Is.EqualTo(child));
        }

        [Test]
        public void SelectionInspector_ConfigSelectionPreservesExactFlowPhase()
        {
            var session = new RecordingSession();
            var sourceSelection = new BattleDiagnosticSelection(
                session.SessionInfo.Scope,
                BattleDiagnosticSelectionKind.TraceNode,
                110,
                1,
                100);
            var reference = new BattleDebugConfigReference(
                BattleDebugConfigKind.SkillFlow,
                7001,
                "cast.release");
            var viewModel = new BattleDebugSelectionInspectorViewModel();

            viewModel.SelectConfig(in reference, in sourceSelection);

            Assert.That(viewModel.HasConfigSelection, Is.True);
            Assert.That(viewModel.ConfigReference, Is.EqualTo(reference));
            Assert.That(viewModel.ConfigReference.PhaseId, Is.EqualTo("cast.release"));
        }

        [Test]
        public void SelectionInspector_ConfigSelectionResolvesAuthoritativeSource()
        {
            var session = new RecordingSession();
            var sourceSelection = new BattleDiagnosticSelection(
                session.SessionInfo.Scope,
                BattleDiagnosticSelectionKind.Event,
                12,
                1);
            var reference = new BattleDebugConfigReference(BattleDebugConfigKind.Skill, 1);
            var viewModel = new BattleDebugSelectionInspectorViewModel();
            viewModel.SelectConfig(in reference, in sourceSelection);

            var active = viewModel.RefreshConfigIfActive(in sourceSelection);

            Assert.That(active, Is.True);
            Assert.That(viewModel.ConfigLocation.HasValue, Is.True, viewModel.ConfigStatusMessage);
            Assert.That(viewModel.ConfigLocation.Value.AssetPath, Does.EndWith("/Resources/moba/skills.json"));
            Assert.That(viewModel.ConfigLocation.Value.LineNumber, Is.GreaterThan(0));
        }

        [Test]
        public void SelectionInspector_ConfigSelectionClearsWhenWorkspaceSelectionChanges()
        {
            var session = new RecordingSession();
            var sourceSelection = new BattleDiagnosticSelection(
                session.SessionInfo.Scope,
                BattleDiagnosticSelectionKind.Event,
                12,
                1);
            var nextSelection = new BattleDiagnosticSelection(
                session.SessionInfo.Scope,
                BattleDiagnosticSelectionKind.Actor,
                27,
                1);
            var reference = new BattleDebugConfigReference(BattleDebugConfigKind.Skill, 1);
            var viewModel = new BattleDebugSelectionInspectorViewModel();
            viewModel.SelectConfig(in reference, in sourceSelection);

            var active = viewModel.RefreshConfigIfActive(in nextSelection);

            Assert.That(active, Is.False);
            Assert.That(viewModel.HasConfigSelection, Is.False);
            Assert.That(viewModel.ConfigLocation.HasValue, Is.False);
        }

        [Test]
        public void EmptyStateProjector_MissingRequiredSelection_TakesPriority()
        {
            var projection = BattleDebugEmptyStateProjector.Project(
                BattleDiagnosticQueryStatus.Ready(1, 1, 0, false),
                requiresSelection: true,
                hasSelection: false,
                subject: "事件");

            Assert.That(projection.Reason, Is.EqualTo(BattleDebugEmptyStateReason.SelectionRequired));
            Assert.That(projection.Severity, Is.EqualTo(BattleDebugEmptyStateSeverity.Info));
        }

        [Test]
        public void EmptyStateProjector_UsesConfiguredSelectionSubject()
        {
            var projection = BattleDebugEmptyStateProjector.Project(
                default,
                requiresSelection: true,
                hasSelection: false,
                subject: "Trace 树",
                selectionSubject: "Root Context");

            Assert.That(projection.Reason, Is.EqualTo(BattleDebugEmptyStateReason.SelectionRequired));
            Assert.That(projection.Title, Does.Contain("Root Context"));
            Assert.That(projection.Message, Does.Contain("Root Context"));
            Assert.That(projection.Message, Does.Not.Contain("Actor"));
        }

        [TestCase(true, (int)BattleDebugEmptyStateReason.FilteredEmpty)]
        [TestCase(false, (int)BattleDebugEmptyStateReason.Empty)]
        public void EmptyStateProjector_EmptyResult_DistinguishesActiveFilters(
            bool hasActiveFilter,
            int expectedReason)
        {
            var projection = BattleDebugEmptyStateProjector.Project(
                BattleDiagnosticQueryStatus.Ready(1, 1, 0, false),
                hasActiveFilter: hasActiveFilter,
                subject: "事件");

            Assert.That(projection.Reason, Is.EqualTo((BattleDebugEmptyStateReason)expectedReason));
            Assert.That(projection.HasValue, Is.True);
        }

        [TestCase(BattleDiagnosticDataAvailability.NotProduced, (int)BattleDebugEmptyStateReason.NotProduced, (int)BattleDebugEmptyStateSeverity.Info)]
        [TestCase(BattleDiagnosticDataAvailability.NotCaptured, (int)BattleDebugEmptyStateReason.NotCaptured, (int)BattleDebugEmptyStateSeverity.Warning)]
        [TestCase(BattleDiagnosticDataAvailability.Evicted, (int)BattleDebugEmptyStateReason.Evicted, (int)BattleDebugEmptyStateSeverity.Warning)]
        [TestCase(BattleDiagnosticDataAvailability.Unsupported, (int)BattleDebugEmptyStateReason.Unsupported, (int)BattleDebugEmptyStateSeverity.Info)]
        [TestCase(BattleDiagnosticDataAvailability.Disconnected, (int)BattleDebugEmptyStateReason.Disconnected, (int)BattleDebugEmptyStateSeverity.Warning)]
        public void EmptyStateProjector_UnavailableResult_MapsReasonAndSeverity(
            BattleDiagnosticDataAvailability availability,
            int expectedReason,
            int expectedSeverity)
        {
            var status = BattleDiagnosticQueryStatus.Unavailable(1, 2, availability, "detail");
            var projection = BattleDebugEmptyStateProjector.Project(status, subject: "状态");

            Assert.That(projection.Reason, Is.EqualTo((BattleDebugEmptyStateReason)expectedReason));
            Assert.That(projection.Severity, Is.EqualTo((BattleDebugEmptyStateSeverity)expectedSeverity));
            Assert.That(projection.Message, Does.Contain("detail"));
        }

        [Test]
        public void EmptyStateProjector_FailedResult_IncludesErrorCode()
        {
            var status = BattleDiagnosticQueryStatus.Failed(1, 2, "Query.Failed", "boom");
            var projection = BattleDebugEmptyStateProjector.Project(status, subject: "事件");

            Assert.That(projection.Reason, Is.EqualTo(BattleDebugEmptyStateReason.Error));
            Assert.That(projection.Severity, Is.EqualTo(BattleDebugEmptyStateSeverity.Error));
            Assert.That(projection.Message, Does.Contain("Query.Failed"));
            Assert.That(projection.Message, Does.Contain("boom"));
        }

        [Test]
        public void EmptyStateProjector_PartialResult_RemainsDisplayable()
        {
            var status = BattleDiagnosticQueryStatus.Partial(
                1,
                2,
                3,
                BattleDiagnosticDataAvailability.Truncated,
                "partial");
            var projection = BattleDebugEmptyStateProjector.Project(status, subject: "事件");

            Assert.That(projection.HasValue, Is.False);
        }

        private static BattleDiagnosticEvent TriggerEvent(
            int frame,
            long sequence,
            int configId,
            in BattleDiagnosticTriggerAnalysisPayload payload)
        {
            return new BattleDiagnosticEvent(
                RecordingSession.Scope,
                frame,
                sequence,
                sequence,
                BattleDiagnosticEventKind.TriggerAnalysis,
                BattleDiagnosticEventChannel.Effect,
                BattleDiagnosticEventOutcome.Failed,
                configId: configId,
                payloadVersion: BattleDiagnosticTriggerAnalysisPayload.CurrentSchemaVersion,
                summary: payload.Reason,
                payload: BattleDiagnosticEventPayload.FromTriggerAnalysis(in payload));
        }

        private static BattleDiagnosticTraceNodeSummary TraceNode(
            long rootContextId,
            long contextId,
            long parentContextId,
            string kind)
        {
            return new BattleDiagnosticTraceNodeSummary(
                RecordingSession.Scope,
                rootContextId,
                contextId,
                parentContextId,
                1,
                -1,
                BattleDiagnosticTraceNodeState.Active,
                kind: kind);
        }

        private sealed class RecordingSession : IBattleDiagnosticReadOnlySession
        {
            internal static readonly BattleDiagnosticSessionScope Scope =
                new BattleDiagnosticSessionScope("test", "world", 1);

            public RecordingSession(int worldInstanceId = 1)
            {
                SessionInfo = new BattleDiagnosticSessionInfo(
                    new BattleDiagnosticSessionScope("test", "world", worldInstanceId),
                    "test",
                    string.Empty,
                    1,
                    1,
                    BattleDiagnosticCapabilities.WorldState |
                    BattleDiagnosticCapabilities.ActorState |
                    BattleDiagnosticCapabilities.Events |
                    BattleDiagnosticCapabilities.Trace,
                    BattleDiagnosticConnectionState.Connected,
                    BattleDiagnosticCaptureState.Capturing);
            }

            public BattleDiagnosticSessionInfo SessionInfo { get; }

            public long EventStoreRevision { get; set; }
            public long StateStoreRevision { get; set; }
            public long TraceStoreRevision { get; set; }
            public long ActorAttributeStoreRevision { get; set; }
            public long ActorBuffStoreRevision { get; set; }
            public long ActorTagStoreRevision { get; set; }
            public long ActorEffectStoreRevision { get; set; }
            public IReadOnlyList<BattleDiagnosticActorSummary> Actors { get; set; }
            public IReadOnlyList<BattleDiagnosticEvent> Events { get; set; }
            public bool PageEventResults { get; set; }
            public long EvictedEventRevision { get; set; } = -1;
            public IReadOnlyList<BattleDiagnosticActorTag> Tags { get; set; }
            public IReadOnlyList<BattleDiagnosticActorEffect> Effects { get; set; }
            public IReadOnlyList<BattleDiagnosticTraceNodeSummary> TraceNodes { get; set; }
            public BattleDiagnosticDataAvailability TraceAvailability { get; set; } =
                BattleDiagnosticDataAvailability.NotProduced;
            public bool TraceTruncated { get; set; }
            public long StoreRevision => EventStoreRevision;
            public int EventQueryCount { get; private set; }
            public BattleDiagnosticEventQuery LastEventQuery { get; private set; }
            public int WorldQueryCount { get; private set; }
            public int ActorQueryCount { get; private set; }
            public int AttributeQueryCount { get; private set; }
            public int ModifierQueryCount { get; private set; }
            public int BuffQueryCount { get; private set; }
            public int TagQueryCount { get; private set; }
            public int EffectQueryCount { get; private set; }
            public int TraceQueryCount { get; private set; }
            public long LastTraceRootContextId { get; private set; }
            public int LastWorldFrame { get; private set; }
            public int LastActorFrame { get; private set; }
            public int LastAttributeFrame { get; private set; }
            public long LastAttributeActorId { get; private set; }
            public int LastBuffFrame { get; private set; }
            public long LastBuffActorId { get; private set; }
            public int LastTagFrame { get; private set; }
            public long LastTagActorId { get; private set; }
            public int LastEffectFrame { get; private set; }
            public long LastEffectActorId { get; private set; }

            public BattleDiagnosticQueryResult<BattleDiagnosticWorldSummary> QueryWorld(
                long requestId,
                int frame)
            {
                WorldQueryCount++;
                LastWorldFrame = frame;
                return BattleDiagnosticQueryResult<BattleDiagnosticWorldSummary>.Unavailable(
                    requestId,
                    StateStoreRevision,
                    BattleDiagnosticDataAvailability.NotProduced);
            }

            public BattleDiagnosticQueryResult<BattleDiagnosticActorSummary> QueryActors(
                long requestId,
                int frame)
            {
                ActorQueryCount++;
                LastActorFrame = frame;
                if (Actors != null)
                {
                    return BattleDiagnosticQueryResult<BattleDiagnosticActorSummary>.FromItems(
                        requestId, StateStoreRevision, new List<BattleDiagnosticActorSummary>(Actors), false);
                }

                return BattleDiagnosticQueryResult<BattleDiagnosticActorSummary>.Unavailable(
                    requestId,
                    StateStoreRevision,
                    BattleDiagnosticDataAvailability.NotProduced);
            }

            public BattleDiagnosticQueryResult<BattleDiagnosticEvent> QueryEvents(
                BattleDiagnosticEventQuery query)
            {
                EventQueryCount++;
                LastEventQuery = query;
                if (query.Page.StoreRevision == EvictedEventRevision)
                {
                    return BattleDiagnosticQueryResult<BattleDiagnosticEvent>.Unavailable(
                        query.RequestId,
                        query.Page.StoreRevision,
                        BattleDiagnosticDataAvailability.Evicted,
                        "The requested store revision is no longer retained.");
                }

                if (!PageEventResults)
                {
                    return BattleDiagnosticQueryResult<BattleDiagnosticEvent>.FromItems(
                        query.RequestId,
                        EventStoreRevision,
                        Events != null
                            ? new List<BattleDiagnosticEvent>(Events)
                            : new List<BattleDiagnosticEvent>(),
                        false);
                }

                var source = Events ?? Array.Empty<BattleDiagnosticEvent>();
                var page = new List<BattleDiagnosticEvent>();
                var end = Math.Min(source.Count, query.Page.Offset + query.Page.Limit);
                for (var i = query.Page.Offset; i < end; i++)
                {
                    page.Add(source[i]);
                }

                return BattleDiagnosticQueryResult<BattleDiagnosticEvent>.FromItems(
                    query.RequestId,
                    query.Page.StoreRevision,
                    page,
                    end < source.Count);
            }

            public BattleDiagnosticQueryResult<BattleDiagnosticTraceNodeSummary> QueryTrace(
                long requestId,
                long rootContextId)
            {
                TraceQueryCount++;
                LastTraceRootContextId = rootContextId;
                if (TraceNodes != null)
                {
                    return BattleDiagnosticQueryResult<BattleDiagnosticTraceNodeSummary>.FromItems(
                        requestId,
                        TraceStoreRevision,
                        new List<BattleDiagnosticTraceNodeSummary>(TraceNodes),
                        TraceTruncated);
                }

                return BattleDiagnosticQueryResult<BattleDiagnosticTraceNodeSummary>.Unavailable(
                    requestId,
                    TraceStoreRevision,
                    TraceAvailability);
            }

            public BattleDiagnosticQueryResult<BattleDiagnosticActorAttribute> QueryActorAttributes(
                long requestId,
                int frame,
                long actorId)
            {
                AttributeQueryCount++;
                LastAttributeFrame = frame;
                LastAttributeActorId = actorId;
                return BattleDiagnosticQueryResult<BattleDiagnosticActorAttribute>.Unavailable(
                    requestId,
                    ActorAttributeStoreRevision,
                    BattleDiagnosticDataAvailability.NotProduced);
            }

            public BattleDiagnosticQueryResult<BattleDiagnosticActorAttributeModifier> QueryActorAttributeModifiers(
                long requestId,
                int frame,
                long actorId)
            {
                ModifierQueryCount++;
                return BattleDiagnosticQueryResult<BattleDiagnosticActorAttributeModifier>.Unavailable(
                    requestId,
                    ActorAttributeStoreRevision,
                    BattleDiagnosticDataAvailability.NotProduced);
            }

            public BattleDiagnosticQueryResult<BattleDiagnosticActorBuff> QueryActorBuffs(
                long requestId,
                int frame,
                long actorId)
            {
                BuffQueryCount++;
                LastBuffFrame = frame;
                LastBuffActorId = actorId;
                return BattleDiagnosticQueryResult<BattleDiagnosticActorBuff>.Unavailable(
                    requestId,
                    ActorBuffStoreRevision,
                    BattleDiagnosticDataAvailability.NotProduced);
            }

            public BattleDiagnosticQueryResult<BattleDiagnosticActorTag> QueryActorTags(
                long requestId,
                int frame,
                long actorId)
            {
                TagQueryCount++;
                LastTagFrame = frame;
                LastTagActorId = actorId;
                if (Tags != null)
                {
                    return BattleDiagnosticQueryResult<BattleDiagnosticActorTag>.FromItems(
                        requestId, ActorTagStoreRevision, new List<BattleDiagnosticActorTag>(Tags), false);
                }

                return BattleDiagnosticQueryResult<BattleDiagnosticActorTag>.Unavailable(
                    requestId,
                    ActorTagStoreRevision,
                    BattleDiagnosticDataAvailability.NotProduced);
            }

            public BattleDiagnosticQueryResult<BattleDiagnosticActorEffect> QueryActorEffects(
                long requestId,
                int frame,
                long actorId)
            {
                EffectQueryCount++;
                LastEffectFrame = frame;
                LastEffectActorId = actorId;
                if (Effects != null)
                {
                    return BattleDiagnosticQueryResult<BattleDiagnosticActorEffect>.FromItems(
                        requestId, ActorEffectStoreRevision, new List<BattleDiagnosticActorEffect>(Effects), false);
                }

                return BattleDiagnosticQueryResult<BattleDiagnosticActorEffect>.Unavailable(
                    requestId,
                    ActorEffectStoreRevision,
                    BattleDiagnosticDataAvailability.NotProduced);
            }
        }
    }
}
