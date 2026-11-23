using AbilityKit.Ability.Config;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Console.Bootstrap;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Demo.Moba.Systems.Bootstrap.Flow.Stages;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Runtime.Plan.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.Demo.Moba.Tests.Triggering;

public sealed class MobaTriggerPlanLoadingBaselineTests(ITestOutputHelper output)
{
    private const string AggregatePath = "ability/ability_trigger_plans.json";
    private const string RuleDirectory = "ability/rules";
    private const string RulePath = RuleDirectory + "/nested/override.json";

    [Fact]
    public void Required_aggregate_is_missing_even_when_rules_exist()
    {
        using var input = new FixtureDirectory();
        input.Write(RulePath, Plan(7));

        var factoryError = Assert.Throws<InvalidOperationException>(() =>
            LoadMobaFactory(new ResourceTreeInput(input.Root)));
        Assert.Contains(AggregatePath, factoryError.Message);

        input.Write("luban/" + RulePath, Plan(7));
        var consoleError = Assert.ThrowsAny<Exception>(() => LoadConsole(input.Root));
        Assert.Contains("luban/" + AggregatePath, consoleError.ToString());
    }

    [Theory]
    [InlineData("unsupported")]
    [InlineData("missing")]
    [InlineData("empty")]
    public void Default_moba_factory_keeps_aggregate_when_enumeration_has_no_results(string mode)
    {
        using var input = new FixtureDirectory();
        input.Write(AggregatePath, Plan(1));
        if (mode == "empty") input.CreateDirectory(RuleDirectory);
        if (mode == "unsupported") input.Write(RulePath, Plan(7));

        var loader = mode == "unsupported" ? new FileInput(input.Root) : new ResourceTreeInput(input.Root);
        var record = Assert.Single(LoadMobaFactory(loader).Records);

        Assert.Equal(101, record.TriggerId);
        Assert.Equal(1, record.Plan.Priority);
    }

    [Fact]
    public void Console_uses_explicit_aggregate_path_and_keeps_its_rule_directory()
    {
        using var input = new FixtureDirectory();
        const string customPath = "custom/trigger_plans.json";
        input.Write(customPath, """
            {"FormatVersion":1,"Triggers":[{"TriggerId":101,"Priority":1},{"TriggerId":202,"Priority":2}]}
            """);
        input.Write("luban/" + RulePath, Plan(7));

        var records = LoadConsole(input.Root, customPath).Records;

        Assert.Equal(2, records.Count);
        Assert.Equal(7, Assert.Single(records, record => record.TriggerId == 101).Plan.Priority);
        Assert.Equal(2, Assert.Single(records, record => record.TriggerId == 202).Plan.Priority);
    }

    [Fact]
    public void Same_invalid_rule_aborts_default_moba_factory_but_console_keeps_valid_override()
    {
        using var input = new FixtureDirectory();
        const string invalidPath = RuleDirectory + "/broken.json";
        foreach (var prefix in new[] { "", "luban/" })
        {
            input.Write(prefix + AggregatePath, Plan(1));
            input.Write(prefix + RulePath, Plan(7));
            input.Write(prefix + invalidPath, "{ not json");
        }

        var strictError = Assert.Throws<InvalidOperationException>(() =>
            LoadMobaFactory(new ResourceTreeInput(input.Root)));
        Assert.Contains(invalidPath, strictError.Message);

        var console = LoadConsole(input.Root);
        Assert.Equal(7, Assert.Single(console.Records).Plan.Priority);
    }

