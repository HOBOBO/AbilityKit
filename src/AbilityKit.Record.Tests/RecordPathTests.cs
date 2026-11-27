using AbilityKit.Core.Recording;
using AbilityKit.Core.Recording.Core;
using AbilityKit.Core.Recording.FrameRecord;
using Xunit;

namespace AbilityKit.Record.Tests;

[Collection(nameof(FrameRecordCodecSelectionCollection))]
public sealed class RecordPathTests
{
    [Fact]
    public void DefaultPathsUseCurrentDirectoryAndPreserveRootedOrEmptyPaths()
    {
        var relative = Path.Combine("replays", "battle.bin");
        Assert.Equal(Path.Combine(Environment.CurrentDirectory, relative), RecordFileStore.ResolvePath(relative));
        Assert.Equal(Path.Combine(Environment.CurrentDirectory, relative), RecordPathResolver.Resolve(relative, persistentDataOnly: true));

        var rooted = Path.Combine(Path.GetTempPath(), relative);
        RecordPathResolver.SetBaseDirectoryProvider(_ => throw new InvalidOperationException("路径无需平台基目录。"));
        try
        {
            Assert.Equal(rooted, RecordFileStore.ResolvePath(rooted));
            Assert.Equal(string.Empty, RecordFileStore.ResolvePath(string.Empty));
            Assert.Null(RecordPathResolver.Resolve(null, persistentDataOnly: true));
        }
        finally
        {
            RecordPathResolver.SetBaseDirectoryProvider(null);
        }
    }

    [Fact]
    public void WritersAndReadersPreservePlatformPolicyAndDefaultFileNames()
    {
        var dir = Directory.CreateTempSubdirectory("abilitykit-record-paths-");
        var persistent = Path.Combine(dir.FullName, "persistent");
        var fallback = Path.Combine(dir.FullName, "fallback");
        var meta = new FrameRecordMeta { WorldId = "path-test" };

        RecordPathResolver.SetBaseDirectoryProvider(persistentOnly => persistentOnly ? persistent : fallback);
        try
        {
            new FrameRecordJsonWriter(string.Empty, meta).Dispose();
            new FrameRecordBinaryWriter(string.Empty, meta).Dispose();
            new FrameRecordOptimizedBinaryWriter(string.Empty, meta).Dispose();

            Assert.True(File.Exists(Path.Combine(fallback, "battle_record.json")));
            Assert.True(File.Exists(Path.Combine(fallback, "battle_record.bin")));
            Assert.True(File.Exists(Path.Combine(persistent, "battle_record.bin")));
            Assert.Equal(meta.WorldId, FrameRecordJsonReader.Load("battle_record.json").Meta.WorldId);
            Assert.Equal(meta.WorldId, FrameRecordBinaryReader.Load("battle_record.bin").Meta.WorldId);
            Assert.Equal(meta.WorldId, FrameRecordOptimizedBinaryReader.Load("battle_record.bin").Meta.WorldId);

            using var session = RecordSessionFactory.CreateDefault();
            RecordSessionFactory.SaveToFile(session, "session.json");
            Assert.True(File.Exists(Path.Combine(fallback, "session.json")));
            using var loaded = RecordSessionFactory.LoadFromFile("session.json");
            Assert.Equal(session.Container.Tracks.Count, loaded.Container.Tracks.Count);
        }
        finally
        {
            RecordPathResolver.SetBaseDirectoryProvider(null);
            File.Delete(Path.Combine(fallback, "session.json"));
            File.Delete(Path.Combine(fallback, "battle_record.json"));
            File.Delete(Path.Combine(fallback, "battle_record.bin"));
            File.Delete(Path.Combine(persistent, "battle_record.bin"));
            if (Directory.Exists(fallback)) Directory.Delete(fallback);
            if (Directory.Exists(persistent)) Directory.Delete(persistent);
            dir.Delete();
        }
    }
}
