using System;
using System.Collections.Generic;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Triggering.PlanActions;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class MobaConfiguredActionDebugLogTests
    {
        private ILogSink _previousSink;
        private MobaRuntimeLogLevel _previousMinimumLevel;
        private bool _previousInvestigationEnabled;
        private bool _previousConfiguredActionDebugEnabled;
        private RecordingLogSink _sink;

        [SetUp]
        public void SetUp()
        {
            _previousSink = Log.Sink;
            _previousMinimumLevel = MobaRuntimeLog.MinimumLevel;
            _previousInvestigationEnabled = MobaRuntimeLog.EnableInvestigationLogs;
            _previousConfiguredActionDebugEnabled = MobaRuntimeLog.EnableConfiguredActionDebugLogs;

            _sink = new RecordingLogSink();
            Log.SetSink(_sink);
            MobaRuntimeLog.MinimumLevel = MobaRuntimeLogLevel.Info;
            MobaRuntimeLog.EnableInvestigationLogs = false;
            MobaRuntimeLog.EnableConfiguredActionDebugLogs = false;
        }

        [TearDown]
        public void TearDown()
        {
            Log.SetSink(_previousSink);
            MobaRuntimeLog.MinimumLevel = _previousMinimumLevel;
            MobaRuntimeLog.EnableInvestigationLogs = _previousInvestigationEnabled;
            MobaRuntimeLog.EnableConfiguredActionDebugLogs = _previousConfiguredActionDebugEnabled;
        }

        [Test]
        public void ConfiguredActionDebugLogs_AreDisabledByDefaultAndIndependentFromInvestigationLogs()
        {
            MobaRuntimeLog.EnableInvestigationLogs = true;

            Assert.IsFalse(MobaRuntimeLog.IsEnabled(
                MobaRuntimeLogLevel.Info,
                MobaRuntimeLogPurpose.ConfiguredActionDebug));

            MobaPlanActionDiagnostics.ConfiguredActionDebug(
                null,
                "debug_log",
                "configured message");

            Assert.IsEmpty(_sink.InfoMessages);
        }

        [Test]
        public void ConfiguredActionDebugLogs_OutputWhenDedicatedSwitchIsEnabled()
        {
            MobaRuntimeLog.EnableConfiguredActionDebugLogs = true;

            Assert.IsTrue(MobaRuntimeLog.IsEnabled(
                MobaRuntimeLogLevel.Info,
                MobaRuntimeLogPurpose.ConfiguredActionDebug));
            Assert.IsFalse(MobaRuntimeLog.IsEnabled(
                MobaRuntimeLogLevel.Debug,
                MobaRuntimeLogPurpose.Investigation));

            MobaPlanActionDiagnostics.ConfiguredActionDebug(
                null,
                "debug_log",
                "configured message");

            Assert.AreEqual(1, _sink.InfoMessages.Count);
            StringAssert.Contains("[ConfiguredActionDebug]", _sink.InfoMessages[0]);
            StringAssert.Contains("action=debug_log configured message", _sink.InfoMessages[0]);
        }

        [Test]
        public void DisabledRuntimeLog_DoesNotInvokeMessageFactory()
        {
            MobaRuntimeLog.MinimumLevel = MobaRuntimeLogLevel.Off;
            var invocationCount = 0;

            MobaRuntimeLog.Warning(
                MobaRuntimeLogModule.Diagnostics,
                MobaRuntimeLogPurpose.Validation,
                nameof(DisabledRuntimeLog_DoesNotInvokeMessageFactory),
                () =>
                {
                    invocationCount++;
                    return "deferred validation message";
                });

            Assert.AreEqual(0, invocationCount);
            Assert.IsEmpty(_sink.WarningMessages);
        }

        [Test]
        public void SkillPipelineStartResult_ExposesStableCodeAndMatchesLegacyWrapper()
        {
            var runner = new SkillPipelineRunner(actorId: 7);
            var request = default(SkillCastRequest);
            var context = new SkillCastContext();
            var policy = SkillCastPolicy.Default;

            var result = runner.TryStart(
                preCastConfig: null,
                preCastPhases: null,
                castConfig: null,
                castPhases: null,
                abilityInstance: this,
                in request,
                context,
                in policy);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("skill.start.castConfigMissing", result.StartReject.Code);
            Assert.AreEqual("Skill cast pipeline config is missing.", result.FailReason);
            Assert.IsFalse(result.PipelineFailure.HasValue);

            var legacySuccess = runner.Start(
                preCastConfig: null,
                preCastPhases: null,
                castConfig: null,
                castPhases: null,
                abilityInstance: this,
                in request,
                context,
                out var legacyFailReason,
                policy: in policy);

            Assert.IsFalse(legacySuccess);
            Assert.AreEqual(result.FailReason, legacyFailReason);
            Assert.AreEqual(result.StartReject.Code, runner.LastStartReject.Code);
        }

        private sealed class RecordingLogSink : ILogSink
        {
            public readonly List<string> InfoMessages = new List<string>();
            public readonly List<string> WarningMessages = new List<string>();

            public void Info(string message)
            {
                InfoMessages.Add(message);
            }

            public void Warning(string message)
            {
                WarningMessages.Add(message);
            }

            public void Error(string message)
            {
            }

            public void Exception(Exception exception, string message = null)
            {
            }
        }
    }
}
