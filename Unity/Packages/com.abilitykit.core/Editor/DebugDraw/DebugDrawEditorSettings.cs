using AbilityKit.Ability.Share.Common.DebugDraw;

namespace AbilityKit.Editor.DebugDraw
{
    public static class DebugDrawEditorSettings
    {
        public static class Masks
        {
            public static readonly DebugDrawMask None = DebugDrawMask.None;
            public static readonly DebugDrawMask Collision = new DebugDrawMask(1 << 0);
            public static readonly DebugDrawMask Targeting = new DebugDrawMask(1 << 1);
            public static readonly DebugDrawMask Nav = new DebugDrawMask(1 << 2);
            public static readonly DebugDrawMask All = DebugDrawMask.All;
        }

        public static bool Enabled { get; set; } = true;

        public static DebugDrawMask EnabledMask { get; set; } = Masks.Collision;

        public static int MaxItemsPerContributor { get; set; } = 2048;

        public static DebugDrawColor CollisionColor { get; set; } = DebugDrawColor.Green;

        public static int CollisionLayerMask { get; set; } = 0;
    }
}
