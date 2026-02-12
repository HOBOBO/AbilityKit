using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Struct;

namespace AbilityKit.Ability.Host.Extensions.Moba.StartSources
{
    public sealed class MatchmakingGameStartSource : IMobaGameStartSource
    {
        private readonly IMobaMatchmakingSpecInbox _inbox;

        public MobaGameStartSourceKind Kind => MobaGameStartSourceKind.Matchmaking;

        public MatchmakingGameStartSource(IMobaMatchmakingSpecInbox inbox)
        {
            _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
        }

        public bool TryBuild(PlayerId localPlayerId, out MobaRoomGameStartSpec spec)
        {
            if (string.IsNullOrEmpty(localPlayerId.Value))
            {
                spec = default;
                return false;
            }

            return _inbox.TryDequeue(out spec);
        }
    }
}