    [Fact]
    public void Override_is_applied_before_integrity_validation_on_both_host_paths()
    {
        using var input = new FixtureDirectory();
        foreach (var prefix in new[] { "", "luban/" })
        {
            input.Write(prefix + AggregatePath, Plan(1));
            input.Write(prefix + RulePath, Plan(7, "load-baseline.unregistered"));
        }

        foreach (var database in new[] { LoadMobaFactory(new ResourceTreeInput(input.Root)), LoadConsole(input.Root) })
        {
            var record = Assert.Single(database.Records);
            Assert.Equal(7, record.Plan.Priority);
            var diagnostics = ValidateIntegrity(database);
            Assert.Equal(1, diagnostics.ErrorCount);
            Assert.Equal(1, diagnostics.WarningCount);
            var error = Assert.Single(diagnostics.Entries, entry => entry.Code == "moba.trigger.plan.event_unregistered");
            Assert.Equal(101L, error.BusinessNumericId);
            Assert.True(error.BlocksStartup);
            Assert.Contains(diagnostics.Entries, entry => entry.Code == "moba.trigger.plan.no_actions" && !entry.BlocksStartup);
        }
    }

    [Fact]
    public void Published_resource_tree_and_console_use_equal_inputs_and_effective_records()
    {
        var root = FindRepositoryRoot();
        var unityResources = Path.Combine(root, "Unity", "Packages", "com.abilitykit.demo.moba.view.runtime", "Resources");
        var consoleConfigs = Path.Combine(root, "src", "AbilityKit.Demo.Moba.Console", "Configs");
        var resourceInput = new ResourceTreeInput(unityResources);
        var consoleInput = new ResourceTreeInput(Path.Combine(consoleConfigs, "luban"));
        var resourcePaths = SourcePaths(resourceInput);
        var consolePaths = SourcePaths(consoleInput);
        Assert.Equal(resourcePaths, consolePaths);
        Assert.NotEmpty(resourcePaths.Skip(1));

        var sourcesById = new Dictionary<int, List<string>>();
        foreach (var path in resourcePaths)
        {
            Assert.True(resourceInput.TryLoadText(path, out var resourceJson), path);
            Assert.True(consoleInput.TryLoadText(path, out var consoleJson), path);
            Assert.True(JToken.DeepEquals(JToken.Parse(resourceJson), JToken.Parse(consoleJson)), $"Published source differs: {path}");

            var sourceDatabase = new TriggerPlanJsonDatabase();
            sourceDatabase.LoadFromJson(resourceJson, path);
            foreach (var record in sourceDatabase.Records)
            {
                if (!sourcesById.TryGetValue(record.TriggerId, out var sources))
                    sourcesById.Add(record.TriggerId, sources = new List<string>());
                sources.Add(path);
            }
        }

        var resourceDb = LoadMobaFactory(resourceInput);
        var consoleDb = LoadConsole(consoleConfigs);
        var expectedIds = sourcesById.Keys.OrderBy(id => id).ToArray();
        Assert.Equal(expectedIds, resourceDb.Records.Select(record => record.TriggerId).OrderBy(id => id));
        Assert.Equal(expectedIds, consoleDb.Records.Select(record => record.TriggerId).OrderBy(id => id));
        foreach (var id in expectedIds)
        {
            Assert.True(resourceDb.TryGetRecordByTriggerId(id, out var resourceRecord));
            Assert.True(consoleDb.TryGetRecordByTriggerId(id, out var consoleRecord));
            var origins = string.Join(", ", sourcesById[id]);
            Assert.True(Describe(resourceRecord) == Describe(consoleRecord),
                $"Trigger {id} differs ({origins}): resources={Describe(resourceRecord)}, console={Describe(consoleRecord)}");
            output.WriteLine("Trigger {0}: sources=[{1}], effective={2}", id, origins, Describe(resourceRecord));
        }
    }

    private static string Describe(TriggerPlanJsonDatabase.Record record) =>
        $"{record.Scope}:{record.EventName}:{record.EventId}:phase={record.Plan.Phase}:priority={record.Plan.Priority}:" +
        $"root={record.ExecutionRoot?.GetType().Name}:actions={string.Join(",", record.Plan.Actions.Select(action => action.Id.Value))}";

    private static string Plan(int priority, string eventName = "") =>
        $$"""{"FormatVersion":1,"Triggers":[{"TriggerId":101,"Priority":{{priority}},"EventName":"{{eventName}}"}]}""";

