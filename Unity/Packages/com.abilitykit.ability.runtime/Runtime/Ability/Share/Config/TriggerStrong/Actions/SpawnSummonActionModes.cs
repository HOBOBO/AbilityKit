using System;

namespace AbilityKit.Ability.Configs
{
    [Serializable]
    public enum SpawnSummonOwnerKeyMode
    {
        None = 0,
        SourceContextId = 1,
        CasterActorId = 2,
    }

    [Serializable]
    public enum SpawnSummonTargetMode
    {
        ExplicitTarget = 0,
        QueryTargets = 1,
    }

    [Serializable]
    public enum SpawnSummonRotationMode
    {
        Caster = 0,
        AimDir = 1,
        FaceTarget = 2,
    }

    [Serializable]
    public enum SpawnSummonPatternMode
    {
        Single = 0,
        Line = 1,
        Ring = 2,
        Arc = 3,
        RandomCircle = 4,
        Grid = 5,
    }

    [Serializable]
    public enum SpawnSummonPerPointRotationMode
    {
        Inherit = 0,
        FaceCenter = 1,
        FaceOutward = 2,
        TangentCw = 3,
        TangentCcw = 4,
        FaceTargetActor = 5,
    }

    [Serializable]
    public enum SpawnSummonPositionMode
    {
        Caster = 0,
        Target = 1,
        AimPos = 2,
        Fixed = 3,
    }
}
