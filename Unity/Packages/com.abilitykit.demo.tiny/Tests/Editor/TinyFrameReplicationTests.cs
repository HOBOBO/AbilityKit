using System.Collections.Generic;
using AbilityKit.Demo.Tiny.View;
using AbilityKit.Protocol.Room;
using NUnit.Framework;

namespace AbilityKit.Demo.Tiny.Tests
{
    public sealed class TinyFrameReplicationTests
    {
        [Test]
        public void AuthoritativeLateInputReplaysFromFullBaseline()
        {
            var replication = new TinyFrameReplication();
            var baseline = Baseline();
            replication.ApplyFullSnapshot(in baseline);
            var authoritative = NewBattle();
            authoritative.Submit(1, new TinyInput(1, 0, true));
            authoritative.Tick();
            var frame = new WireRoomFramePush
            {
                WorldId = 7,
                Frame = 0,
                StateHash = authoritative.ComputeHash(),
                Inputs = new[]
                {
                    new WireRoomFrameInput
                    {
                        PlayerId = 1,
                        InputOpCode = TinyBattle.InputOpCode,
                        Payload = new TinyInput(1, 0, true).Encode()
                    }
                }
            };
            replication.ApplyFrame(in frame, 7);

            Assert.That(replication.NeedsFullSnapshot, Is.False);
            Assert.That(replication.TryGetPresentation(7, out var presentation), Is.True);
            Assert.That(presentation.Frame, Is.EqualTo(1));
            Assert.That(presentation.Actors.Find(actor => actor.ActorId == 2).Hp, Is.EqualTo(90));
        }

        [Test]
        public void HashMismatchRequestsFullBaseline()
        {
            var replication = new TinyFrameReplication();
            var baseline = Baseline();
            replication.ApplyFullSnapshot(in baseline);
            var frame = new WireRoomFramePush
            {
                WorldId = 7,
                Frame = 0,
                StateHash = 42,
                Inputs = new WireRoomFrameInput[0]
            };
            replication.ApplyFrame(in frame, 7);
            Assert.That(replication.NeedsFullSnapshot, Is.True);
        }

        [Test]
        public void LocalPredictionReconcilesEarlierAuthorityAndRetainsFutureInput()
        {
            var replication = new TinyFrameReplication();
            var baseline = Baseline();
            replication.ApplyFullSnapshot(in baseline);
            var input = new TinyInput(1, 0, false);
            var inputFrame = replication.ReserveInputFrame();
            replication.PredictLocalInput(inputFrame, 1, input);
            Assert.That(replication.TryGetPresentation(7, out var predicted), Is.True);
            Assert.That(predicted.Actors.Find(actor => actor.ActorId == 1).X, Is.EqualTo(0));

            var authority = NewBattle();
            for (var frameIndex = 0; frameIndex < inputFrame; frameIndex++)
            {
                authority.Tick();
                var previous = Frame(frameIndex, authority.ComputeHash());
                replication.ApplyFrame(in previous, 7);
            }
            authority.Submit(1, input);
            authority.Tick();
            var confirmed = Frame(inputFrame, authority.ComputeHash(), 1, input);
            replication.ApplyFrame(in confirmed, 7);

            Assert.That(replication.NeedsFullSnapshot, Is.False);
            Assert.That(replication.TryGetPresentation(7, out var presentation), Is.True);
            Assert.That(presentation.Actors.Find(actor => actor.ActorId == 1).X, Is.EqualTo(0));
            Assert.That(replication.ReserveInputFrame(), Is.GreaterThan(inputFrame));
        }

        [Test]
        public void MatchingPeriodicSnapshotKeepsPredictedFutureFrame()
        {
            var replication = new TinyFrameReplication();
            var baseline = Baseline();
            replication.ApplyFullSnapshot(in baseline);
            var inputFrame = replication.ReserveInputFrame();
            replication.PredictLocalInput(inputFrame, 1, new TinyInput(1, 0, false));
            var authority = NewBattle();
            authority.Tick();
            var periodic = new WireStateSyncSnapshotPush
            {
                WorldId = 7,
                Frame = authority.Frame,
                IsFullSnapshot = true,
                PayloadOpCode = TinyBattleStateCodec.PayloadOpCode,
                Payload = TinyBattleStateCodec.Encode(authority.CaptureState())
            };
            replication.ApplyFullSnapshot(in periodic);

            Assert.That(replication.TryGetPresentation(7, out var presentation), Is.True);
            Assert.That(presentation.Frame, Is.EqualTo(inputFrame + 1));
            Assert.That(presentation.Actors.Find(actor => actor.ActorId == 1).X, Is.EqualTo(0));
        }

        private static WireRoomFramePush Frame(int frame, uint hash, uint playerId = 0, TinyInput input = default) =>
            new WireRoomFramePush
            {
                WorldId = 7,
                Frame = frame,
                StateHash = hash,
                Inputs = playerId == 0 ? new WireRoomFrameInput[0] : new[]
                {
                    new WireRoomFrameInput
                    {
                        PlayerId = playerId,
                        InputOpCode = TinyBattle.InputOpCode,
                        Payload = input.Encode()
                    }
                }
            };

        private static WireStateSyncSnapshotPush Baseline() => new WireStateSyncSnapshotPush
        {
            WorldId = 7,
            Frame = 0,
            IsFullSnapshot = true,
            PayloadOpCode = TinyBattleStateCodec.PayloadOpCode,
            Payload = TinyBattleStateCodec.Encode(NewBattle().CaptureState())
        };

        private static TinyBattle NewBattle()
        {
            var battle = new TinyBattle();
            battle.AddPlayer(1, -1, 0);
            battle.AddPlayer(2, 1, 0);
            return battle;
        }
    }
}
