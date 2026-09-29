using System;

namespace AbilityKit.Demo.Moba.Systems.Bootstrap.Flow.Stages
{
    public sealed class MobaTriggerPlanLoadProfile
    {
        public static readonly MobaTriggerPlanLoadProfile Default = new MobaTriggerPlanLoadProfile(
            new[]
            {
                TriggerPlanLoadEntry.File("Main trigger plans", "ability/ability_trigger_plans.json"),
                TriggerPlanLoadEntry.Directory("Ability rules", "ability/rules", "**/*.json"),
                TriggerPlanLoadEntry.File("Moba effect plans", "moba/effect_plans.json"),
            },
            failFastOnDirectoryLoad: true);

        public MobaTriggerPlanLoadProfile(TriggerPlanLoadEntry[] entries, bool failFastOnDirectoryLoad = false)
        {
            Entries = entries ?? Array.Empty<TriggerPlanLoadEntry>();
            FailFastOnDirectoryLoad = failFastOnDirectoryLoad;
        }

        public TriggerPlanLoadEntry[] Entries { get; }

        public bool FailFastOnDirectoryLoad { get; }
    }

    public readonly struct TriggerPlanLoadEntry
    {
        public TriggerPlanLoadEntry(string name, string path, string pattern, bool isDirectory)
        {
            Name = string.IsNullOrEmpty(name) ? path : name;
            Path = path;
            Pattern = pattern;
            IsDirectory = isDirectory;
        }

        public string Name { get; }
        public string Path { get; }
        public string Pattern { get; }
        public bool IsDirectory { get; }

        public static TriggerPlanLoadEntry File(string name, string path)
        {
            return new TriggerPlanLoadEntry(name, path, null, false);
        }

        public static TriggerPlanLoadEntry Directory(string name, string path, string pattern)
        {
            return new TriggerPlanLoadEntry(name, path, pattern, true);
        }
    }
}
