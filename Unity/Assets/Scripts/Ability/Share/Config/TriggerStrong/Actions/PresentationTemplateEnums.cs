using System;

namespace AbilityKit.Ability.Configs
{
    [Serializable]
    public enum PresentationKind
    {
        Vfx = 0,
        Sfx = 1,
        Highlight = 2,
        Decal = 3,
    }

    [Serializable]
    public enum PresentationAttachMode
    {
        None = 0,
        ToActor = 1,
        ToWorldPos = 2,
    }

    [Serializable]
    public enum PresentationStackPolicy
    {
        Replace = 0,
        Ignore = 1,
        Stack = 2,
    }

    [Serializable]
    public enum PresentationStopPolicy
    {
        Auto = 0,
        NeedStopEvent = 1,
    }
}
