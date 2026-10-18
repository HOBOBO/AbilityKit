using AbilityKit.Demo.Moba.Config.BattleDemo;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Ability.Config;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests;

public sealed class MobaConfigTableManifestTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConfigTableDefinition_RejectsPartialFactoryConfiguration(bool dtoFactoryOnly)
    {
        Func<Array, object> factory = source => source;

        Assert.Throws<ArgumentException>(() => new ConfigTableDefinition(
            "skills",
            typeof(LegacyDto),
            typeof(LegacyMo),
            "LegacyJson",
            dtoFactoryOnly ? factory : null,
            dtoFactoryOnly ? null : factory));
    }

    [Fact]
    public void ConfigTableDefinition_AcceptsFactoryPair()
    {
        Func<Array, object> factory = source => source;

        var definition = new ConfigTableDefinition(
            "skills",
            typeof(LegacyDto),
            typeof(LegacyMo),
            "LegacyJson",
            factory,
            factory);

        Assert.Same(factory, definition.DtoTableFactory);
        Assert.Same(factory, definition.EntryTableFactory);
    }

    [Fact]
    public void ConfigTableDefinition_PreservesLegacyConstructor()
    {
        var definition = new ConfigTableDefinition(
            "skills",
            typeof(LegacyDto),
            typeof(LegacyMo));

        Assert.Null(definition.DtoTableFactory);
        Assert.Null(definition.EntryTableFactory);
        Assert.Null(definition.ChangedIdCollector);
    }

    [Fact]
    public void RegistryAndLegacyGroup_UseTheSameGeneratedTableSet()
    {
        AppContext.SetSwitch("AbilityKit.Moba.DisableConfigTableReflectionFallback", true);
        MobaRuntimeConfigTableRegistry.Entry[] registryTables;
        IReadOnlyList<AbilityKit.Ability.Config.ConfigTableDefinition> groupTables;
        try
        {
            registryTables = MobaRuntimeConfigTableRegistry.Tables;
            groupTables = MobaConfigGroups.LegacyJson.Tables;
        }
        finally
        {
            AppContext.SetSwitch("AbilityKit.Moba.DisableConfigTableReflectionFallback", false);
        }

        Assert.Equal(26, registryTables.Length);
        Assert.Equal(registryTables.Length, groupTables.Count);
        for (var i = 0; i < registryTables.Length; i++)
        {
            Assert.Equal(registryTables[i].FilePath, groupTables[i].FilePath);
            Assert.Equal(registryTables[i].DtoType, groupTables[i].DtoType);
            Assert.Equal(registryTables[i].EntryType, groupTables[i].EntryType);
            Assert.Equal(ConfigGroupNames.LegacyJson, groupTables[i].GroupName);
            Assert.NotNull(registryTables[i].DtoTableFactory);
            Assert.NotNull(registryTables[i].EntryTableFactory);
            Assert.NotNull(registryTables[i].ChangedIdCollector);
            Assert.NotNull(groupTables[i].DtoTableFactory);
            Assert.NotNull(groupTables[i].EntryTableFactory);
            Assert.NotNull(groupTables[i].ChangedIdCollector);
        }

        Assert.Contains(registryTables, table => table.FilePath == MobaConfigPaths.SummonAttrInheritsFile);
        Assert.Contains(registryTables, table => table.FilePath == MobaConfigPaths.BattleMapsFile);
    }

    [Fact]
    public void IncrementalReload_UsesGeneratedFactoriesAndPreservesConversions()
    {
        var definition = Assert.Single(
            MobaRuntimeConfigTableRegistry.Tables,
            table => table.DtoType == typeof(SkillDTO));
        var database = new ConfigDatabase(
            new LegacyRegistry(definition),
            JsonNetConfigDeserializer.Instance);
        var initialDto = new SkillDTO
        {
            Id = 700,
            Name = "Initial Skill",
            SkillType = (int)SkillType.NormalAttack,
            Tags = new[] { 1 },
        };
        var loadResult = database.ReloadFromDtoArrays(
            new Dictionary<Type, Array> { [typeof(SkillDTO)] = new[] { initialDto } });
        Assert.True(loadResult.Succeeded, loadResult.Error);

        var dtoTable = database.GetDtoTable<SkillDTO>();
        var entryTable = database.GetTable<SkillMO>();
        var reloadResult = database.ReloadIncremental(new[]
        {
            new ConfigDatabase.IncrementalChange(
                definition.FilePath,
                "[{\"Id\":701,\"Name\":\"Updated Skill\",\"SkillType\":2,\"Tags\":null}]")
        });

        Assert.True(reloadResult.Succeeded, reloadResult.Error);
        Assert.Same(dtoTable, database.GetDtoTable<SkillDTO>());
        Assert.Same(entryTable, database.GetTable<SkillMO>());
        Assert.False(dtoTable.TryGet(initialDto.Id, out _));
        Assert.False(entryTable.TryGet(initialDto.Id, out _));
        Assert.True(dtoTable.TryGet(701, out var updatedDto));
        Assert.Equal("Updated Skill", updatedDto.Name);
        Assert.True(entryTable.TryGet(701, out var updatedEntry));
        Assert.Equal(SkillType.Active, updatedEntry.SkillType);
        Assert.Empty(updatedEntry.Tags);
        Assert.Equal(new[] { 701 }, reloadResult.ChangedIds);
    }

    [Fact]
    public void GeneratedFactories_PreserveCustomDtoToMoConstructorSemantics()
    {
        var definition = Assert.Single(
            MobaRuntimeConfigTableRegistry.Tables,
            table => table.DtoType == typeof(SkillDTO));
        var dto = new SkillDTO
        {
            Id = 701,
            Name = "Generated Factory Skill",
            SkillType = (int)SkillType.Ultimate,
            Tags = null,
        };

        var dtoTable = Assert.IsAssignableFrom<IDtoTable<SkillDTO>>(
            definition.DtoTableFactory(new[] { dto }));
        var entryTable = Assert.IsAssignableFrom<IConfigTable<SkillMO>>(
            definition.EntryTableFactory(new[] { dto }));

        Assert.True(dtoTable.TryGet(dto.Id, out var storedDto));
        Assert.Same(dto, storedDto);
        Assert.True(entryTable.TryGet(dto.Id, out var skill));
        Assert.Equal(SkillType.Ultimate, skill.SkillType);
        Assert.Empty(skill.Tags);

        var changedIds = new HashSet<int>();
        definition.ChangedIdCollector(new[] { dto }, changedIds);
        Assert.Equal(new[] { dto.Id }, changedIds);
    }

    [Fact]
    public void ConfigDatabase_KeepsReflectionFallbackForDefinitionsWithoutFactories()
    {
        var definition = new ConfigTableDefinition(
            "legacy",
            typeof(LegacyDto),
            typeof(LegacyMo));
        var database = new ConfigDatabase(
            new LegacyRegistry(definition),
            JsonNetConfigDeserializer.Instance);
        var dto = new LegacyDto { Id = 17, Value = "kept" };

        var result = database.ReloadFromDtoArrays(
            new Dictionary<Type, Array> { [typeof(LegacyDto)] = new[] { dto } });

        Assert.True(result.Succeeded, result.Error);
        Assert.True(database.GetDtoTable<LegacyDto>().TryGet(dto.Id, out var storedDto));
        Assert.Same(dto, storedDto);
        Assert.True(database.GetTable<LegacyMo>().TryGet(dto.Id, out var entry));
        Assert.Equal(dto.Value, entry.Value);
    }

    [Fact]
    public void ConfigDatabase_IncrementalReflectionFallbackRebuildsBothTablesInPlace()
    {
        var definition = new ConfigTableDefinition(
            "legacy",
            typeof(LegacyDto),
            typeof(LegacyMo));
        var database = new ConfigDatabase(
            new LegacyRegistry(definition),
            JsonNetConfigDeserializer.Instance);
        var initialDto = new LegacyDto { Id = 17, Value = "initial" };
        var loadResult = database.ReloadFromDtoArrays(
            new Dictionary<Type, Array> { [typeof(LegacyDto)] = new[] { initialDto } });
        Assert.True(loadResult.Succeeded, loadResult.Error);

        var dtoTable = database.GetDtoTable<LegacyDto>();
        var entryTable = database.GetTable<LegacyMo>();
        var reloadResult = database.ReloadIncremental(new[]
        {
            new ConfigDatabase.IncrementalChange(
                definition.FilePath,
                "[{\"Id\":18,\"Value\":\"updated\"}]")
        });

        Assert.True(reloadResult.Succeeded, reloadResult.Error);
        Assert.Same(dtoTable, database.GetDtoTable<LegacyDto>());
        Assert.Same(entryTable, database.GetTable<LegacyMo>());
        Assert.False(dtoTable.TryGet(initialDto.Id, out _));
        Assert.False(entryTable.TryGet(initialDto.Id, out _));
        Assert.True(dtoTable.TryGet(18, out var updatedDto));
        Assert.Equal("updated", updatedDto.Value);
        Assert.True(entryTable.TryGet(18, out var updatedEntry));
        Assert.Equal("updated", updatedEntry.Value);
        Assert.Equal(new[] { 18 }, reloadResult.ChangedIds);
    }

    [Fact]
    public void ConfigDatabase_IncrementalDeletionRequiresFullReloadWithoutMutation()
    {
        var definition = new ConfigTableDefinition(
            "legacy",
            typeof(LegacyDto),
            typeof(LegacyMo));
        var database = new ConfigDatabase(
            new LegacyRegistry(definition),
            JsonNetConfigDeserializer.Instance);
        var dto = new LegacyDto { Id = 17, Value = "kept" };
        var loadResult = database.ReloadFromDtoArrays(
            new Dictionary<Type, Array> { [typeof(LegacyDto)] = new[] { dto } });
        Assert.True(loadResult.Succeeded, loadResult.Error);
        var version = database.Version;

        var deleteResult = database.ReloadIncremental(new[]
        {
            new ConfigDatabase.IncrementalChange(definition.FilePath, (string)null)
        });

        Assert.False(deleteResult.Succeeded);
        Assert.Contains("requires full reload", deleteResult.Error, StringComparison.Ordinal);
        Assert.Equal(version, database.Version);
        Assert.True(database.GetDtoTable<LegacyDto>().TryGet(dto.Id, out _));
        Assert.True(database.GetTable<LegacyMo>().TryGet(dto.Id, out _));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConfigDatabase_IncrementalConversionFailureDoesNotPartiallyCommit(bool useGeneratedDelegates)
    {
        var definition = useGeneratedDelegates
            ? new ConfigTableDefinition(
                "transactional",
                typeof(LegacyDto),
                typeof(LegacyMo),
                null,
                source => ConfigTableFactory.CreateDtoTable<LegacyDto>(source, dto => dto.Id),
                source => ConfigTableFactory.CreateEntryTable<LegacyDto, LegacyMo>(
                    source,
                    dto => dto.Id,
                    dto => new LegacyMo(dto)),
                (source, changedIds) => ConfigTableFactory.CollectChangedIds<LegacyDto>(
                    source,
                    changedIds,
                    dto => dto.Id))
            : new ConfigTableDefinition(
                "transactional",
                typeof(LegacyDto),
                typeof(LegacyMo));
        var database = new ConfigDatabase(
            new LegacyRegistry(definition),
            JsonNetConfigDeserializer.Instance);
        var initialDto = new LegacyDto { Id = 17, Value = "initial" };
        var loadResult = database.ReloadFromDtoArrays(
            new Dictionary<Type, Array> { [typeof(LegacyDto)] = new[] { initialDto } });
        Assert.True(loadResult.Succeeded, loadResult.Error);
        var dtoTable = database.GetDtoTable<LegacyDto>();
        var entryTable = database.GetTable<LegacyMo>();
        var version = database.Version;

        var reloadResult = database.ReloadIncremental(new[]
        {
            new ConfigDatabase.IncrementalChange(
                definition.FilePath,
                "[{\"Id\":18,\"Value\":\"candidate\"},{\"Id\":19,\"Value\":\"throw\"}]")
        });

        Assert.False(reloadResult.Succeeded);
        Assert.Equal(version, database.Version);
        Assert.Same(dtoTable, database.GetDtoTable<LegacyDto>());
        Assert.Same(entryTable, database.GetTable<LegacyMo>());
        Assert.True(dtoTable.TryGet(initialDto.Id, out var storedDto));
        Assert.Equal("initial", storedDto.Value);
        Assert.True(entryTable.TryGet(initialDto.Id, out var storedEntry));
        Assert.Equal("initial", storedEntry.Value);
        Assert.False(dtoTable.TryGet(18, out _));
        Assert.False(dtoTable.TryGet(19, out _));
        Assert.False(entryTable.TryGet(18, out _));
        Assert.False(entryTable.TryGet(19, out _));
    }

    [Fact]
    public void ConfigDatabase_IncrementalBatchFailureDoesNotCommitEarlierTables()
    {
        var firstDefinition = new ConfigTableDefinition(
            "first",
            typeof(LegacyDto),
            typeof(LegacyMo));
        var secondDefinition = new ConfigTableDefinition(
            "second",
            typeof(SecondaryDto),
            typeof(SecondaryMo));
        var database = new ConfigDatabase(
            new LegacyRegistry(firstDefinition, secondDefinition),
            JsonNetConfigDeserializer.Instance);
        var firstDto = new LegacyDto { Id = 17, Value = "first-initial" };
        var secondDto = new SecondaryDto { Id = 27, Value = "second-initial" };
        var loadResult = database.ReloadFromDtoArrays(new Dictionary<Type, Array>
        {
            [typeof(LegacyDto)] = new[] { firstDto },
            [typeof(SecondaryDto)] = new[] { secondDto },
        });
        Assert.True(loadResult.Succeeded, loadResult.Error);
        var version = database.Version;

        var reloadResult = database.ReloadIncremental(new[]
        {
            new ConfigDatabase.IncrementalChange(
                firstDefinition.FilePath,
                "[{\"Id\":18,\"Value\":\"first-candidate\"}]"),
            new ConfigDatabase.IncrementalChange(
                secondDefinition.FilePath,
                "[{\"Id\":28,\"Value\":\"throw\"}]")
        });

        Assert.False(reloadResult.Succeeded);
        Assert.Equal(version, database.Version);
        Assert.True(database.GetDtoTable<LegacyDto>().TryGet(firstDto.Id, out _));
        Assert.True(database.GetTable<LegacyMo>().TryGet(firstDto.Id, out _));
        Assert.False(database.GetDtoTable<LegacyDto>().TryGet(18, out _));
        Assert.False(database.GetTable<LegacyMo>().TryGet(18, out _));
        Assert.True(database.GetDtoTable<SecondaryDto>().TryGet(secondDto.Id, out _));
        Assert.True(database.GetTable<SecondaryMo>().TryGet(secondDto.Id, out _));
        Assert.False(database.GetDtoTable<SecondaryDto>().TryGet(28, out _));
        Assert.False(database.GetTable<SecondaryMo>().TryGet(28, out _));
    }

    [Fact]
    public void ConfigDatabase_IncrementalBatchCommitsAllTablesAndIncrementsVersionOnce()
    {
        var firstDefinition = new ConfigTableDefinition(
            "first",
            typeof(LegacyDto),
            typeof(LegacyMo));
        var secondDefinition = new ConfigTableDefinition(
            "second",
            typeof(SecondaryDto),
            typeof(SecondaryMo));
        var database = new ConfigDatabase(
            new LegacyRegistry(firstDefinition, secondDefinition),
            JsonNetConfigDeserializer.Instance);
        var firstDto = new LegacyDto { Id = 17, Value = "first-initial" };
        var secondDto = new SecondaryDto { Id = 27, Value = "second-initial" };
        var loadResult = database.ReloadFromDtoArrays(new Dictionary<Type, Array>
        {
            [typeof(LegacyDto)] = new[] { firstDto },
            [typeof(SecondaryDto)] = new[] { secondDto },
        });
        Assert.True(loadResult.Succeeded, loadResult.Error);
        var firstTable = database.GetTable<LegacyMo>();
        var secondTable = database.GetTable<SecondaryMo>();
        var version = database.Version;

        var reloadResult = database.ReloadIncremental(new[]
        {
            new ConfigDatabase.IncrementalChange(
                firstDefinition.FilePath,
                "[{\"Id\":18,\"Value\":\"first-updated\"}]"),
            new ConfigDatabase.IncrementalChange(
                secondDefinition.FilePath,
                "[{\"Id\":28,\"Value\":\"second-updated\"}]")
        });

        Assert.True(reloadResult.Succeeded, reloadResult.Error);
        Assert.Equal(version + 1, database.Version);
        Assert.Same(firstTable, database.GetTable<LegacyMo>());
        Assert.Same(secondTable, database.GetTable<SecondaryMo>());
        Assert.True(firstTable.TryGet(18, out var firstEntry));
        Assert.Equal("first-updated", firstEntry.Value);
        Assert.True(secondTable.TryGet(28, out var secondEntry));
        Assert.Equal("second-updated", secondEntry.Value);
        Assert.Equal(new[] { 18, 28 }, reloadResult.ChangedIds.OrderBy(id => id));
    }

    [Fact]
    public void ConfigDatabase_IncrementalBatchUsesLastChangeForSameTableAndCollectsAllIds()
    {
        var definition = new ConfigTableDefinition(
            "legacy",
            typeof(LegacyDto),
            typeof(LegacyMo));
        var database = new ConfigDatabase(
            new LegacyRegistry(definition),
            JsonNetConfigDeserializer.Instance);
        var initialDto = new LegacyDto { Id = 17, Value = "initial" };
        var loadResult = database.ReloadFromDtoArrays(
            new Dictionary<Type, Array> { [typeof(LegacyDto)] = new[] { initialDto } });
        Assert.True(loadResult.Succeeded, loadResult.Error);
        var entryTable = database.GetTable<LegacyMo>();

        var reloadResult = database.ReloadIncremental(new[]
        {
            new ConfigDatabase.IncrementalChange(
                definition.FilePath,
                "[{\"Id\":18,\"Value\":\"first-candidate\"}]"),
            new ConfigDatabase.IncrementalChange(
                definition.FilePath,
                "[{\"Id\":19,\"Value\":\"last-candidate\"}]")
        });

        Assert.True(reloadResult.Succeeded, reloadResult.Error);
        Assert.Same(entryTable, database.GetTable<LegacyMo>());
        Assert.False(entryTable.TryGet(initialDto.Id, out _));
        Assert.False(entryTable.TryGet(18, out _));
        Assert.True(entryTable.TryGet(19, out var finalEntry));
        Assert.Equal("last-candidate", finalEntry.Value);
        Assert.Equal(new[] { 18, 19 }, reloadResult.ChangedIds.OrderBy(id => id));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConfigDatabase_IncrementalBatchInstallsNewTablesOnlyAfterWholeBatchSucceeds(
        bool secondTableSucceeds)
    {
        var firstDefinition = new ConfigTableDefinition(
            "first",
            typeof(LegacyDto),
            typeof(LegacyMo));
        var secondDefinition = new ConfigTableDefinition(
            "second",
            typeof(SecondaryDto),
            typeof(SecondaryMo));
        var database = new ConfigDatabase(
            new LegacyRegistry(firstDefinition, secondDefinition),
            JsonNetConfigDeserializer.Instance);
        var secondValue = secondTableSucceeds ? "second-created" : "throw";

        var reloadResult = database.ReloadIncremental(new[]
        {
            new ConfigDatabase.IncrementalChange(
                firstDefinition.FilePath,
                "[{\"Id\":18,\"Value\":\"first-created\"}]"),
            new ConfigDatabase.IncrementalChange(
                secondDefinition.FilePath,
                $"[{{\"Id\":28,\"Value\":\"{secondValue}\"}}]")
        });

        Assert.Equal(secondTableSucceeds, reloadResult.Succeeded);
        if (secondTableSucceeds)
        {
            Assert.True(database.GetDtoTable<LegacyDto>().TryGet(18, out _));
            Assert.True(database.GetTable<LegacyMo>().TryGet(18, out _));
            Assert.True(database.GetDtoTable<SecondaryDto>().TryGet(28, out _));
            Assert.True(database.GetTable<SecondaryMo>().TryGet(28, out _));
            Assert.Equal(new[] { 18, 28 }, reloadResult.ChangedIds.OrderBy(id => id));
        }
        else
        {
            Assert.Equal(0, database.Version);
            Assert.False(database.TryGetTable<LegacyMo>(out _));
            Assert.False(database.TryGetTable<SecondaryMo>(out _));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConfigDatabase_IncrementalBatchValidationFailureDoesNotCommitPreparedTable(
        bool failWithDeletion)
    {
        var definition = new ConfigTableDefinition(
            "legacy",
            typeof(LegacyDto),
            typeof(LegacyMo));
        var database = new ConfigDatabase(
            new LegacyRegistry(definition),
            JsonNetConfigDeserializer.Instance);
        var initialDto = new LegacyDto { Id = 17, Value = "initial" };
        var loadResult = database.ReloadFromDtoArrays(
            new Dictionary<Type, Array> { [typeof(LegacyDto)] = new[] { initialDto } });
        Assert.True(loadResult.Succeeded, loadResult.Error);
        var version = database.Version;
        var invalidChange = failWithDeletion
            ? new ConfigDatabase.IncrementalChange(definition.FilePath, (string)null)
            : new ConfigDatabase.IncrementalChange("unknown", "[]");

        var reloadResult = database.ReloadIncremental(new[]
        {
            new ConfigDatabase.IncrementalChange(
                definition.FilePath,
                "[{\"Id\":18,\"Value\":\"candidate\"}]"),
            invalidChange,
        });

        Assert.False(reloadResult.Succeeded);
        Assert.Equal(version, database.Version);
        Assert.True(database.GetDtoTable<LegacyDto>().TryGet(initialDto.Id, out _));
        Assert.True(database.GetTable<LegacyMo>().TryGet(initialDto.Id, out _));
        Assert.False(database.GetDtoTable<LegacyDto>().TryGet(18, out _));
        Assert.False(database.GetTable<LegacyMo>().TryGet(18, out _));
    }

    public sealed class LegacyDto
    {
        public int Id;
        public string Value;
    }

    public sealed class LegacyMo
    {
        public LegacyMo(LegacyDto dto)
        {
            if (dto.Value == "throw") throw new InvalidOperationException("Requested conversion failure.");
            Value = dto.Value;
        }

        public string Value { get; }
    }

    public sealed class SecondaryDto
    {
        public int Id;
        public string Value;
    }

    public sealed class SecondaryMo
    {
        public SecondaryMo(SecondaryDto dto)
        {
            if (dto.Value == "throw") throw new InvalidOperationException("Requested conversion failure.");
            Value = dto.Value;
        }

        public string Value { get; }
    }

    private sealed class LegacyRegistry : ConfigTableRegistryBase
    {
        public LegacyRegistry(params ConfigTableDefinition[] definitions)
            : base(definitions)
        {
        }
    }
}
