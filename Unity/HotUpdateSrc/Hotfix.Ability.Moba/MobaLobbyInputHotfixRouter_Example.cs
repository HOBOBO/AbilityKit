using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Server;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.World.DI;

namespace Hotfix.Ability.Moba
{
    public sealed class MobaLobbyInputHotfixRouter_Example : IMobaLobbyInputHotfixRouter
    {
        public bool TryHandle(IWorldServices services, FrameIndex frame, PlayerInputCommand cmd)
        {
            if (cmd == null) return false;

            if (cmd.OpCode == (int)MobaOpCode.SkillInput)
            {
                if (cmd.Payload == null || cmd.Payload.Length == 0)
                {
                    Log.Error("[Hotfix] Drop SkillInput with empty payload");
                    return true;
                }
            }

            return false;
        }

        public void Dispose()
        {
        }
    }
}
