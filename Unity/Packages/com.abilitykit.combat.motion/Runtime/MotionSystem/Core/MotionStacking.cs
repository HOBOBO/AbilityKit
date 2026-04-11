namespace AbilityKit.Core.Common.MotionSystem.Core
{
    public enum MotionStacking
    {
        Additive = 0,

        // Only the highest-priority source in the group is allowed to tick and contribute.
        ExclusiveHighestPriority = 1,

        // Same as ExclusiveHighestPriority for now, but semantically used for "suppresses lower" cases.
        OverrideLowerPriority = 2,
    }
}
