/*
 * 验证下一主版本前保留的配置文件、缓存及模块描述兼容面。
 * 每项文件测试使用独立临时目录；不修改进程当前目录，也不依赖等待文件时间自然变化。
 */
#pragma warning disable CS0618
using AbilityKit.Core.Configuration;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class ConfigFileCompatibilityContractTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid\0path")]
    public void Flat_file_load_maps_invalid_paths_to_fresh_empty_settings(string? path)
    {
        var calls = 0;
        Dictionary<string, object> Parse(string _) { calls++; return new(); }
        var first = JsonSettingsFiles.LoadFlatOrEmpty(path!, Parse);
        var second = JsonSettingsFiles.LoadFlatOrEmpty(path!, Parse);
        Assert.Empty(first.Values);
        Assert.Empty(second.Values);
        Assert.NotSame(first, second);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Flat_file_load_does_not_deserialize_missing_files_or_directories(bool directory)
    {
        using var files = new TempFiles();
        var path = directory ? files.Root : files.PathFor("missing.json");
        var calls = 0;
        Assert.Empty(JsonSettingsFiles.LoadFlatOrEmpty(path, _ => { calls++; return new(); }).Values);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Flat_file_load_skips_deserialization_for_empty_text_or_null_callback()
    {
        using var files = new TempFiles();
        var path = files.Write("empty.json", "");
        var calls = 0;
        Assert.Empty(JsonSettingsFiles.LoadFlatOrEmpty(path, _ => { calls++; return new(); }).Values);
        Assert.Equal(0, calls);
        File.WriteAllText(path, "有内容");
        Assert.Empty(JsonSettingsFiles.LoadFlatOrEmpty(path, null!).Values);
    }

    [Fact]
    public void Flat_file_load_passes_raw_text_and_copies_only_dictionary_structure_without_caching()
    {
        using var files = new TempFiles();
        var text = "  非 JSON 文本\n";
        var path = files.Write("raw.json", text);
        var shared = new List<int> { 1 };
        var input = new Dictionary<string, object> { ["value"] = shared };
        var calls = 0;
        Dictionary<string, object> Parse(string actual) { Assert.Equal(text, actual); calls++; return input; }
        var first = JsonSettingsFiles.LoadFlatOrEmpty(path, Parse);
        var second = JsonSettingsFiles.LoadFlatOrEmpty(path, Parse);
        input.Clear();
        shared.Add(2);
        Assert.Equal(2, calls);
        Assert.NotSame(first, second);
        Assert.Same(shared, first.Values["value"]);
        Assert.Equal(new[] { 1, 2 }, Assert.IsType<List<int>>(second.Values["value"]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Flat_file_load_returns_empty_when_deserializer_returns_null_or_throws(bool throws)
    {
        using var files = new TempFiles();
        var path = files.Write("bad.json", "data");
        Assert.Empty(JsonSettingsFiles.LoadFlatOrEmpty(path, _ => throws ? throw new FormatException("解析失败") : null!).Values);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid\0path")]
    public void Save_rejects_invalid_paths_before_serialization(string? path)
    {
        var calls = 0;
        Assert.False(JsonSettingsFiles.TrySaveOverrides(path!, new Dictionary<string, object>(), _ => { calls++; return "data"; }));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Save_rejects_null_serializer_without_changing_existing_file()
    {
        using var files = new TempFiles();
        var path = files.Write("keep.json", "old");
        Assert.False(JsonSettingsFiles.TrySaveOverrides(path, null!, null!));
        Assert.Equal("old", File.ReadAllText(path));
    }

    [Fact]
    public void Save_creates_parent_then_passes_original_dictionary_and_writes_unvalidated_text()
    {
        using var files = new TempFiles();
        var path = files.PathFor("nested/settings.json");
        var input = new Dictionary<string, object> { ["key"] = 3 };
        Assert.True(JsonSettingsFiles.TrySaveOverrides(path, input, actual =>
        {
            Assert.True(Directory.Exists(Path.GetDirectoryName(path)));
            Assert.Same(input, actual);
            return "中文原文，不要求 JSON";
        }));
        Assert.Equal("中文原文，不要求 JSON", File.ReadAllText(path));
    }

    [Fact]
    public void Save_converts_null_overrides_and_null_output_to_empty_dictionary_and_empty_file()
    {
        using var files = new TempFiles();
        var path = files.Write("settings.json", "old");
        Assert.True(JsonSettingsFiles.TrySaveOverrides(path, null!, actual => { Assert.Empty(actual); return null!; }));
        Assert.Equal("", File.ReadAllText(path));
    }

    [Fact]
    public void Serialization_failure_keeps_created_directory_without_creating_file()
    {
        using var files = new TempFiles();
        var path = files.PathFor("created/settings.json");
        Assert.False(JsonSettingsFiles.TrySaveOverrides(path, null!, _ => throw new FormatException("序列化失败")));
        Assert.True(Directory.Exists(Path.GetDirectoryName(path)));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Serialization_failure_preserves_preexisting_file_contents()
    {
        using var files = new TempFiles();
        var path = files.Write("settings.json", "old");
        Assert.False(JsonSettingsFiles.TrySaveOverrides(path, null!, _ => throw new FormatException("序列化失败")));
        Assert.Equal("old", File.ReadAllText(path));
    }

    [Fact]
    public void Parent_creation_failure_skips_serializer()
    {
        using var files = new TempFiles();
        var parentFile = files.Write("parent", "file");
        var called = false;
        Assert.False(JsonSettingsFiles.TrySaveOverrides(Path.Combine(parentFile, "settings.json"), null!, _ => { called = true; return "data"; }));
        Assert.False(called);
        Assert.Equal("file", File.ReadAllText(parentFile));
    }

    [Fact]
    public void Target_write_failure_occurs_after_serializer_side_effects()
    {
        using var files = new TempFiles();
        var calls = 0;
        Assert.False(JsonSettingsFiles.TrySaveOverrides(files.Root, null!, _ => { calls++; return "data"; }));
        Assert.Equal(1, calls);
        Assert.True(Directory.Exists(files.Root));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid\0path")]
    public void Persistent_load_maps_invalid_paths_to_null_without_calling_deserializer(string? path)
    {
        var calls = 0;
        Assert.Null(PersistentJsonConfigLoader.TryLoad<Payload>(path!, _ => { calls++; return new(); }));
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Persistent_load_returns_null_for_missing_file_or_directory(bool directory)
    {
        using var files = new TempFiles();
        var calls = 0;
        Assert.Null(PersistentJsonConfigLoader.TryLoad<Payload>(directory ? files.Root : files.PathFor("missing.json"), _ => { calls++; return new(); }));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Null_deserializer_bypasses_even_a_warm_cache()
    {
        using var files = new TempFiles();
        var path = files.Write("cache.json", "data");
        var cached = new Payload();
        Assert.Same(cached, PersistentJsonConfigLoader.TryLoad(path, _ => cached));
        Assert.Null(PersistentJsonConfigLoader.TryLoad<Payload>(path, null));
        Assert.NotSame(cached, PersistentJsonConfigLoader.LoadOrDefault<Payload>(path, null));
    }

    [Fact]
    public void Cache_shares_mutable_object_and_ignores_new_deserializer_for_same_key()
    {
        using var files = new TempFiles();
        var path = files.Write("cache.json", "data");
        var cached = new Payload { Value = "first" };
        Assert.Same(cached, PersistentJsonConfigLoader.TryLoad(path, _ => cached));
        cached.Value = "mutated";
        var again = PersistentJsonConfigLoader.TryLoad<Payload>(path, _ => throw new Exception("命中时不应调用新回调"));
        Assert.Same(cached, again);
        Assert.Equal("mutated", again!.Value);
    }

    [Fact]
    public void Full_path_normalization_shares_cache_between_relative_and_dot_paths()
    {
        using var files = new TempFiles();
        var path = files.Write("cache.json", "data");
        var cached = new Payload();
        PersistentJsonConfigLoader.TryLoad(path, _ => cached);
        var relative = Path.GetRelativePath(Environment.CurrentDirectory, path);
        Assert.Same(cached, PersistentJsonConfigLoader.TryLoad<Payload>(relative, _ => throw new Exception("同一路径")));
        var dotPath = Path.Combine(files.Root, ".", "cache.json");
        Assert.Same(cached, PersistentJsonConfigLoader.TryLoad<Payload>(dotPath, _ => throw new Exception("同一路径")));
    }

    [Fact]
    public void Requested_type_is_part_of_cache_key_even_for_same_runtime_object_type()
    {
        using var files = new TempFiles();
        var path = files.Write("cache.json", "data");
        var typed = new Payload { Value = "typed" };
        var asObject = new Payload { Value = "object" };
        Assert.Same(typed, PersistentJsonConfigLoader.TryLoad<Payload>(path, _ => typed));
        Assert.Same(asObject, PersistentJsonConfigLoader.TryLoad<object>(path, _ => asObject));
        Assert.Same(typed, PersistentJsonConfigLoader.TryLoad<Payload>(path, _ => throw new Exception("应命中")));
    }

    [Fact]
    public void Changed_timestamp_invalidates_cache_without_requiring_changed_content()
    {
        using var files = new TempFiles();
        var path = files.Write("cache.json", "same");
        var first = PersistentJsonConfigLoader.TryLoad(path, _ => new Payload());
        File.SetLastWriteTimeUtc(path, TempFiles.LaterStamp);
        Assert.NotEqual(TempFiles.Stamp, File.GetLastWriteTimeUtc(path));
        var second = PersistentJsonConfigLoader.TryLoad(path, _ => new Payload());
        Assert.NotNull(second);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void Changed_content_and_length_with_same_timestamp_keep_cached_object()
    {
        using var files = new TempFiles();
        var path = files.Write("cache.json", "old");
        var first = PersistentJsonConfigLoader.TryLoad(path, text => new Payload { Value = text });
        files.Write("cache.json", "completely different and longer");
        Assert.Same(first, PersistentJsonConfigLoader.TryLoad<Payload>(path, _ => throw new Exception("时间相同仍命中")));
        Assert.Equal("old", first!.Value);
    }

    [Fact]
    public void Null_deserialization_result_is_cached_but_fallback_defaults_are_not()
    {
        using var files = new TempFiles();
        var path = files.Write("cache.json", "data");
        var calls = 0;
        Payload? Parse(string _) { calls++; return null; }
        Assert.Null(PersistentJsonConfigLoader.TryLoad(path, Parse));
        var firstDefault = PersistentJsonConfigLoader.LoadOrDefault(path, Parse);
        var secondDefault = PersistentJsonConfigLoader.LoadOrDefault(path, Parse);
        Assert.NotSame(firstDefault, secondDefault);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Empty_text_is_not_cached_and_can_be_replaced_without_timestamp_change()
    {
        using var files = new TempFiles();
        var path = files.Write("cache.json", "");
        var calls = 0;
        Assert.Null(PersistentJsonConfigLoader.TryLoad<Payload>(path, _ => { calls++; return new(); }));
        Assert.Equal(0, calls);
        files.Write("cache.json", "data");
        var value = new Payload();
        Assert.Same(value, PersistentJsonConfigLoader.TryLoad(path, _ => value));
    }

    [Fact]
    public void Whitespace_text_is_passed_unmodified_to_deserializer()
    {
        using var files = new TempFiles();
        var path = files.Write("cache.json", " \t\n");
        var value = new Payload();
        Assert.Same(value, PersistentJsonConfigLoader.TryLoad(path, text => { Assert.Equal(" \t\n", text); return value; }));
    }

    [Fact]
    public void Deserializer_failure_does_not_cache_failure_and_same_timestamp_can_retry()
    {
        using var files = new TempFiles();
        var path = files.Write("cache.json", "data");
        Assert.Null(PersistentJsonConfigLoader.TryLoad<Payload>(path, _ => throw new FormatException("解析失败")));
        var retry = new Payload();
        Assert.Same(retry, PersistentJsonConfigLoader.TryLoad(path, _ => retry));
    }

    [Fact]
    public void Missing_file_does_not_return_cached_value_but_recreation_can_revive_old_entry()
    {
        using var files = new TempFiles();
        var path = files.Write("cache.json", "old");
        var first = PersistentJsonConfigLoader.TryLoad(path, _ => new Payload());
        File.Delete(path);
        var calls = 0;
        Assert.Null(PersistentJsonConfigLoader.TryLoad<Payload>(path, _ => { calls++; return new(); }));
        Assert.Equal(0, calls);
        files.Write("cache.json", "new");
        Assert.Same(first, PersistentJsonConfigLoader.TryLoad<Payload>(path, _ => throw new Exception("旧缓存仍存在")));
    }

    [Fact]
    public void Empty_changed_file_does_not_evict_old_cache_entry()
    {
        using var files = new TempFiles();
        var path = files.Write("cache.json", "old");
        var first = PersistentJsonConfigLoader.TryLoad(path, _ => new Payload());
        files.Write("cache.json", "", TempFiles.LaterStamp);
        var calls = 0;
        Assert.Null(PersistentJsonConfigLoader.TryLoad<Payload>(path, _ => { calls++; return new(); }));
        Assert.Equal(0, calls);
        files.Write("cache.json", "new");
        Assert.Same(first, PersistentJsonConfigLoader.TryLoad<Payload>(path, _ => throw new Exception("旧缓存仍存在")));
    }

    [Fact]
    public void Clear_cache_discards_entries_without_disposing_returned_objects()
    {
        using var files = new TempFiles();
        var path = files.Write("cache.json", "data");
        var first = new Payload();
        PersistentJsonConfigLoader.TryLoad(path, _ => first);
        PersistentJsonConfigLoader.ClearCache();
        var next = new Payload();
        Assert.Same(next, PersistentJsonConfigLoader.TryLoad(path, _ => next));
        Assert.Equal(0, first.DisposeCalls);
    }

    [Fact]
    public void Clear_cache_inside_deserializer_does_not_prevent_outer_load_from_committing()
    {
        using var files = new TempFiles();
        var path = files.Write("cache.json", "data");
        var value = new Payload();
        Assert.Same(value, PersistentJsonConfigLoader.TryLoad(path, _ => { PersistentJsonConfigLoader.ClearCache(); return value; }));
        Assert.Same(value, PersistentJsonConfigLoader.TryLoad<Payload>(path, _ => throw new Exception("外层已重新提交")));
    }

    [Fact]
    public async Task Concurrent_misses_can_parse_twice_and_last_commit_wins()
    {
        using var files = new TempFiles();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var path = files.Write("cache.json", "data");
        var slow = new Payload { Value = "slow" };
        var fast = new Payload { Value = "fast" };
        var pending = Task.Run(() => PersistentJsonConfigLoader.TryLoad(path, _ =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("测试同步超时");
            return slow;
        }));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.Same(fast, PersistentJsonConfigLoader.TryLoad(path, _ => fast));
        }
        finally
        {
            release.Set();
            await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Same(slow, await pending);
        Assert.Same(slow, PersistentJsonConfigLoader.TryLoad<Payload>(path, _ => throw new Exception("后提交者已覆盖")));
    }

    [Fact]
    public void File_change_during_deserialization_is_not_a_snapshot_and_invalidates_next_read()
    {
        using var files = new TempFiles();
        var path = files.Write("cache.json", "old");
        var first = PersistentJsonConfigLoader.TryLoad(path, text =>
        {
            files.Write("cache.json", "new", TempFiles.LaterStamp);
            return new Payload { Value = text };
        });
        Assert.Equal("old", first!.Value);
        var second = PersistentJsonConfigLoader.TryLoad(path, text => new Payload { Value = text });
        Assert.Equal("new", second!.Value);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void Default_constructor_failure_is_outside_load_exception_isolation()
    {
        var failure = Assert.ThrowsAny<Exception>(() => PersistentJsonConfigLoader.LoadOrDefault<ThrowingDefault>("", null));
        Assert.Same(ThrowingDefault.Failure, failure.GetBaseException());
    }

    [Theory]
    [InlineData(null, "Type", false)]
    [InlineData("", "Type", false)]
    [InlineData("key", null, false)]
    [InlineData("key", "", false)]
    [InlineData(" ", "\t", true)]
    [InlineData("key", "Unresolvable.Type", true)]
    public void Module_validity_only_checks_nonempty_key_and_type(string? key, string? type, bool expected)
    {
        var config = new ModuleInstallerConfig { ModuleKey = key!, InstallerType = type! };
        Assert.Equal(expected, config.IsValid);
    }

    [Theory]
    [InlineData(null, "InstallAsCurrent")]
    [InlineData("", "InstallAsCurrent")]
    [InlineData(" ", " ")]
    [InlineData("Custom", "Custom")]
    public void Module_method_fallback_does_not_trim_or_validate_name(string? method, string expected)
    {
        var config = new ModuleInstallerConfig { ModuleKey = "key", InstallerType = "Type", InstallerMethod = method! };
        Assert.True(config.IsValid);
        Assert.Equal(expected, config.GetEffectiveMethod());
    }

    [Fact]
    public void Module_lookup_skips_invalid_entries_and_returns_first_valid_duplicate()
    {
        var first = new ModuleInstallerConfig { ModuleKey = "key", InstallerType = "Type" };
        var second = new ModuleInstallerConfig { ModuleKey = "key", InstallerType = "OtherType" };
        var set = new ModuleInstallerConfigSet { Modules = new[] { null!, new ModuleInstallerConfig { ModuleKey = "key" }, first, second } };
        Assert.Same(first, set.FindModule("key"));
        Assert.Null(set.FindModule("KEY"));
        Assert.Null(set.FindModule(" key"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Module_lookup_has_no_match_for_empty_query(string? key)
    {
        var set = new ModuleInstallerConfigSet { Modules = new[] { new ModuleInstallerConfig { ModuleKey = key!, InstallerType = "Type" } } };
        Assert.Null(set.FindModule(key!));
    }

    [Fact]
    public void Module_lookup_accepts_null_array_and_observes_later_array_and_object_mutations()
    {
        var set = new ModuleInstallerConfigSet { Modules = null! };
        Assert.Null(set.FindModule("key"));
        var config = new ModuleInstallerConfig { ModuleKey = "key", InstallerType = "Type" };
        var input = new[] { config };
        set.Modules = input;
        Assert.Same(config, set.FindModule("key"));
        config.ModuleKey = "new";
        Assert.Null(set.FindModule("key"));
        Assert.Same(config, set.FindModule("new"));
        input[0] = new ModuleInstallerConfig();
        Assert.Null(set.FindModule("new"));
        set.Modules = Array.Empty<ModuleInstallerConfig>();
        Assert.Null(set.FindModule("new"));
    }

    private sealed class TempFiles : IDisposable
    {
        public static readonly DateTime Stamp = new(2021, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        public static readonly DateTime LaterStamp = new(2022, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "AbilityKit-core-config-" + Guid.NewGuid().ToString("N"));
        public TempFiles() => Directory.CreateDirectory(Root);
        public string PathFor(string name) => Path.Combine(Root, name);
        public string Write(string name, string text, DateTime? timestamp = null)
        {
            var path = PathFor(name);
            File.WriteAllText(path, text);
            File.SetLastWriteTimeUtc(path, timestamp ?? Stamp);
            Assert.Equal(timestamp ?? Stamp, File.GetLastWriteTimeUtc(path));
            return path;
        }
        public void Dispose()
        {
            PersistentJsonConfigLoader.ClearCache();
            Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class Payload : IDisposable
    {
        public string Value { get; set; } = "";
        public int DisposeCalls { get; private set; }
        public void Dispose() => DisposeCalls++;
    }

    private sealed class ThrowingDefault
    {
        public static readonly Exception Failure = new InvalidOperationException("默认构造失败");
        public ThrowingDefault() => throw Failure;
    }
}
#pragma warning restore CS0618
