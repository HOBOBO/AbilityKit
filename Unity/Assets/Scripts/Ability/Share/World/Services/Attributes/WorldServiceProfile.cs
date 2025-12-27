using System;

namespace AbilityKit.Ability.World.Services.Attributes
{
    [Flags]
    public enum WorldServiceProfile
    {
        Default = 1 << 0,
        Client = 1 << 1,
        Server = 1 << 2,
        All = Default | Client | Server
    }
}
