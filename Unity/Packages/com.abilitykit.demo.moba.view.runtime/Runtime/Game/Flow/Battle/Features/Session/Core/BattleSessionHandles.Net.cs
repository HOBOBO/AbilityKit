using AbilityKit.Game.Flow.Battle;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    internal sealed partial class BattleSessionHandles
    {
        internal sealed class NetHandles_Deprecated
        {
            internal BattleSessionNetAdapter Adapter;
            internal IBattleSessionNetAdapterContext Ctx;

            public void Reset()
            {
                Adapter = null;
                Ctx = null;
            }
        }
    }
}