    private static MobaRuntimeValidationReport ValidateIntegrity(TriggerPlanJsonDatabase database)
    {
        var report = new MobaRuntimeValidationReport();
        MobaTriggerPlanIntegrityValidator.ValidateDatabase(database, new MobaEventSubscriptionRegistry(), new PayloadAccessorRegistry(), report);
        return report;
    }

    private static TriggerPlanJsonDatabase LoadMobaFactory(ITextAssetLoader loader)
    {
        var builder = new WorldContainerBuilder();
        var blackboards = new DictionaryBlackboardResolver();
        builder.RegisterInstance(loader);
        builder.RegisterInstance(MobaTriggerPlanLoadProfile.Default);
        builder.RegisterInstance<IBlackboardResolver>(blackboards);
        builder.RegisterInstance<IOwnerBlackboardStore>(new OwnerBlackboardStore(blackboards));
        RegisterCueService(builder);
        using var scope = builder.Build().CreateScope();
        return new MobaTriggerPlanDatabaseFactory().Create(scope);
    }

    private static TriggerPlanJsonDatabase LoadConsole(string root, string aggregatePath = "luban/" + AggregatePath)
    {
        var builder = new WorldContainerBuilder();
        new ConsoleConfigModule(triggerPlansPath: aggregatePath).Configure(builder);
        builder.Register<ITextAssetLoader>(WorldLifetime.Singleton, _ => new ConsoleTextAssetLoader(root));
        RegisterCueService(builder);
        using var scope = builder.Build().CreateScope();
        return scope.Resolve<TriggerPlanJsonDatabase>();
    }

    private static void RegisterCueService(WorldContainerBuilder builder) =>
        builder.RegisterInstance(new MobaPresentationCueSnapshotService(new MobaLogicWorldRunGateService()));

    private static string[] SourcePaths(ResourceTreeInput input) =>
        new[] { AggregatePath }.Concat(input.GetTextAssetPaths(RuleDirectory, "**/*.json").OrderBy(path => path, StringComparer.Ordinal)).ToArray();

    private static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Unity", "Packages"))) return dir.FullName;
        }
        throw new DirectoryNotFoundException("Cannot locate AbilityKit repository root for published trigger loading test.");
    }

    private class FileInput(string root) : ITextAssetLoader
    {
        protected string Root { get; } = root;

        public bool TryLoadText(string path, out string text)
        {
            var file = Path.Combine(Root, path);
            text = File.Exists(file) ? File.ReadAllText(file) : null!;
            return !string.IsNullOrEmpty(text);
        }

        public bool TryLoadBytes(string path, out byte[] bytes) => throw new NotSupportedException();
    }

    private sealed class ResourceTreeInput(string root) : FileInput(root), ITextAssetDirectoryLoader
    {
        public IEnumerable<string> GetTextAssetPaths(string directory, string pattern)
        {
            var fullPath = Path.Combine(Root, directory);
            var searchPattern = pattern.Replace("**/", "");
            return Directory.Exists(fullPath)
                ? Directory.EnumerateFiles(fullPath, searchPattern, SearchOption.AllDirectories)
                    .Select(file => Path.GetRelativePath(Root, file).Replace('\\', '/'))
                : Array.Empty<string>();
        }
    }

    private sealed class FixtureDirectory : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("abilitykit-trigger-load-").FullName;

        public void CreateDirectory(string relativePath) => Directory.CreateDirectory(Path.Combine(Root, relativePath));

        public void Write(string relativePath, string content)
        {
            CreateDirectory(Path.GetDirectoryName(relativePath)!);
            File.WriteAllText(Path.Combine(Root, relativePath), content);
        }

        public void Dispose()
        {
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)) File.Delete(file);
            foreach (var directory in Directory.GetDirectories(Root, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length))
                Directory.Delete(directory);
            Directory.Delete(Root);
        }
    }
}
