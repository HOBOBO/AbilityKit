using System.Text;
using AbilityKit.Ability.Config;
using Xunit;

namespace AbilityKit.Ability.Tests;

[CollectionDefinition(nameof(ConfigDatabaseReloadBusCollection), DisableParallelization = true)]
public sealed class ConfigDatabaseReloadBusCollection;

[Collection(nameof(ConfigDatabaseReloadBusCollection))]
public sealed class ConfigDatabaseFullReloadTests
{
    [Theory]
    [InlineData("source")]
    [InlineData("texts")]
    [InlineData("bytes")]
    [InlineData("mixed")]
    [InlineData("groups")]
    [InlineData("dto")]
    public void Missing_later_table_keeps_both_old_tables_and_success_replaces_them(string mode)
    {
        var database = CreateDatabase(Items, Other);
        Assert.True(database.ReloadFromDtoArrays(Dtos(1, includeOther: true)).Succeeded);
        var oldEntries = database.GetTable<Entry>();
        var oldDtos = database.GetDtoTable<Dto>();
        var oldOtherEntries = database.GetTable<OtherEntry>();
        var oldOtherDtos = database.GetDtoTable<OtherDto>();
        var observed = new List<(ConfigReloadResult Result, long Version, int Entry, int Dto)>();
        void Record(ConfigReloadResult result) => observed.Add((
            result, database.Version, database.GetTable<Entry>().Get(1).Value, database.GetDto<Dto>(1).Value));

        ConfigReloadBus.OnPublished += Record;
        try
        {
            var failure = Reload(database, mode, includeOther: false);
            Assert.False(failure.Succeeded);
            Assert.Equal(1, failure.Version);
            Assert.Equal(1, database.Version);
            Assert.Same(oldEntries, database.GetTable<Entry>());
            Assert.Same(oldDtos, database.GetDtoTable<Dto>());
            Assert.Same(oldOtherEntries, database.GetTable<OtherEntry>());
            Assert.Same(oldOtherDtos, database.GetDtoTable<OtherDto>());

            var success = Reload(database, mode, includeOther: true);
            Assert.True(success.Succeeded);
            Assert.True(success.FullReload);
            Assert.Equal(2, success.Version);
            Assert.NotSame(oldEntries, database.GetTable<Entry>());
            Assert.NotSame(oldDtos, database.GetDtoTable<Dto>());
            Assert.Equal(1, oldEntries.Get(1).Value);
            Assert.Equal(1, oldDtos.Get(1).Value);
            Assert.Collection(observed,
                attempt => Assert.Equal((false, 1L, 1, 1), (attempt.Result.Succeeded, attempt.Version, attempt.Entry, attempt.Dto)),
                attempt => Assert.Equal((true, 2L, 2, 2), (attempt.Result.Succeeded, attempt.Version, attempt.Entry, attempt.Dto)));
        }
        finally
        {
            ConfigReloadBus.OnPublished -= Record;
        }
    }

    [Fact]
    public void Source_falls_back_on_parse_error_but_mixed_dictionary_does_not()
    {
        var database = CreateDatabase(Items);
        var bytes = new Dictionary<string, byte[]> { ["b/items"] = Bytes("bad") };
        var texts = new Dictionary<string, string> { ["b/items"] = "bad", ["items"] = "3" };
        Assert.True(database.Reload(new DictionaryConfigSource(texts, bytes), "b").Succeeded);
        Assert.Equal(3, database.GetDto<Dto>(1).Value);

        var failed = database.ReloadFromMixed(bytes, texts, "b", "b", strict: false);
        Assert.False(failed.Succeeded);
        Assert.StartsWith("Failed to parse config (mixed): items.", failed.Error);
        Assert.Equal(1, database.Version);
        Assert.Equal(3, database.GetDto<Dto>(1).Value);

        bytes["b/items"] = Bytes("4");
        texts["b/items"] = "5";
        Assert.True(database.Reload(new DictionaryConfigSource(texts, bytes), "b").Succeeded);
        Assert.Equal(4, database.GetDto<Dto>(1).Value);
        Assert.True(database.ReloadFromTexts(texts, "b").Succeeded);
        Assert.Equal(5, database.GetDto<Dto>(1).Value);
        Assert.True(database.ReloadFromMixed(bytes, texts, "b", "b").Succeeded);
        Assert.Equal(4, database.GetDto<Dto>(1).Value);
    }

