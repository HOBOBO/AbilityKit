using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Trace;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    /// <summary>
    /// 验证 Buff 生命周期 Producer（BuffAdded/BuffRemoved）的诊断草稿生成与采集。
    /// </summary>
    public sealed class MobaBuffDiagnosticProducerTests
    {
        private BattleDiagnosticSessionScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("session", "world", 1);
        }

        [Test]
        public void CreateBuffAddedDraft_MapsAllFields()
        {
            var request = new BuffApplyRequest
            {
                TargetActorId = 11,
                BuffId = 201,
                SourceActorId = 7,
                DurationOverrideMs = 3000,
                SourceContextId = 500,
                ForceNewInstance = true,
                Origin = new BuffOriginContext(
                    parentContextId: 400,
                    originSourceActorId: 7,
                    originTargetActorId: 11,
                    skillRuntimeHandle: new MobaSkillCastRuntimeHandle(42, 2, 400)),
            };

            var draft = MobaBuffService.CreateBuffAddedDraft(in request);

            Assert.That(draft.Kind, Is.EqualTo(BattleDiagnosticEventKind.BuffAdded));
            Assert.That(draft.Channel, Is.EqualTo(BattleDiagnosticEventChannel.Buff));
            Assert.That(draft.Outcome, Is.EqualTo(BattleDiagnosticEventOutcome.Succeeded));
            Assert.That(draft.SourceActorId, Is.EqualTo(7));
            Assert.That(draft.TargetActorId, Is.EqualTo(11));
            Assert.That(draft.ConfigId, Is.EqualTo(201));
            Assert.That(draft.SkillRuntime, Is.EqualTo(new BattleDiagnosticRuntimeHandle(42, 2)));
            Assert.That(draft.Summary, Does.Contain("201"));
            Assert.That(draft.Summary, Does.Contain("3000"));
            Assert.That(draft.Summary, Does.Contain("True"));
        }

        [Test]
        public void CreateBuffAddedDraft_WithoutDuration_OmitsDurationInSummary()
        {
            var request = new BuffApplyRequest
            {
                TargetActorId = 11,
                BuffId = 201,
                SourceActorId = 7,
                DurationOverrideMs = 0,
                SourceContextId = 0L,
                Origin = default,
            };

            var draft = MobaBuffService.CreateBuffAddedDraft(in request);

            Assert.That(draft.Summary, Does.Contain("201"));
            Assert.That(draft.Summary, Does.Not.Contain("durationMs"));
        }

        [Test]
        public void CreateBuffAddedDraft_WithoutOrigin_UsesSourceContextId()
        {
            var request = new BuffApplyRequest
            {
                TargetActorId = 11,
                BuffId = 201,
                SourceActorId = 7,
                DurationOverrideMs = 0,
                SourceContextId = 900,
                Origin = default,
            };

            var draft = MobaBuffService.CreateBuffAddedDraft(in request);

            Assert.That(draft.ContextId, Is.EqualTo(900));
            Assert.That(draft.RootContextId, Is.EqualTo(900));
            Assert.That(draft.SkillRuntime, Is.EqualTo(default(BattleDiagnosticRuntimeHandle)));
        }

        [Test]
        public void CreateBuffRemovedDraft_MapsAllFields()
        {
            var request = new BuffRemoveRequest
            {
                TargetActorId = 11,
                BuffId = 201,
                SourceActorId = 7,
                SourceContextId = 500,
                Reason = MobaExecutionEndReason.Expired,
            };

            var draft = MobaBuffService.CreateBuffRemovedDraft(in request);

            Assert.That(draft.Kind, Is.EqualTo(BattleDiagnosticEventKind.BuffRemoved));
            Assert.That(draft.Channel, Is.EqualTo(BattleDiagnosticEventChannel.Buff));
            Assert.That(draft.Outcome, Is.EqualTo(BattleDiagnosticEventOutcome.Succeeded));
            Assert.That(draft.SourceActorId, Is.EqualTo(7));
            Assert.That(draft.TargetActorId, Is.EqualTo(11));
            Assert.That(draft.ConfigId, Is.EqualTo(201));
            Assert.That(draft.ContextId, Is.EqualTo(500));
            Assert.That(draft.Summary, Does.Contain("201"));
            Assert.That(draft.Summary, Does.Contain("Expired"));
        }

        [Test]
        public void BuffAddedDraft_FlowsThroughCollector()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);
            var request = new BuffApplyRequest
            {
                TargetActorId = 11,
                BuffId = 201,
                SourceActorId = 7,
                DurationOverrideMs = 0,
                SourceContextId = 0L,
                Origin = default,
            };

            var draft = MobaBuffService.CreateBuffAddedDraft(in request);
            var accepted = collector.TryCollect(in draft);

            Assert.That(accepted, Is.True);
            Assert.That(collector.LastSequence, Is.EqualTo(1));
            Assert.That(collector.Store.Count, Is.EqualTo(1));
        }

        [Test]
        public void BuffRemovedDraft_FlowsThroughCollector()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);
            var request = new BuffRemoveRequest
            {
                TargetActorId = 11,
                BuffId = 201,
                SourceActorId = 7,
                SourceContextId = 0L,
                Reason = MobaExecutionEndReason.Dispelled,
            };

            var draft = MobaBuffService.CreateBuffRemovedDraft(in request);
            var accepted = collector.TryCollect(in draft);

            Assert.That(accepted, Is.True);
            Assert.That(collector.LastSequence, Is.EqualTo(1));
            Assert.That(collector.Store.Count, Is.EqualTo(1));
        }

        [Test]
        public void BuffDrafts_RespectDisabledChannel()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);
            collector.EnabledChannels = BattleDiagnosticEventChannel.Skill;

            var applyRequest = new BuffApplyRequest
            {
                TargetActorId = 11,
                BuffId = 201,
                SourceActorId = 7,
            };
            var removeRequest = new BuffRemoveRequest
            {
                TargetActorId = 11,
                BuffId = 201,
                SourceActorId = 7,
            };

            var addedDraft = MobaBuffService.CreateBuffAddedDraft(in applyRequest);
            var removedDraft = MobaBuffService.CreateBuffRemovedDraft(in removeRequest);

            Assert.That(collector.TryCollect(in addedDraft), Is.False);
            Assert.That(collector.TryCollect(in removedDraft), Is.False);
            Assert.That(collector.LastSequence, Is.Zero);
            Assert.That(collector.Store.Count, Is.Zero);
        }

        [Test]
        public void BuffAddedAndRemoved_ProduceStrictSequence()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);
            var applyRequest = new BuffApplyRequest
            {
                TargetActorId = 11,
                BuffId = 201,
                SourceActorId = 7,
            };
            var removeRequest = new BuffRemoveRequest
            {
                TargetActorId = 11,
                BuffId = 201,
                SourceActorId = 7,
            };

            var addedDraft = MobaBuffService.CreateBuffAddedDraft(in applyRequest);
            var removedDraft = MobaBuffService.CreateBuffRemovedDraft(in removeRequest);
            collector.TryCollect(in addedDraft);
            collector.TryCollect(in removedDraft);

            Assert.That(collector.LastSequence, Is.EqualTo(2));
            Assert.That(collector.Store.Count, Is.EqualTo(2));
        }
    }
}
