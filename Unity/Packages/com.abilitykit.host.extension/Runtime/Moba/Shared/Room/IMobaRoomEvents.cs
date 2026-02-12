using System;

namespace AbilityKit.Ability.Host.Extensions.Moba.Room
{
    public interface IMobaRoomEvents
    {
        void AddChanged(Action<MobaRoomChangedArgs> handler);

        void RemoveChanged(Action<MobaRoomChangedArgs> handler);
    }
}
