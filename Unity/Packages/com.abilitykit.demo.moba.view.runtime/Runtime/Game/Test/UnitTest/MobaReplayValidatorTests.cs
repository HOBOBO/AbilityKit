using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Demo.Moba.Replay.Validation;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Battle.Replay;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class MobaReplayValidatorTests
    {
        private static MobaActorSnapshotEntry Actor(int actorId, float x, float y, float z, float hp, int teamId = 1)
        {
            return new MobaActorSnapshotEntry(
                actorId,
                x, y, z,
                rotation: 0f,
                velocityX: 0f,
                velocityZ: 0f,
                hp: hp,
                hpMax: 100f,
                teamId: teamId);
        }

        private static MobaWorldSnapshotPayload Snapshot(int frame, params MobaActorSnapshotEntry[] actors)
        {
            return new MobaWorldSnapshotPayload(
                worldId: 1UL,
                frame: frame,
                timestamp: frame * 33L,
                isFullSnapshot: true,
                actors: actors ?? new MobaActorSnapshotEntry[0]);
        }

        [Test]
        public void IdenticalSnapshots_ProduceCleanReport()
        {
            var validator = new MobaReplayValidator();
            var recorded = new List<MobaWorldSnapshotPayload>
            {
                Snapshot(1, Actor(11, 0f, 0f, 0f, 100f)),
                Snapshot(2, Actor(11, 1f, 0f, 0f, 100f)),
                Snapshot(3, Actor(11, 2f, 0f, 0f, 90f))
            };
            var replayed = new List<MobaWorldSnapshotPayload>
            {
                Snapshot(1, Actor(11, 0f, 0f, 0f, 100f)),
                Snapshot(2, Actor(11, 1f, 0f, 0f, 100f)),
                Snapshot(3, Actor(11, 2f, 0f, 0f, 90f))
            };

            var report = validator.Compare(recorded, replayed, "identical");

            Assert.AreEqual("identical", report.ScenarioName);
            Assert.AreEqual(3, report.TotalComparedFrames);
            Assert.AreEqual(3, report.MatchedFrames);
            Assert.AreEqual(0, report.DivergentFrames);
            Assert.AreEqual(0, report.TotalActorDiffs);
            Assert.IsTrue(report.IsClean);
            Assert.AreEqual(1f, report.MatchRate);
        }

        [Test]
        public void PositionDivergence_IsDetectedAsDivergent()
        {
            var validator = new MobaReplayValidator();
            var recorded = new List<MobaWorldSnapshotPayload>
            {
                Snapshot(1, Actor(11, 0f, 0f, 0f, 100f))
            };
            var replayed = new List<MobaWorldSnapshotPayload>
            {
                Snapshot(1, Actor(11, 0.05f, 0f, 0f, 100f)) // 5 cm drift
            };

            var report = validator.Compare(recorded, replayed);

            Assert.AreEqual(1, report.DivergentFrames);
            Assert.AreEqual(0, report.MatchedFrames);
            Assert.AreEqual(1, report.TotalActorDiffs);
            Assert.Greater(report.MaxPositionDelta, 0);
            Assert.IsFalse(report.IsClean);
        }

        [Test]
        public void HpDivergence_IsDetected()
        {
            var validator = new MobaReplayValidator();
            var recorded = new List<MobaWorldSnapshotPayload>
            {
                Snapshot(1, Actor(11, 0f, 0f, 0f, 100f))
            };
            var replayed = new List<MobaWorldSnapshotPayload>
            {
                Snapshot(1, Actor(11, 0f, 0f, 0f, 90f))
            };

            var report = validator.Compare(recorded, replayed);

            Assert.AreEqual(1, report.DivergentFrames);
            Assert.Greater(report.MaxHpDelta, 0);
        }

        [Test]
        public void MissingActor_IsReportedAsDivergence()
        {
            var validator = new MobaReplayValidator();
            var recorded = new List<MobaWorldSnapshotPayload>
            {
                Snapshot(1, Actor(11, 0f, 0f, 0f, 100f), Actor(22, 5f, 0f, 0f, 80f))
            };
            var replayed = new List<MobaWorldSnapshotPayload>
            {
                Snapshot(1, Actor(11, 0f, 0f, 0f, 100f))
            };

            var report = validator.Compare(recorded, replayed);

            Assert.AreEqual(1, report.DivergentFrames);
            Assert.AreEqual(1, report.TotalMissingActors);
            Assert.IsFalse(report.IsClean);
        }

        [Test]
        public void ExtraActor_IsReportedAsDivergence()
        {
            var validator = new MobaReplayValidator();
            var recorded = new List<MobaWorldSnapshotPayload>
            {
                Snapshot(1, Actor(11, 0f, 0f, 0f, 100f))
            };
            var replayed = new List<MobaWorldSnapshotPayload>
            {
                Snapshot(1, Actor(11, 0f, 0f, 0f, 100f), Actor(33, 9f, 0f, 0f, 100f))
            };

            var report = validator.Compare(recorded, replayed);

            Assert.AreEqual(1, report.DivergentFrames);
            Assert.AreEqual(1, report.TotalExtraActors);
            Assert.IsFalse(report.IsClean);
        }

        [Test]
        public void FrameNumberMismatch_IsSkippedAndCountedAsDivergence()
        {
            var validator = new MobaReplayValidator();
            var recorded = new List<MobaWorldSnapshotPayload>
            {
                Snapshot(1, Actor(11, 0f, 0f, 0f, 100f))
            };
            var replayed = new List<MobaWorldSnapshotPayload>
            {
                Snapshot(2, Actor(11, 0f, 0f, 0f, 100f))
            };

            var report = validator.Compare(recorded, replayed);

            Assert.AreEqual(1, report.TotalComparedFrames);
            Assert.AreEqual(1, report.DivergentFrames);
            Assert.AreEqual(0, report.MatchedFrames);
            Assert.AreEqual(1, report.SkippedFrames);
        }

        [Test]
        public void CompareFrame_SingleActor_ProducesExpectedDiff()
        {
            var validator = new MobaReplayValidator();
            var recorded = Snapshot(1, Actor(11, 1.234f, 0f, 0f, 100f, teamId: 1));
            var replayed = Snapshot(1, Actor(11, 1.235f, 0f, 0f, 99.99f, teamId: 2));

            var comp = validator.CompareFrame(in recorded, in replayed);

            Assert.IsTrue(comp.FrameNumbersMatch);
            Assert.IsTrue(comp.HasDivergence);
            Assert.AreEqual(0, comp.MissingActorIds.Count);
            Assert.AreEqual(0, comp.ExtraActorIds.Count);
            Assert.AreEqual(1, comp.ActorDiffs.Count);
            Assert.IsFalse(comp.ActorDiffs[0].TeamMatches);
        }

        [Test]
        public void CompareFrame_AllMatch_ProducesCleanComparison()
        {
            var validator = new MobaReplayValidator();
            var recorded = Snapshot(1, Actor(11, 0f, 0f, 0f, 100f, teamId: 3));
            var replayed = Snapshot(1, Actor(11, 0f, 0f, 0f, 100f, teamId: 3));

            var comp = validator.CompareFrame(in recorded, in replayed);

            Assert.IsTrue(comp.FrameNumbersMatch);
            Assert.IsFalse(comp.HasDivergence);
            Assert.AreEqual(1, comp.ActorDiffs.Count);
            Assert.IsFalse(comp.ActorDiffs[0].IsDivergent);
        }

        [Test]
        public void EmptyStreams_ProduceEmptyReport()
        {
            var validator = new MobaReplayValidator();
            var report = validator.Compare(
                new List<MobaWorldSnapshotPayload>(),
                new List<MobaWorldSnapshotPayload>(),
                "empty");

            Assert.AreEqual(0, report.TotalComparedFrames);
            Assert.IsTrue(report.IsClean);
        }

        [Test]
        public void MaxComparedFrames_LimitsComparisonWindow()
        {
            var validator = new MobaReplayValidator(new MobaReplayValidatorOptions { MaxComparedFrames = 2 });
            var recorded = new List<MobaWorldSnapshotPayload>
            {
                Snapshot(1, Actor(11, 0f, 0f, 0f, 100f)),
                Snapshot(2, Actor(11, 1f, 0f, 0f, 100f)),
                Snapshot(3, Actor(11, 2f, 0f, 0f, 100f)),
                Snapshot(4, Actor(11, 3f, 0f, 0f, 100f))
            };
            var replayed = new List<MobaWorldSnapshotPayload>
            {
                Snapshot(1, Actor(11, 0f, 0f, 0f, 100f)),
                Snapshot(2, Actor(11, 1f, 0f, 0f, 100f)),
                Snapshot(3, Actor(11, 2f, 0f, 0f, 100f)),
                Snapshot(4, Actor(11, 3f, 0f, 0f, 100f))
            };

            var report = validator.Compare(recorded, replayed);

            Assert.AreEqual(2, report.TotalComparedFrames);
            Assert.AreEqual(2, report.MatchedFrames);
        }

        [Test]
        public void StopOnFirstDivergence_TerminatesEarly()
        {
            var validator = new MobaReplayValidator(new MobaReplayValidatorOptions { StopOnFirstDivergence = true });
            var recorded = new List<MobaWorldSnapshotPayload>
            {
                Snapshot(1, Actor(11, 0f, 0f, 0f, 100f)),
                Snapshot(2, Actor(11, 1f, 0f, 0f, 100f)),
                Snapshot(3, Actor(11, 2f, 0f, 0f, 100f))
            };
            var replayed = new List<MobaWorldSnapshotPayload>
            {
                Snapshot(1, Actor(11, 0f, 0f, 0f, 100f)),
                Snapshot(2, Actor(11, 5f, 0f, 0f, 100f)), // divergent
                Snapshot(3, Actor(11, 2f, 0f, 0f, 100f))
            };

            var report = validator.Compare(recorded, replayed);

            Assert.AreEqual(2, report.TotalComparedFrames);
            Assert.AreEqual(1, report.DivergentFrames);
        }
    }

    public sealed class MobaReplayDeterminismHarnessTests
    {
        private sealed class FakeRunner : IMobaReplayDeterminismRunner
        {
            private readonly IReadOnlyList<MobaWorldSnapshotPayload> _snapshots;
            private readonly bool _fail;
            private readonly string _failure;

            public FakeRunner(string name, IReadOnlyList<MobaWorldSnapshotPayload> snapshots, bool fail = false, string failure = null)
            {
                RunnerName = name;
                _snapshots = snapshots;
                _fail = fail;
                _failure = failure;
            }

            public string RunnerName { get; }

            public bool TryRun(
                IReadOnlyList<PlayerInputCommandBatch> inputSequence,
                out IReadOnlyList<MobaWorldSnapshotPayload> snapshots,
                out string failureReason)
            {
                if (_fail)
                {
                    snapshots = null;
                    failureReason = _failure ?? "fail";
                    return false;
                }
                snapshots = _snapshots;
                failureReason = null;
                return true;
            }
        }

        private static MobaActorSnapshotEntry Actor(int actorId, float x, float y, float z)
        {
            return new MobaActorSnapshotEntry(actorId, x, y, z, 0f, 0f, 0f, 100f, 100f, 1);
        }

        private static MobaWorldSnapshotPayload Snapshot(int frame, params MobaActorSnapshotEntry[] actors)
        {
            return new MobaWorldSnapshotPayload(1UL, frame, frame * 33L, true, actors);
        }

        [Test]
        public void IdenticalRunners_ReportIsDeterministic()
        {
            var sequence = new PlayerInputCommandBatch[]
            {
                new PlayerInputCommandBatch(1, new byte[][] { new byte[] { 0x01 } }),
                new PlayerInputCommandBatch(2, new byte[][] { new byte[] { 0x02 } })
            };
            var snapshots = new List<MobaWorldSnapshotPayload>
            {
                Snapshot(1, Actor(11, 0f, 0f, 0f)),
                Snapshot(2, Actor(11, 1f, 0f, 0f))
            };
            var harness = new MobaReplayDeterminismHarness();

            var result = harness.Run("determinism-1", sequence, new FakeRunner("ref", snapshots), new FakeRunner("cand", snapshots));

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.IsDeterministic);
            Assert.AreEqual(2, result.ReferenceSnapshotCount);
            Assert.AreEqual(2, result.CandidateSnapshotCount);
            Assert.IsTrue(result.Report.IsClean);
        }

        [Test]
        public void ReferenceRunnerFailure_ProducesFailureReason()
        {
            var sequence = new PlayerInputCommandBatch[]
            {
                new PlayerInputCommandBatch(1, new byte[][] { new byte[] { 0x01 } })
            };
            var harness = new MobaReplayDeterminismHarness();

            var result = harness.Run("scenario",
                sequence,
                new FakeRunner("ref", new List<MobaWorldSnapshotPayload>(), fail: true, failure: "boom"),
                new FakeRunner("cand", new List<MobaWorldSnapshotPayload>()));

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsDeterministic);
            StringAssert.Contains("boom", result.FailureReason);
        }

        [Test]
        public void SnapshotCountMismatch_ReportsFailureButStillCompares()
        {
            var sequence = new PlayerInputCommandBatch[]
            {
                new PlayerInputCommandBatch(1, new byte[][] { new byte[] { 0x01 } })
            };
            var refShots = new List<MobaWorldSnapshotPayload>
            {
                Snapshot(1, Actor(11, 0f, 0f, 0f)),
                Snapshot(2, Actor(11, 1f, 0f, 0f))
            };
            var candShots = new List<MobaWorldSnapshotPayload>
            {
                Snapshot(1, Actor(11, 0f, 0f, 0f))
            };
            var harness = new MobaReplayDeterminismHarness();

            var result = harness.Run("mismatch", sequence, new FakeRunner("ref", refShots), new FakeRunner("cand", candShots));

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Report);
            StringAssert.Contains("Snapshot counts differ", result.FailureReason);
        }

        [Test]
        public void DivergentRunners_ProduceDivergentReport()
        {
            var sequence = new PlayerInputCommandBatch[]
            {
                new PlayerInputCommandBatch(1, new byte[][] { new byte[] { 0x01 } })
            };
            var refShots = new List<MobaWorldSnapshotPayload>
            {
                Snapshot(1, Actor(11, 0f, 0f, 0f))
            };
            var candShots = new List<MobaWorldSnapshotPayload>
            {
                Snapshot(1, Actor(11, 5f, 0f, 0f))
            };
            var harness = new MobaReplayDeterminismHarness();

            var result = harness.Run("divergent", sequence, new FakeRunner("ref", refShots), new FakeRunner("cand", candShots));

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(result.IsDeterministic);
            Assert.IsFalse(result.Report.IsClean);
            Assert.AreEqual(1, result.Report.DivergentFrames);
        }
    }

    public sealed class SessionReplayControllerTests
    {
        [TestCase(false, true, true)]
        [TestCase(false, false, false)]
        [TestCase(true, true, false)]
        [TestCase(true, false, false)]
        public void CanUseRollbackSeek_OnlyAllowsLogicOnlyReplayWithRollback(
            bool renderPresentation,
            bool hasRollbackModule,
            bool expected)
        {
            Assert.AreEqual(
                expected,
                SessionReplayController.CanUseRollbackSeek(renderPresentation, hasRollbackModule));
        }

        [Test]
        public void SeekBackward_WithPresentation_RebuildsSessionBetweenSuspendAndRestore()
        {
            var fixture = ReplaySeekFixture.Create(renderPresentation: true);
            try
            {
                var result = fixture.Controller.SeekToFrame(
                    fixture.Plan,
                    fixture.State,
                    fixture.Handles,
                    fixture.Context,
                    fixture.Host,
                    0);

                Assert.IsTrue(result);
                CollectionAssert.AreEqual(
                    new[] { "suspend", "stop", "start", "auto", "restore" },
                    fixture.Calls);
                Assert.AreEqual(0, fixture.State.Tick.LastFrame);
                Assert.AreSame(fixture.Handles.Session, fixture.Context.Session);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void SeekBackward_WithPresentation_RestoresPresentationWhenRestartFails()
        {
            var fixture = ReplaySeekFixture.Create(renderPresentation: true, failRestart: true);
            try
            {
                var result = fixture.Controller.SeekToFrame(
                    fixture.Plan,
                    fixture.State,
                    fixture.Handles,
                    fixture.Context,
                    fixture.Host,
                    0);

                Assert.IsFalse(result);
                CollectionAssert.AreEqual(
                    new[] { "suspend", "stop", "start", "auto", "restore" },
                    fixture.Calls);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private sealed class ReplaySeekFixture : IDisposable
        {
            private ReplaySeekFixture()
            {
            }

            public SessionReplayController Controller { get; private set; }
            public BattleStartPlan Plan { get; private set; }
            public BattleSessionState State { get; private set; }
            public BattleSessionHandles Handles { get; private set; }
            public BattleContext Context { get; private set; }
            public ISessionReplayHost Host { get; private set; }
            public IReadOnlyList<string> Calls => ((RecordingReplayHost)Host).Calls;

            public static ReplaySeekFixture Create(bool renderPresentation, bool failRestart = false)
            {
                var plan = new TestBattleBootstrapper().Build().WithInputReplay("replay-test.json");
                var handles = new BattleSessionHandles();
                handles.Session = CreateSession(plan.World.WorldId);
                handles.Replay.Driver = CreateDriver(plan.World.WorldId);

                var state = new BattleSessionState();
                state.Tick.LastFrame = 5;
                var context = BattleContext.Rent();
                SessionContextBinder.BindRuntimeSession(context, state, handles);

                return new ReplaySeekFixture
                {
                    Controller = new SessionReplayController(),
                    Plan = plan,
                    State = state,
                    Handles = handles,
                    Context = context,
                    Host = new RecordingReplayHost(
                        handles,
                        plan.World.WorldId,
                        renderPresentation,
                        failRestart),
                };
            }

            public void Dispose()
            {
                Handles.Session?.Dispose();
                Handles.Session = null;
                BattleContext.Return(Context);
                Context = null;
            }

            private static BattleLogicSession CreateSession(string worldId)
            {
                return new BattleLogicSession(new BattleLogicSessionOptions
                {
                    WorldId = new WorldId(worldId),
                    PlayerId = "replay-test-player",
                    ScanAllLoadedAssemblies = false,
                    AutoCreateWorld = false,
                    AutoJoin = false,
                });
            }

            private static FrameReplayDriver CreateDriver(string worldId)
            {
                return new FrameReplayDriver(
                    new WorldId(worldId),
                    new FrameRecordFile
                    {
                        Inputs = new List<FrameRecordInputFrame>
                        {
                            new FrameRecordInputFrame { Frame = 5, PlayerId = "replay-test-player" }
                        }
                    });
            }

            private sealed class RecordingReplayHost : ISessionReplayHost
            {
                private readonly BattleSessionHandles _handles;
                private readonly string _worldId;
                private readonly bool _failRestart;

                public RecordingReplayHost(
                    BattleSessionHandles handles,
                    string worldId,
                    bool renderPresentation,
                    bool failRestart)
                {
                    _handles = handles;
                    _worldId = worldId;
                    _failRestart = failRestart;
                    RenderPresentation = renderPresentation;
                }

                public bool RenderPresentation { get; }
                public List<string> Calls { get; } = new List<string>();

                public void StartSession()
                {
                    Calls.Add("start");
                    if (_failRestart) return;

                    _handles.Session = CreateSession(_worldId);
                    _handles.Replay.Driver = CreateDriver(_worldId);
                }

                public void StopSession()
                {
                    Calls.Add("stop");
                    _handles.Session?.Dispose();
                    _handles.Session = null;
                    _handles.Replay.Driver = null;
                }

                public void ApplyAutoPlanActions()
                {
                    Calls.Add("auto");
                }

                public void SuspendReplayPresentation()
                {
                    Calls.Add("suspend");
                }

                public void RestoreReplayPresentation()
                {
                    Calls.Add("restore");
                }

                public float GetFixedDeltaSeconds()
                {
                    return 1f / 30f;
                }
            }
        }
    }

    public sealed class FrameReplayDriverTests
    {
        [Test]
        public void PlaybackControls_ClampSpeedAndPreservePauseState()
        {
            var driver = CreateDriver();

            Assert.IsTrue(driver.IsPlaying);
            Assert.AreEqual(1f, driver.PlaybackSpeed);

            driver.Pause();
            driver.PlaybackSpeed = 0f;

            Assert.IsFalse(driver.IsPlaying);
            Assert.AreEqual(0.1f, driver.PlaybackSpeed);

            driver.Play();
            driver.PlaybackSpeed = 20f;

            Assert.IsTrue(driver.IsPlaying);
            Assert.AreEqual(8f, driver.PlaybackSpeed);
        }

        [Test]
        public void LastFrame_UsesFurthestInputHashOrSnapshot()
        {
            var driver = CreateDriver();

            Assert.AreEqual(40, driver.LastFrame);
        }

        [Test]
        public void Seek_ResetsOneShotHashMismatchState()
        {
            var driver = CreateDriver();

            Assert.IsFalse(driver.TryValidateStateHashOnce(30, 1, 0xBADU, out var firstExpected));
            Assert.AreEqual(0x1234U, firstExpected.Hash);
            Assert.IsTrue(driver.TryValidateStateHashOnce(30, 1, 0xBADU, out var suppressedExpected));
            Assert.IsNull(suppressedExpected);

            driver.SeekToFrame(20);

            Assert.IsFalse(driver.TryValidateStateHashOnce(30, 1, 0xBADU, out var resetExpected));
            Assert.AreEqual(0x1234U, resetExpected.Hash);
        }

        [Test]
        public void Pump_RealLogicSession_SubmitsRecordedInputOnlyAtMatchingFrames()
        {
            var worldId = new WorldId("frame-replay-driver-session");
            var session = CreateLocalMobaSession(worldId);
            var receivedInputs = new List<PlayerInputCommand>();
            session.FrameReceived += packet =>
            {
                if (packet.Inputs == null) return;
                receivedInputs.AddRange(packet.Inputs);
            };

            try
            {
                var firstPayload = MobaMoveCodec.Serialize(0.25f, -0.5f);
                var secondPayload = MobaMoveCodec.Serialize(-1f, 0.75f);
                var driver = new FrameReplayDriver(
                    worldId,
                    new FrameRecordFile
                    {
                        Inputs = new List<FrameRecordInputFrame>
                        {
                            CreateInput(2, "p1", MobaOpCodes.Input.Move, firstPayload),
                            CreateInput(4, "p1", MobaOpCodes.Input.Move, secondPayload),
                        }
                    });

                PumpAndTick(driver, session, 1);
                PumpAndTick(driver, session, 2);
                PumpAndTick(driver, session, 3);
                PumpAndTick(driver, session, 4);

                Assert.AreEqual(2, receivedInputs.Count);
                AssertInput(receivedInputs[0], 2, "p1", MobaOpCodes.Input.Move, firstPayload);
                AssertInput(receivedInputs[1], 4, "p1", MobaOpCodes.Input.Move, secondPayload);

                driver.Pause();
                driver.Pump(session, 5);
                session.Tick(1f / 30f);

                Assert.AreEqual(2, receivedInputs.Count);
            }
            finally
            {
                session.Dispose();
            }
        }

        private static FrameReplayDriver CreateDriver()
        {
            return new FrameReplayDriver(
                new WorldId("battle-world"),
                new FrameRecordFile
                {
                    Inputs = new List<FrameRecordInputFrame>
                    {
                        new FrameRecordInputFrame { Frame = 10, PlayerId = "player", OpCode = 1 }
                    },
                    StateHashes = new List<FrameRecordStateHashFrame>
                    {
                        new FrameRecordStateHashFrame { Frame = 30, Version = 1, Hash = 0x1234U }
                    },
                    Snapshots = new List<FrameRecordSnapshotFrame>
                    {
                        new FrameRecordSnapshotFrame { Frame = 40, OpCode = 2 }
                    }
                });
        }

        private static BattleLogicSession CreateLocalMobaSession(WorldId worldId)
        {
            var plan = new TestBattleBootstrapper().Build();
            var session = new BattleLogicSession(new BattleLogicSessionOptions
            {
                WorldId = worldId,
                PlayerId = "p1",
                ScanAllLoadedAssemblies = true,
                AutoCreateWorld = false,
                AutoJoin = false,
            });
            var createWorld = plan.CreateWorld;
            var worldOptions = SessionMobaWorldBootstrapFactory.CreateWorldOptions(
                plan,
                worldId,
                registerWorldInitData: false);
            session.CreateWorld(new CreateWorldRequest(
                worldOptions,
                createWorld.OpCode,
                createWorld.Payload));

            return session;
        }

        private static FrameRecordInputFrame CreateInput(
            int frame,
            string playerId,
            int opCode,
            byte[] payload)
        {
            return new FrameRecordInputFrame
            {
                Frame = frame,
                PlayerId = playerId,
                OpCode = opCode,
                PayloadBase64 = Convert.ToBase64String(payload),
            };
        }

        private static void PumpAndTick(
            FrameReplayDriver driver,
            BattleLogicSession session,
            int frame)
        {
            driver.Pump(session, frame);
            session.Tick(1f / 30f);
        }

        private static void AssertInput(
            PlayerInputCommand actual,
            int frame,
            string playerId,
            int opCode,
            byte[] payload)
        {
            Assert.AreEqual(frame, actual.Frame.Value);
            Assert.AreEqual(playerId, actual.Player.Value);
            Assert.AreEqual(opCode, actual.OpCode);
            CollectionAssert.AreEqual(payload, actual.Payload);
        }
    }
}