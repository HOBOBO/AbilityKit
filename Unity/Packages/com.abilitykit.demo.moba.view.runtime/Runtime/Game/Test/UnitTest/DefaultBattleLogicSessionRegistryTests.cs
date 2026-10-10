using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Ability.Host;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class DefaultBattleLogicSessionRegistryTests
    {
        [Test]
        public void FailedStart_DoesNotPublishDebugFacade()
        {
            var registry = new DefaultBattleLogicSessionRegistry();
            var options = new BattleLogicSessionOptions
            {
                Mode = BattleLogicMode.Remote,
                WorldId = new WorldId("failed-start")
            };

            Assert.Throws<ArgumentNullException>(() => registry.Start(options));

            Assert.That(registry.HasSession, Is.False);
            Assert.That(BattleDebugFacadeProvider.TryGet("failed-start", out _), Is.False);
        }

        [Test]
        public void PublicationFailure_StopsSessionAndWithdrawsDebugFacade()
        {
            var registry = new DefaultBattleLogicSessionRegistry();
            var transport = new DisposableTransport();
            registry.SessionChanged += session =>
            {
                if (session != null) throw new InvalidOperationException("subscriber failed");
            };
            var options = new BattleLogicSessionOptions
            {
                Mode = BattleLogicMode.Remote,
                WorldId = new WorldId("publication-failure"),
                AutoConnect = false,
                AutoCreateWorld = false,
                AutoJoin = false,
            };

            Assert.Throws<InvalidOperationException>(() => registry.Start(options, transport));
            Assert.That(registry.HasSession, Is.False);
            Assert.That(BattleDebugFacadeProvider.TryGet("publication-failure", out _), Is.False);
            Assert.That(transport.DisposeCount, Is.EqualTo(1));
        }

        private sealed class DisposableTransport : IBattleLogicTransport, IDisposable
        {
            public int DisposeCount { get; private set; }
            public event Action<FramePacket> FramePushed;
            public void Connect() { }
            public void Disconnect() { }
            public void SendCreateWorld(CreateWorldRequest request) { }
            public void SendJoin(JoinWorldRequest request) { }
            public void SendLeave(LeaveWorldRequest request) { }
            public void SendInput(SubmitInputRequest request) { }
            public void Dispose() => DisposeCount++;
        }
    }
}
