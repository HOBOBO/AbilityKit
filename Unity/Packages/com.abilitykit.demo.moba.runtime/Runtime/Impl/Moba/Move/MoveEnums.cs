namespace AbilityKit.Ability.Share.Impl.Moba.Move
{
    [System.Flags]
    public enum MobaMoveDisableMask
    {
        None = 0,

        Input = 1 << 0,
        Ability = 1 << 1,
        Control = 1 << 2,

        Horizontal = 1 << 8,
        Vertical = 1 << 9,

        All = ~0,
    }

    public enum MobaMoveGroup
    {
        Locomotion = 0,
        Ability = 1,
        Control = 2,
    }

    public enum MobaMoveKind
    {
        Input = 0,
        Dash = 1,
        Knock = 2,
    }

    public enum MobaMoveStacking
    {
        Reject = 0,
        Override = 1,
        Additive = 2,
        RefreshDuration = 3,
    }
}
