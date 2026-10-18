using System;

namespace AbilityKit.Game.Flow
{
    [Flags]
    public enum BattleProjectionViewCapabilities
    {
        Actors = 0,
        Vfx = 1 << 0,
        AreaEffects = 1 << 1,
        FloatingText = 1 << 2,
        Events = 1 << 3,
        Camera = 1 << 4,
        Full = Vfx | AreaEffects | FloatingText | Events | Camera,
    }

    public enum BattleProjectionViewRole
    {
        Prediction,
        Auxiliary,
        Confirmed,
        Spectator,
        Replay,
    }

    [Flags]
    public enum BattleProjectionPermission
    {
        None = 0,
        Prediction = 1 << 0,
        Confirmed = 1 << 1,
        Spectator = 1 << 2,
        Replay = 1 << 3,
    }

    /// <summary>
    /// Client presentation policy. Network authorization must still control which data arrives.
    /// </summary>
    public readonly struct BattleProjectionSelectionPolicy
    {
        public BattleProjectionSelectionPolicy(BattleProjectionPermission permissions)
        {
            Permissions = permissions;
        }

        public BattleProjectionPermission Permissions { get; }

        public bool Allows(BattleProjectionViewRole role)
        {
            switch (role)
            {
                case BattleProjectionViewRole.Prediction:
                    return Has(BattleProjectionPermission.Prediction);
                case BattleProjectionViewRole.Confirmed:
                    return Has(BattleProjectionPermission.Confirmed);
                case BattleProjectionViewRole.Spectator:
                    return Has(BattleProjectionPermission.Spectator);
                case BattleProjectionViewRole.Replay:
                    return Has(BattleProjectionPermission.Replay);
                default:
                    return false;
            }
        }

        private bool Has(BattleProjectionPermission permission) =>
            (Permissions & permission) == permission;
    }
}
