using AbilityKit.Ability.Config;
using Xunit;

namespace AbilityKit.Ability.Tests;

public sealed class ConfigDatabaseExtensionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Incremental_reload_preserves_table_identity_and_conversion_for_external_and_fallback_tables(bool external)
    {
        var database = CreateDatabase(external);
        Assert.True(database.ReloadFromDtoArrays(new Dictionary<Type, Array>
        {
            [typeof(Dto)] = new[] { new Dto { Id = 1, Value = 10 } }
        }).Succeeded);
        var dtos = database.GetDtoTable<Dto>();
        var entries = database.GetTable<Entry>();

        var result = database.ReloadIncremental(new[] { new ConfigDatabase.IncrementalChange("items", "2:20") });

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(2, database.Version);
        Assert.Same(dtos, database.GetDtoTable<Dto>());
        Assert.Same(entries, database.GetTable<Entry>());
        Assert.False(dtos.TryGet(1, out _));
        Assert.False(entries.TryGet(1, out _));
        Assert.Equal(20, dtos.Get(2).Value);
        Assert.Equal(40, entries.Get(2).Value);
        Assert.Equal(new[] { 2 }, result.ChangedIds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Conversion_failure_does_not_mutate_existing_tables_or_version(bool external)
    {
        var database = CreateDatabase(external);
        database.ReloadFromDtoArrays(new Dictionary<Type, Array> { [typeof(Dto)] = new[] { new Dto { Id = 1, Value = 10 } } });
        var entries = database.GetTable<Entry>();
        var dtos = database.GetDtoTable<Dto>();

        var result = database.ReloadIncremental(new[]
        {
            new ConfigDatabase.IncrementalChange("items", "2:20"),
            new ConfigDatabase.IncrementalChange("items", "3:-1")
        });

        Assert.False(result.Succeeded);
        Assert.Equal(1, database.Version);
        Assert.Same(entries, database.GetTable<Entry>());
        Assert.Same(dtos, database.GetDtoTable<Dto>());
        Assert.Equal(20, Assert.Single(entries.All()).Value);
        Assert.Equal(10, Assert.Single(dtos.All()).Value);
    }

    [Fact]
    public void External_table_without_replacement_capability_is_rejected_before_any_table_changes()
    {
        var definition = new ConfigTableDefinition("items", typeof(Dto), typeof(Entry), null!,
            values => new NonReplacingDtoTable(((Dto[])values).ToDictionary(x => x.Id)),
            values => new ExternalTable<Entry>(((Dto[])values).ToDictionary(x => x.Id, x => new Entry(x))));
        var database = new ConfigDatabase(new Registry(definition), new Deserializer());
        database.ReloadFromDtoArrays(new Dictionary<Type, Array> { [typeof(Dto)] = new[] { new Dto { Id = 1, Value = 10 } } });
        var dtos = database.GetDtoTable<Dto>();
        var entries = database.GetTable<Entry>();

        var result = database.ReloadIncremental(new[] { new ConfigDatabase.IncrementalChange("items", "2:20") });

        Assert.False(result.Succeeded);
        Assert.Contains("Table replacement is not supported", result.Error);
        Assert.Equal(1, database.Version);
        Assert.Same(dtos, database.GetDtoTable<Dto>());
        Assert.Same(entries, database.GetTable<Entry>());
        Assert.Equal(10, Assert.Single(dtos.All()).Value);
        Assert.Equal(20, Assert.Single(entries.All()).Value);
    }

    private sealed class NonReplacingDtoTable(Dictionary<int, Dto> values) : IDtoTable<Dto>
    {
        public int Count => values.Count;
        public Dto Get(int id) => values[id];
        public bool TryGet(int id, out Dto dto) => values.TryGetValue(id, out dto!);
        public IEnumerable<Dto> All() => values.Values;
    }

    private static ConfigDatabase CreateDatabase(bool external)
    {
        var definition = external
            ? new ConfigTableDefinition("items", typeof(Dto), typeof(Entry), null!,
                values => new ExternalTable<Dto>(((Dto[])values).ToDictionary(x => x.Id)),
                values => new ExternalTable<Entry>(((Dto[])values).ToDictionary(x => x.Id, x => new Entry(x))))
            : new ConfigTableDefinition("items", typeof(Dto), typeof(Entry));
        return new ConfigDatabase(new Registry(definition), new Deserializer());
    }

    public sealed class Dto
    {
        public int Id { get; init; }
        public int Value { get; init; }
    }

    public sealed class Entry
    {
        public Entry(Dto dto)
        {
            if (dto.Value < 0) throw new InvalidOperationException("conversion failed");
            Value = dto.Value * 2;
        }
        public int Value { get; }
    }

    private sealed class Registry(ConfigTableDefinition definition) : ConfigTableRegistryBase(new[] { definition });

    private sealed class Deserializer : IConfigDeserializer
    {
        public Array DeserializeText(string text, Type targetType)
        {
            var values = text.Split(':');
            return new[] { new Dto { Id = int.Parse(values[0]), Value = int.Parse(values[1]) } };
        }
        public Array DeserializeBytes(byte[] bytes, Type targetType) => throw new NotSupportedException();
        public bool CanHandle(Type targetType) => targetType == typeof(Dto);
    }

    private sealed class ExternalTable<T>(Dictionary<int, T> values)
        : IConfigTable<T>, IDtoTable<T>, IConfigTableContentReplacer where T : class
    {
        private Dictionary<int, T> _values = values;
        public int Count => _values.Count;
        public T Get(int id) => _values[id];
        public bool TryGet(int id, out T value) => _values.TryGetValue(id, out value!);
        public IEnumerable<T> All() => _values.Values;
        public void ReplaceContentsFrom(object source) => _values = new Dictionary<int, T>(((ExternalTable<T>)source)._values);
    }
}
