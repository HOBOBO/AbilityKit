using System;
using System.IO;
using AbilityKit.Ability.Config;
using AbilityKit.HFSM.Definition;

namespace AbilityKit.Demo.Moba.Services.StateMachine
{
    public static class MobaCharacterHfsmTestFixture
    {
        public const int AttackSlot = 10;
        public const int RPhase = 11;
        /// <summary>
        /// The attack action group committed by the currently running basic
        /// attack cast. AttackSlot is the magazine's next slot and may be
        /// advanced by the launch trigger before the cast leaves casting.
        /// </summary>
        public const int CommittedAttackSlot = 12;

        // Generic sustained ranged-action phases consumed by the character HFSM.
        // Zero means the mode has no active phase; 1/2/3 are aim, regular shot,
        // and final shot. Combat logic owns writes and cleanup of these values.
        public const int RPhaseInactive = 0;
        public const int RPhaseAiming = 1;
        public const int RPhaseShooting = 2;
        public const int RPhaseFinalShot = 3;

        public static StateMachineDefinition CreateDefinition() => MobaCharacterHfsmProfile.LoadDefinition(new SourceLoader(), 3001);
        private sealed class SourceLoader : ITextAssetLoader
        {
            public bool TryLoadBytes(string path, out byte[] bytes) { bytes = null; return false; }
            public bool TryLoadText(string path, out string text)
            {
                var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (directory != null)
                {
                    // Tests run both from a repository checkout (.NET) and a
                    // Unity project whose Packages directory may be linked.
                    foreach (var root in new[] { directory.FullName, Path.Combine(directory.FullName, "Unity") })
                    {
                        var folder = Path.Combine(root, "Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaStateMachineAuthoring");
                        if (!Directory.Exists(folder)) continue;
                        var file = Path.Combine(folder, Path.GetFileName(path) + ".json");
                        text = File.Exists(file) ? File.ReadAllText(file) : null;
                        return text != null;
                    }
                    directory = directory.Parent;
                }
                throw new InvalidOperationException("MOBA authoring test fixture directory not found.");
            }
        }
    }
}
