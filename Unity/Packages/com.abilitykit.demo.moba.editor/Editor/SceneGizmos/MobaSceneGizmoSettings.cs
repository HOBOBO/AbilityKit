using AbilityKit.Diagnostics.DebugDraw;
using AbilityKit.Diagnostics.Editor.DebugDraw;
using UnityEditor;

namespace AbilityKit.Game.Editor.Gizmos
{
    /// <summary>
    /// MOBA Scene Gizmo 集中设置：
    /// - 定义三种 Gizmo 专属位（Attack / Buff / Spawn），位于 Targeting 槽之后避免占用 Core 已使用的位。
    /// - 与 <see cref="DebugDrawEditorSettings"/> 协作：每个 Contributor 的 Mask 同时携带 Targeting 位
    ///   （让 Core 驱动能正确派发 OnSceneGUI），并在 Draw() 内部按本类的位决定是否真正绘制。
    /// - 提供 <see cref="RefreshCoreEnabledMask"/> 用于菜单切换后即时同步 Core EnabledMask。
    /// - 持久化通过 <see cref="MobaGizmoSettingsPersistence"/> 写入 EditorPrefs。
    /// </summary>
    public static class MobaSceneGizmoSettings
    {
        public const int AttackBit = 1 << 3;
        public const int BuffBit = 1 << 4;
        public const int SpawnBit = 1 << 5;
        public const int NavigationBit = 1 << 6;
        public const int PathBit = 1 << 7;

        public static class Masks
        {
            public static readonly DebugDrawMask Attack = new DebugDrawMask(AttackBit);
            public static readonly DebugDrawMask Buff = new DebugDrawMask(BuffBit);
            public static readonly DebugDrawMask Spawn = new DebugDrawMask(SpawnBit);

            public static readonly DebugDrawMask AllMoba = new DebugDrawMask(AttackBit | BuffBit | SpawnBit);
        }

        public const int DefaultAttackMask = AttackBit | SpawnBit;
        public const int DefaultBuffMask = BuffBit;

        private static int s_attackColorR = 255;
        private static int s_attackColorG = 96;
        private static int s_attackColorB = 96;

        private static int s_buffColorR = 255;
        private static int s_buffColorG = 200;
        private static int s_buffColorB = 0;

        private static int s_spawnColorR = 64;
        private static int s_spawnColorG = 220;
        private static int s_spawnColorB = 96;

        public static DebugDrawColor AttackColor =>
            new DebugDrawColor((byte)s_attackColorR, (byte)s_attackColorG, (byte)s_attackColorB, 255);

        public static DebugDrawColor BuffColor =>
            new DebugDrawColor((byte)s_buffColorR, (byte)s_buffColorG, (byte)s_buffColorB, 255);

        public static DebugDrawColor SpawnColor =>
            new DebugDrawColor((byte)s_spawnColorR, (byte)s_spawnColorG, (byte)s_spawnColorB, 255);

        public static int MaxBuffPerActor { get; set; } = 8;
        public static int MaxActors { get; set; } = 64;
        public static int MaxAreas { get; set; } = 64;
        public static int MaxNavCells { get; set; } = 2048;

        private static int s_navFreeColorR = 64;
        private static int s_navFreeColorG = 220;
        private static int s_navFreeColorB = 96;
        private static int s_navBlockedColorR = 220;
        private static int s_navBlockedColorG = 64;
        private static int s_navBlockedColorB = 64;
        private static int s_pathColorR = 64;
        private static int s_pathColorG = 192;
        private static int s_pathColorB = 255;

        public static DebugDrawColor NavFreeColor =>
            new DebugDrawColor((byte)s_navFreeColorR, (byte)s_navFreeColorG, (byte)s_navFreeColorB, 80);
        public static DebugDrawColor NavBlockedColor =>
            new DebugDrawColor((byte)s_navBlockedColorR, (byte)s_navBlockedColorG, (byte)s_navBlockedColorB, 120);
        public static DebugDrawColor PathColor =>
            new DebugDrawColor((byte)s_pathColorR, (byte)s_pathColorG, (byte)s_pathColorB, 255);

        public static int GetOrCreateEnabledMask()
        {
            var stored = MobaGizmoSettingsPersistence.LoadEnabledMask();
            return stored == 0 ? DefaultAttackMask | DefaultBuffMask : stored;
        }

        public static bool IsAttackEnabled() => (GetOrCreateEnabledMask() & AttackBit) != 0;
        public static bool IsBuffEnabled() => (GetOrCreateEnabledMask() & BuffBit) != 0;
        public static bool IsSpawnEnabled() => (GetOrCreateEnabledMask() & SpawnBit) != 0;
        public static bool IsNavigationEnabled() => (GetOrCreateEnabledMask() & NavigationBit) != 0;
        public static bool IsPathEnabled() => (GetOrCreateEnabledMask() & PathBit) != 0;

        public static void SetAttackEnabled(bool value) => SetBit(AttackBit, value);
        public static void SetBuffEnabled(bool value) => SetBit(BuffBit, value);
        public static void SetSpawnEnabled(bool value) => SetBit(SpawnBit, value);
        public static void SetNavigationEnabled(bool value) => SetBit(NavigationBit, value);
        public static void SetPathEnabled(bool value) => SetBit(PathBit, value);

        public static void SetBit(int bit, bool enabled)
        {
            var current = GetOrCreateEnabledMask();
            var next = enabled ? (current | bit) : (current & ~bit);
            if (next == current) return;

            MobaGizmoSettingsPersistence.SaveEnabledMask(next);
            RefreshCoreEnabledMask();
        }

        public static void Reset()
        {
            MobaGizmoSettingsPersistence.SaveEnabledMask(DefaultAttackMask | DefaultBuffMask);
            RefreshCoreEnabledMask();
        }

        /// <summary>
        /// 把 Targeting 槽（Core 用作驱动位）以及本类已启用的位合并到 Core 的全局 EnabledMask，
        /// 这样 <see cref="DebugDrawSceneViewDriver"/> 才会调用我们的 Contributors。
        /// </summary>
        public static void RefreshCoreEnabledMask()
        {
            var current = DebugDrawEditorSettings.EnabledMask.Value;
            var mobaBits = GetOrCreateEnabledMask();

            var targeting = DebugDrawEditorSettings.Masks.Targeting.Value;
            var required = current | targeting | mobaBits;

            if (required != current)
            {
                DebugDrawEditorSettings.EnabledMask = new DebugDrawMask(required);
                SceneView.RepaintAll();
            }
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            MobaGizmoSettingsPersistence.EnsureDefaults(DefaultAttackMask | DefaultBuffMask);
            RefreshCoreEnabledMask();
        }
    }
}
