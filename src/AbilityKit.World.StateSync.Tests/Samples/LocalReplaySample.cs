using AbilityKit.BattleScenario.Editor;
using Xunit;

namespace AbilityKit.World.StateSync.Tests;

internal static class LocalReplaySample
{
    internal static void VerifyAndExport(string name, LocalStateSyncReplay artifact)
    {
        var fileName = $"{name}-{artifact.Variant}.statesync-replay.json";
        var json = artifact.Serialize();
        Assert.Equal(json, LocalStateSyncReplay.Parse(json).Serialize());

        var directory = Environment.GetEnvironmentVariable("ABILITYKIT_LOCAL_STATESYNC_REPLAY_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, fileName), json);
        }

        var samplePath = Path.Combine(AppContext.BaseDirectory, "Samples", "ReplaySamples", fileName);
        Assert.True(File.Exists(samplePath), $"缺少内置验收录制：{fileName}");
        Assert.Equal(json, LocalStateSyncReplay.Parse(File.ReadAllText(samplePath)).Serialize());
    }
}
