using AbilityKit.Ability.Host.Extensions.Moba.Struct;

namespace AbilityKit.Ability.Host.Extensions.Moba.StartSources
{
    public interface IMobaMatchmakingSpecInbox
    {
        bool TryDequeue(out MobaRoomGameStartSpec spec);
    }
}
