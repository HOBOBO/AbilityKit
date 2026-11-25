using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class TriggerPlanLoadingBaselineTests
{
    [Fact]
    public void Template_bindings_are_local_to_each_record_and_shared_by_flat_and_tree_conversion()
    {
        var database = new TriggerPlanJsonDatabase();
        database.LoadFromJson("""
            { "FormatVersion": 1, "Triggers": [
                { "TriggerId": 101, "Template": { "Bindings": {
                    "alias": { "Kind": "TemplateParam", "Key": "AMOUNT" },
                    "amount": { "Kind": "Const", "ConstValue": 17 } } },
                  "Actions": [{ "ActionId": 1, "Arity": 1, "Arg0": { "Kind": "TemplateParam", "Key": "Alias" } }],
                  "Predicate": { "Kind": "expr", "Nodes": [{ "Kind": "Function", "FunctionId": 7,
                    "FunctionArity": 1, "Left": { "Kind": "TemplateParam", "Key": "amount" } }] },
                  "ExecutionRoot": { "Kind": "Action", "Action": { "ActionId": 2, "Arity": 1,
                    "Arg0": { "Kind": "TemplateParam", "Key": "alias" } } } },
                { "TriggerId": 102, "Template": { "Bindings": {
                    "amount": { "Kind": "Const", "ConstValue": 39 } } },
                  "Actions": [{ "ActionId": 3, "Args": {
                    "size": { "Kind": "TemplateParam", "Key": "amount" } } }] }
            ] }
            """);

        var first = database.Records[0];
        Assert.Equal(17d, Assert.Single(first.Plan.Actions).Arg0.ConstValue);
        Assert.Equal(17d, Assert.Single(first.Plan.PredicateExpr.Nodes).Left.ConstValue);
        Assert.Equal(17d, Assert.IsType<ActionCallTriggerPlanExecutable>(first.ExecutionRoot).Action.Arg0.ConstValue);
        Assert.Equal(39d, Assert.Single(database.Records[1].Plan.Actions).Args["size"].Ref.ConstValue);
    }

    [Theory]
    [InlineData("missing", "Template parameter is not bound: missing")]
    [InlineData("LOOP", "Cyclic template parameter binding detected")]
    public void Invalid_template_values_preserve_existing_database(string key, string expectedError)
    {
        var database = new TriggerPlanJsonDatabase();
        database.LoadFromJson(Plan(7));
        var previousRecords = database.Records;
        var error = Assert.Throws<InvalidOperationException>(() => database.LoadFromJson($$"""
            { "FormatVersion": 1, "Triggers": [{ "TriggerId": 101,
                "Template": { "Bindings": { "loop": { "Kind": "TemplateParam", "Key": "loop" } } },
                "ExecutionRoot": { "Kind": "Action", "Action": { "ActionId": 2, "Arity": 1,
                    "Arg0": { "Kind": "TemplateParam", "Key": "{{key}}" } } } }] }
            """));

        Assert.Contains(expectedError, error.Message);
        Assert.Same(previousRecords, database.Records);
    }

    [Fact]
    public void Aggregate_compiler_is_owned_by_authoring_without_runtime_dependency()
    {
        var compiler = typeof(TriggerPlanAggregateCompiler);
        var runtime = typeof(TriggerPlanJsonDatabase).Assembly;

        Assert.Equal("AbilityKit.Triggering.Authoring", compiler.Assembly.GetName().Name);
        Assert.Null(runtime.GetType(compiler.FullName!));
        Assert.DoesNotContain(runtime.GetReferencedAssemblies(), assembly => assembly.Name == compiler.Assembly.GetName().Name);
    }

    [Fact]
    public void Required_single_file_rejects_missing_input()
    {
        var database = new TriggerPlanJsonDatabase();
        var error = Assert.Throws<InvalidOperationException>(() =>
            database.Load(new DirectoryInput(), "plans/aggregate.json"));

        Assert.Contains("plans/aggregate.json", error.Message);
    }

    [Fact]
    public void Strict_directory_load_without_matches_returns_empty_without_diagnostics()
    {
        var diagnostics = new List<TriggerPlanJsonDiagnostic>();
        var database = new TriggerPlanDirectoryLoader(new DirectoryInput())
            .LoadDirectory("plans", "**/*.json", Options(true, diagnostics));

        Assert.Empty(database.Records);
        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{ not json")]
    public void Invalid_enumerated_input_fails_in_strict_mode_but_is_reported_and_skipped_in_permissive_mode(string? content)
    {
        const string invalidPath = "plans/invalid.json";
        var loader = new TriggerPlanDirectoryLoader(new DirectoryInput(
            (invalidPath, content), ("plans/valid.json", Plan(4))));
        var strictDiagnostics = new List<TriggerPlanJsonDiagnostic>();
        var permissiveDiagnostics = new List<TriggerPlanJsonDiagnostic>();

        var error = Assert.Throws<InvalidOperationException>(() =>
            loader.LoadDirectory("plans", "**/*.json", Options(true, strictDiagnostics)));
        Assert.Contains(invalidPath, error.Message);
        Assert.Contains(strictDiagnostics, diagnostic =>
            diagnostic.SourceName == invalidPath && diagnostic.Severity == TriggerPlanJsonDiagnosticSeverity.Error);

        var loaded = loader.LoadDirectory("plans", "**/*.json", Options(false, permissiveDiagnostics));
        Assert.Equal(4, Assert.Single(loaded.Records).Plan.Priority);
        Assert.Contains(permissiveDiagnostics, diagnostic =>
            diagnostic.SourceName == invalidPath && diagnostic.Severity == TriggerPlanJsonDiagnosticSeverity.Error);
    }

    [Fact]
    public void Directory_and_compiler_have_distinct_duplicate_id_behavior()
    {
        var entries = new[] { ("plans/z.json", Plan(7)), ("plans/a.json", Plan(3)) };
        var directory = new TriggerPlanDirectoryLoader(new DirectoryInput(
            entries.Select(entry => (entry.Item1, (string?)entry.Item2)).ToArray())).LoadDirectory("plans");

        Assert.Equal(new[] { 7, 3 }, directory.Records.Select(record => record.Plan.Priority));
        Assert.True(directory.TryGetRecordByTriggerId(101, out var indexed));
        Assert.Equal(3, indexed.Plan.Priority);

        var target = new TriggerPlanJsonDatabase();
        target.LoadFromJson(Plan(1));
        target.MergeFrom(directory, replaceExisting: false);
        Assert.Equal(1, Assert.Single(target.Records).Plan.Priority);
        target.MergeFrom(directory, replaceExisting: true);
        Assert.Equal(3, Assert.Single(target.Records).Plan.Priority);

        var error = Assert.Throws<InvalidOperationException>(() => TriggerPlanAggregateCompiler.Compile(
            entries.Select(entry => new TriggerPlanAggregateCompiler.SourceDocument(entry.Item1, entry.Item2))));
        Assert.Contains("Duplicate trigger ID 101", error.Message);
        Assert.Contains("plans/a.json", error.Message);
        Assert.Contains("plans/z.json", error.Message);
    }

    [Fact]
    public void Rule_overlay_replaces_the_plan_and_clears_the_previous_execution_root()
    {
        var aggregate = new TriggerPlanJsonDatabase();
        aggregate.LoadFromJson("""
            {
              "FormatVersion": 1,
              "Triggers": [
                { "TriggerId": 101, "Priority": 1, "ExecutionRoot": { "Kind": "Succeed" } },
                { "TriggerId": 102, "Priority": 2 }
              ]
            }
            """);
        Assert.True(aggregate.TryGetExecutionRootByTriggerId(101, out _));

        var rules = new TriggerPlanDirectoryLoader(new DirectoryInput(("plans/rule.json", Plan(7))))
            .LoadDirectory("plans");
        aggregate.MergeFrom(rules, replaceExisting: true);

        Assert.Equal(new[] { 101, 102 }, aggregate.Records.Select(record => record.TriggerId));
        Assert.True(aggregate.TryGetRecordByTriggerId(101, out var record));
        Assert.Equal(7, record.Plan.Priority);
        Assert.Null(record.ExecutionRoot);
        Assert.True(aggregate.TryGetPlanByTriggerId(101, out var plan));
        Assert.Equal(7, plan.Priority);
        Assert.False(aggregate.TryGetExecutionRootByTriggerId(101, out _));
        Assert.True(aggregate.TryGetPlanByTriggerId(102, out var unaffected));
        Assert.Equal(2, unaffected.Priority);
    }

    [Fact]
    public void Loading_one_document_retains_duplicate_rows_and_legacy_root_lookup()
    {
        var database = new TriggerPlanJsonDatabase();
        database.LoadFromJson("""
            {
              "FormatVersion": 1,
              "Triggers": [
                { "TriggerId": 101, "Priority": 1, "ExecutionRoot": { "Kind": "Succeed" } },
                { "TriggerId": 101, "Priority": 2 }
              ]
            }
            """);

        Assert.Equal(new[] { 1, 2 }, database.Records.Select(record => record.Plan.Priority));
        Assert.True(database.TryGetRecordByTriggerId(101, out var indexed));
        Assert.Equal(2, indexed.Plan.Priority);
        Assert.Null(indexed.ExecutionRoot);
        Assert.True(database.TryGetPlanByTriggerId(101, out var plan));
        Assert.Equal(2, plan.Priority);
        Assert.True(database.TryGetExecutionRootByTriggerId(101, out var root));
        Assert.Same(database.Records[0].ExecutionRoot, root);
    }

    [Fact]
    public void Failed_materialization_does_not_replace_the_previous_database()
    {
        var database = new TriggerPlanJsonDatabase();
        database.LoadFromJson("""
            {
              "FormatVersion": 1,
              "Strings": { "1": "existing" },
              "Triggers": [{ "TriggerId": 101, "Priority": 7, "ExecutionRoot": { "Kind": "Succeed" } }]
            }
            """);
        var previousRecords = database.Records;
        var previousBlackboards = database.Blackboards;
        var factory = new FailingCueFactory();
        database.CueFactory = factory;

        var error = Assert.Throws<InvalidOperationException>(() => database.LoadFromJson("""
            {
              "FormatVersion": 1,
              "Strings": { "1": "new" },
              "Triggers": [{ "TriggerId": 201 }, { "TriggerId": 202 }]
            }
            """));

        Assert.Same(factory.Error, error);
        Assert.Equal(2, factory.Calls);
        Assert.Same(previousRecords, database.Records);
        Assert.Same(previousBlackboards, database.Blackboards);
        Assert.True(database.TryGetRecordByTriggerId(101, out var record));
        Assert.Equal(7, record.Plan.Priority);
        Assert.True(database.TryGetExecutionRootByTriggerId(101, out var root));
        Assert.Same(record.ExecutionRoot, root);
        Assert.False(database.TryGetRecordByTriggerId(201, out _));
        Assert.True(database.TryGetString(1, out var text));
        Assert.Equal("existing", text);
    }

    private sealed class FailingCueFactory : TriggerPlanJsonDatabase.ICueFactory
    {
        internal readonly InvalidOperationException Error = new("fixture cue conversion failure");
        internal int Calls { get; private set; }

        public ITriggerCue Create(in TriggerCueDescriptor descriptor) => ++Calls == 2
            ? throw Error
            : NullTriggerCue.Instance;
    }

    private static string Plan(int priority) =>
        $$"""{"FormatVersion":1,"Triggers":[{"TriggerId":101,"Priority":{{priority}}}]}""";

    private static TriggerPlanDirectoryLoadOptions Options(bool strict, IList<TriggerPlanJsonDiagnostic> diagnostics) =>
        new() { ThrowOnFileParseError = strict, Diagnostics = diagnostics };

    private sealed class DirectoryInput(params (string Path, string? Content)[] entries) : IFileSystemTextLoader
    {
        public IEnumerable<string> GetFiles(string directory, string pattern) => entries.Select(entry => entry.Path);

        public bool TryLoad(string id, out string text)
        {
            text = entries.FirstOrDefault(entry => entry.Path == id).Content!;
            return text != null;
        }
    }
}
