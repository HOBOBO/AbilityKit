namespace AbilityKit.Triggering.Runtime.Plan
{
    public enum ETriggerPlanExecutableKind : byte
    {
        Action = 0,
        Sequence = 1,
        Selector = 2,
        Random = 3,
        If = 4,
        Parallel = 5,
        Repeat = 6,
        Until = 7,
        Invert = 8,
        Succeed = 9,
        Fail = 10,
        Scheduled = 11,
        Metadata = 12,
        ForEach = 13,
    }
}
