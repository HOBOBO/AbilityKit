using AbilityKit.Protocol.Moba.StateSync;
using NUnit.Framework;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleHudSkillFailureAckFeedTests
    {
        [Test]
        public void RejectedAckFiltersActorAndSequence()
        {
            var feed = new BattleHudSkillFailureAckFeed();
            var entries = new[]
            {
                new MobaActionAckEntry { ActorId = 8, AuthoritySequence = 12, Reason = (int)MobaActionAckReason.Cooldown },
                new MobaActionAckEntry { ActorId = 7, AuthoritySequence = 11, Reason = (int)MobaActionAckReason.Cooldown },
                new MobaActionAckEntry { ActorId = 7, AuthoritySequence = 13, Accepted = true },
                new MobaActionAckEntry { ActorId = 7, AuthoritySequence = 14, Reason = (int)MobaActionAckReason.ResourceInsufficient }
            };

            Assert.IsTrue(feed.TryReadLatest(entries, 7, out var message));
            Assert.AreEqual(BattleHudSkillFailureText.FormatAck(MobaActionAckReason.ResourceInsufficient), message);
            Assert.IsFalse(feed.TryReadLatest(entries, 7, out _));
            Assert.IsTrue(feed.TryReadLatest(entries, 8, out message));
            Assert.AreEqual(BattleHudSkillFailureText.FormatAck(MobaActionAckReason.Cooldown), message);
        }
    }
}
