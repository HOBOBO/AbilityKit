using System.Collections.Generic;
using AbilityKit.Ability.Host.Extensions.Moba.Struct;

namespace AbilityKit.Ability.Host.Extensions.Moba.StartSources
{
    public sealed class InMemoryMobaMatchmakingSpecInbox : IMobaMatchmakingSpecInbox
    {
        private readonly Queue<MobaRoomGameStartSpec> _queue = new Queue<MobaRoomGameStartSpec>(4);

        public void Enqueue(in MobaRoomGameStartSpec spec)
        {
            _queue.Enqueue(spec);
        }

        public bool TryDequeue(out MobaRoomGameStartSpec spec)
        {
            if (_queue.Count > 0)
            {
                spec = _queue.Dequeue();
                return true;
            }

            spec = default;
            return false;
        }
    }
}