    [Fact]
    public void Empty_binary_at_full_key_masks_bare_binary_but_mixed_can_use_text()
    {
        var database = CreateDatabase(Items);
        var bytes = new Dictionary<string, byte[]> { ["b/items"] = [], ["items"] = Bytes("4") };
        var texts = new Dictionary<string, string> { ["t/items"] = "", ["items"] = "3" };
        Assert.False(database.ReloadFromBytes(bytes, "b").Succeeded);
        Assert.True(database.ReloadFromMixed(bytes, texts, "b", "t").Succeeded);
        Assert.Equal(3, database.GetDto<Dto>(1).Value);
        bytes.Remove("b/items");
        Assert.True(database.ReloadFromBytes(bytes, "b").Succeeded);
        Assert.Equal(4, database.GetDto<Dto>(1).Value);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("mixed")]
    [InlineData("dto")]
    public void Lenient_full_reload_replaces_missing_tables_with_empty_tables(string mode)
    {
        var database = CreateDatabase(Items, Other);
        Assert.True(database.ReloadFromDtoArrays(Dtos(1, includeOther: true)).Succeeded);
        var result = mode switch
        {
            "source" => database.Reload(new DictionaryConfigSource(Texts(false)), null, strict: false),
            "mixed" => database.ReloadFromMixed(new Dictionary<string, byte[]>(), Texts(false), strict: false),
            _ => database.ReloadFromDtoArrays(Dtos(2, includeOther: false), strict: false)
        };
        Assert.True(result.Succeeded);
        Assert.Empty(database.GetTable<OtherEntry>().All());
        Assert.Empty(database.GetDtoTable<OtherDto>().All());
    }

    [Fact]
    public void Group_fallback_uses_the_declaring_deserializer_and_later_definitions_can_replace()
    {
        var database = CreateDatabase();
        var declaring = new ConfigGroup("declaring", new GroupLoader(new Dictionary<string, string>()), new Deserializer(10), Items);
        var supplying = new GroupLoader(new Dictionary<string, string> { ["items"] = "2" });
        Assert.True(database.ReloadFromGroups([declaring, new ConfigGroup("supplying", supplying, new Deserializer(20))]).Succeeded);
        Assert.Equal(12, database.GetDto<Dto>(1).Value);
        Assert.True(database.ReloadFromGroups([declaring, new ConfigGroup("supplying", supplying, new Deserializer(20), Items)]).Succeeded);
        Assert.Equal(22, database.GetDto<Dto>(1).Value);
    }

    private static ConfigReloadResult Reload(ConfigDatabase database, string mode, bool includeOther)
    {
        var texts = Texts(includeOther);
        var bytes = texts.ToDictionary(pair => pair.Key, pair => Bytes(pair.Value));
        return mode switch
        {
            "source" => database.Reload(new DictionaryConfigSource(texts)),
            "texts" => database.ReloadFromTexts(texts),
            "bytes" => database.ReloadFromBytes(bytes),
            "mixed" => database.ReloadFromMixed(bytes, texts),
            "groups" => database.ReloadFromGroups([new ConfigGroup("fixture", new GroupLoader(texts), new Deserializer(), Items, Other)]),
            "dto" => database.ReloadFromDtoArrays(Dtos(2, includeOther)),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    private static readonly ConfigTableDefinition Items = new("items", typeof(Dto), typeof(Entry));
    private static readonly ConfigTableDefinition Other = new("other", typeof(OtherDto), typeof(OtherEntry));

    private static ConfigDatabase CreateDatabase(params ConfigTableDefinition[] definitions) =>
        new(new Registry(definitions), new Deserializer());

    private static Dictionary<string, string> Texts(bool includeOther) => includeOther
        ? new() { ["items"] = "2", ["other"] = "2" }
        : new() { ["items"] = "2" };

    private static Dictionary<Type, Array> Dtos(int value, bool includeOther) => includeOther
        ? new() { [typeof(Dto)] = new[] { new Dto(1, value) }, [typeof(OtherDto)] = new[] { new OtherDto(1, value) } }
        : new() { [typeof(Dto)] = new[] { new Dto(1, value) } };

    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);

    public sealed record Dto(int Id, int Value);
    public sealed record OtherDto(int Id, int Value);
    public sealed class Entry(Dto dto) { public int Value { get; } = dto.Value; }
    public sealed class OtherEntry(OtherDto dto) { public int Value { get; } = dto.Value; }

    private sealed class Registry(ConfigTableDefinition[] definitions) : ConfigTableRegistryBase(definitions);

    private sealed class GroupLoader(IReadOnlyDictionary<string, string> texts) : IConfigGroupLoader
    {
        public string ResourcesDir => "";

        public bool TryLoad(string tableName, out byte[] bytes, out string text)
        {
            bytes = null!;
            return texts.TryGetValue(tableName, out text!);
        }
    }

    private sealed class Deserializer(int offset = 0) : IConfigDeserializer, IConfigGroupDeserializer
    {
        public bool CanHandle(Type type) => type == typeof(Dto) || type == typeof(OtherDto);
        public Array DeserializeText(string text, Type type)
        {
            var value = int.Parse(text) + offset;
            return type == typeof(Dto) ? new[] { new Dto(1, value) } : new[] { new OtherDto(1, value) };
        }
        public Array DeserializeBytes(byte[] bytes, Type type) => DeserializeText(Encoding.UTF8.GetString(bytes), type);
        public Array DeserializeFromText(string text, Type type) => DeserializeText(text, type);
        public Array DeserializeFromBytes(byte[] bytes, Type type) => DeserializeBytes(bytes, type);
    }
}
