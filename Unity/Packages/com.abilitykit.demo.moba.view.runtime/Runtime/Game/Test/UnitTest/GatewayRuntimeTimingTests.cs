using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using AbilityKit.Protocol.Moba;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class GatewayRuntimeTimingTests
    {
        [Test]
        public void GatewayRoomPreparationHelper_DetectsGatewayRoomPreparationRequirement()
        {
            var autoJoinPlan = CreateGatewayPlan(worldId: "1001", numericRoomId: 2002, joinRoomId: string.Empty);
            var noGatewayPlan = BattleStartPlanBuilder
                .ForWorld("1001", "battle", "client_1", "7", tickRate: 30, inputDelayFrames: 0)
                .WithHostMode(BattleHostMode.Local)
                .Build();
            var noRoomPlan = BattleStartPlanBuilder
                .ForWorld("1001", "battle", "client_1", "7", tickRate: 30, inputDelayFrames: 0)
                .WithHostMode(BattleHostMode.GatewayRemote)
                .WithGateway(
                    useGatewayTransport: true,
                    host: "127.0.0.1",
                    port: 4000,
                    numericRoomId: 1001,
                    sessionToken: "token",
                    region: "dev",
                    serverId: "local",
                    autoCreateRoom: false,
                    autoJoinRoom: false,
                    joinRoomId: string.Empty,
                    createRoomOpCode: 110,
                    joinRoomOpCode: 111)
                .Build();

            Assert.IsTrue(GatewayRoomPreparationHelper.ShouldPrepareGatewayRoom(autoJoinPlan));
            Assert.IsFalse(GatewayRoomPreparationHelper.ShouldPrepareGatewayRoom(noGatewayPlan));
            Assert.IsFalse(GatewayRoomPreparationHelper.ShouldPrepareGatewayRoom(noRoomPlan));
        }

        [Test]
        public void GatewayRoomPreparationHelper_ResolvesJoinRoomIdByPriority()
        {
            var explicitPlan = CreateGatewayPlan(worldId: "1001", numericRoomId: 2002, joinRoomId: "room_explicit");
            var numericPlan = CreateGatewayPlan(worldId: "1001", numericRoomId: 2002, joinRoomId: string.Empty);
            var worldPlan = CreateGatewayPlan(worldId: "1001", numericRoomId: 0, joinRoomId: string.Empty);

            Assert.AreEqual("room_explicit", GatewayRoomPreparationHelper.ResolveJoinRoomId(explicitPlan));
            Assert.AreEqual("2002", GatewayRoomPreparationHelper.ResolveJoinRoomId(numericPlan));
            Assert.AreEqual("1001", GatewayRoomPreparationHelper.ResolveJoinRoomId(worldPlan));
        }

        [Test]
        public void GatewayRoomPreparationHelper_ThrowsWhenJoinRoomIdCannotBeResolved()
        {
            var plan = CreateGatewayPlan(worldId: "placeholder", numericRoomId: 0, joinRoomId: string.Empty)
                .WithGatewayRoom(string.Empty, 0);

            Assert.Throws<InvalidOperationException>(() => GatewayRoomPreparationHelper.ResolveJoinRoomId(plan));
        }

        [Test]
        public void GatewayRoomPreparationHelper_ResolvesCreatedRoomWorldId()
        {
            var result = new GatewayCreateRoomResult("room_1001", 1001);

            var worldId = GatewayRoomPreparationHelper.ResolveCreatedRoomWorldId(in result);

            Assert.AreEqual("1001", worldId);
        }

        [Test]
        public void GatewayRoomPreparationHelper_ThrowsWhenCreatedRoomNumericIdIsInvalid()
        {
            var result = new GatewayCreateRoomResult("room_invalid", 0);

            Assert.Throws<InvalidOperationException>(() => GatewayRoomPreparationHelper.ResolveCreatedRoomWorldId(in result));
        }

        [Test]
        public void GatewayRoomPreparationHelper_ResolvesCreatedRoomJoinRoomId()
        {
            var explicitResult = new GatewayCreateRoomResult("room_1001", 1001);
            var numericFallbackResult = new GatewayCreateRoomResult(string.Empty, 1001);

            Assert.AreEqual("room_1001", GatewayRoomPreparationHelper.ResolveCreatedRoomJoinRoomId(in explicitResult, 1001));
            Assert.AreEqual("1001", GatewayRoomPreparationHelper.ResolveCreatedRoomJoinRoomId(in numericFallbackResult, 1001));
        }

        [Test]
        public void GatewayRoomPreparationHelper_ResolvesJoinedRoomWorldId()
        {
            var result = new GatewayJoinRoomResult(
                numericRoomId: 1001,
                snapshotJson: string.Empty,
                worldStartAnchor: default);

            var worldId = GatewayRoomPreparationHelper.ResolveJoinedRoomWorldId(in result, "room_1001");

            Assert.AreEqual("1001", worldId);
        }

        [Test]
        public void GatewayRoomPreparationHelper_ThrowsWhenJoinedRoomNumericIdIsInvalid()
        {
            var result = new GatewayJoinRoomResult(
                numericRoomId: 0,
                snapshotJson: string.Empty,
                worldStartAnchor: default);

            Assert.Throws<InvalidOperationException>(() => GatewayRoomPreparationHelper.ResolveJoinedRoomWorldId(in result, "room_invalid"));
        }

        [Test]
        public void GatewayRoomPreparationHelper_RecordsValidWorldStartAnchor()
        {
            var anchors = new Dictionary<WorldId, GatewayWorldStartAnchor>();
            var worldId = new WorldId("1001");
            var anchor = new GatewayWorldStartAnchor(
                startServerTicks: 100,
                serverTickFrequency: 1000,
                startFrame: 12,
                fixedDeltaSeconds: 0.033d);

            var recorded = GatewayRoomPreparationHelper.TryRecordWorldStartAnchor(anchors, worldId, in anchor);

            Assert.IsTrue(recorded);
            Assert.IsTrue(anchors.TryGetValue(worldId, out var stored));
            Assert.AreEqual(1000, stored.ServerTickFrequency);
            Assert.AreEqual(12, stored.StartFrame);
        }

        [Test]
        public void GatewayRoomPreparationHelper_IgnoresInvalidWorldStartAnchor()
        {
            var anchors = new Dictionary<WorldId, GatewayWorldStartAnchor>();
            var worldId = new WorldId("1001");
            var anchor = new GatewayWorldStartAnchor(
                startServerTicks: 100,
                serverTickFrequency: 0,
                startFrame: 12,
                fixedDeltaSeconds: 0.033d);

            var recorded = GatewayRoomPreparationHelper.TryRecordWorldStartAnchor(anchors, worldId, in anchor);

            Assert.IsFalse(recorded);
            Assert.IsFalse(anchors.ContainsKey(worldId));
        }

        [Test]
        public void GatewayTimeSyncHelper_NormalizesRuntimeOptions()
        {
            var raw = new BattleStartPlanTimeSyncOptions(
                opCode: 120,
                intervalMs: 0,
                alpha: 2d,
                timeoutMs: -1,
                idealFrameSafetyConstMarginFrames: 0,
                idealFrameSafetyRttFactor: 0d,
                idealFrameSafetyMinMarginFrames: 0,
                idealFrameSafetyMaxMarginFrames: 0);

            var options = GatewayTimeSyncHelper.ResolveRuntimeOptions(in raw);

            Assert.AreEqual(120u, options.OpCode);
            Assert.AreEqual(1000, options.IntervalMs);
            Assert.AreEqual(1d, options.Alpha);
            Assert.AreEqual(2000, options.TimeoutMs);
        }

        [Test]
        public void GatewayTimeSyncHelper_CalculatesRttAndClockOffset()
        {
            var sample = GatewayTimeSyncHelper.CalculateSample(
                clientSendTicks: 1000,
                clientReceiveTicks: 1300,
                serverNowTicks: 2000,
                serverTickFrequency: 1000,
                localTickFrequency: 1000d);

            Assert.AreEqual(0.3d, sample.RttSeconds, 0.000001d);
            Assert.AreEqual(-0.85d, sample.OffsetSeconds, 0.000001d);
        }

        [Test]
        public void GatewayTimeSyncHelper_ClampsNegativeRtt()
        {
            var sample = GatewayTimeSyncHelper.CalculateSample(
                clientSendTicks: 1300,
                clientReceiveTicks: 1000,
                serverNowTicks: 2000,
                serverTickFrequency: 1000,
                localTickFrequency: 1000d);

            Assert.AreEqual(0d, sample.RttSeconds, 0.000001d);
        }

        [Test]
        public void GatewayTimeSyncHelper_AppliesFirstAndEwmaSamples()
        {
            var firstSample = new GatewayTimeSyncSample(rttSeconds: 0.3d, offsetSeconds: -0.8d);
            var first = GatewayTimeSyncHelper.ApplySample(
                hasClockSync: false,
                currentClockOffsetSecondsEwma: 0d,
                currentRttSecondsEwma: 0d,
                currentSamples: 0,
                sample: in firstSample,
                alpha: 0.5d);
            var secondSample = new GatewayTimeSyncSample(rttSeconds: 0.5d, offsetSeconds: -0.4d);
            var second = GatewayTimeSyncHelper.ApplySample(
                hasClockSync: first.HasClockSync,
                currentClockOffsetSecondsEwma: first.ClockOffsetSecondsEwma,
                currentRttSecondsEwma: first.RttSecondsEwma,
                currentSamples: first.Samples,
                sample: in secondSample,
                alpha: 0.5d);

            Assert.IsTrue(first.HasClockSync);
            Assert.AreEqual(-0.8d, first.ClockOffsetSecondsEwma, 0.000001d);
            Assert.AreEqual(0.3d, first.RttSecondsEwma, 0.000001d);
            Assert.AreEqual(1, first.Samples);
            Assert.AreEqual(-0.6d, second.ClockOffsetSecondsEwma, 0.000001d);
            Assert.AreEqual(0.4d, second.RttSecondsEwma, 0.000001d);
            Assert.AreEqual(2, second.Samples);
        }

        [Test]
        public void GatewayFrameTimingHelper_ResolvesRawMarginAndLimitedFrame()
        {
            var anchor = new GatewayWorldStartAnchor(
                startServerTicks: 1000,
                serverTickFrequency: 1000,
                startFrame: 10,
                fixedDeltaSeconds: 0.1d);
            var timeSync = new BattleStartPlanTimeSyncOptions(
                opCode: 120,
                intervalMs: 1000,
                alpha: 0.5d,
                timeoutMs: 2000,
                idealFrameSafetyConstMarginFrames: 2,
                idealFrameSafetyRttFactor: 1.5d,
                idealFrameSafetyMinMarginFrames: 1,
                idealFrameSafetyMaxMarginFrames: 4);
            var input = new GatewayFrameTimingInput(
                in anchor,
                hasClockSync: true,
                clockOffsetSecondsEwma: 0.5d,
                rttSecondsEwma: 0.2d,
                timeSync: in timeSync);

            var raw = GatewayFrameTimingHelper.ResolveIdealFrameRaw(in input, localNowSeconds: 2.15d);
            var margin = GatewayFrameTimingHelper.ResolveIdealFrameSafetyMarginFrames(in input);
            var limit = GatewayFrameTimingHelper.ResolveIdealFrameLimit(in input, localNowSeconds: 2.15d);

            Assert.AreEqual(16, raw);
            Assert.AreEqual(3, margin);
            Assert.AreEqual(13, limit);
        }

        private static BattleStartPlan CreateGatewayPlan(string worldId, ulong numericRoomId, string joinRoomId)
        {
            return BattleStartPlanBuilder
                .ForWorld(worldId, "battle", "client_1", "7", tickRate: 30, inputDelayFrames: 0)
                .WithHostMode(BattleHostMode.GatewayRemote)
                .WithGateway(
                    useGatewayTransport: true,
                    host: "127.0.0.1",
                    port: 4000,
                    numericRoomId: numericRoomId,
                    sessionToken: "token",
                    region: "dev",
                    serverId: "local",
                    autoCreateRoom: numericRoomId == 0 && string.IsNullOrEmpty(joinRoomId),
                    autoJoinRoom: true,
                    joinRoomId: joinRoomId,
                    createRoomOpCode: 110,
                    joinRoomOpCode: 111)
                .Build();
        }
    }
}
