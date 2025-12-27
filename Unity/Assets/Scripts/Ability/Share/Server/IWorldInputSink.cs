using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Ability.Server
{
    public interface IWorldInputSink
    {
        void Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs);
    }
}
