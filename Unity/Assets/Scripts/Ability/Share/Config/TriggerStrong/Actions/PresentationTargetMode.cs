using System;

namespace AbilityKit.Ability.Configs
{
    [Serializable]
    public enum PresentationTargetMode
    {
        Explicit = 0,
        QueryTemplate = 1,
        Source = 2,
        Target = 3,
        Self = 4,
        PayloadAttacker = 5,
        PayloadTarget = 6,
        Position = 7,
    }
}
