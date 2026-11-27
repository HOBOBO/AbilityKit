using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Runtime.Diagnostics;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Triggering.Validation;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class TriggerPlanLoadingBaselineTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Explicit_runtime_boundary_validates_the_loaded_execution_root(bool unsupportedSchedule)
    {
        var executionRoot = unsupportedSchedule
            ? """{ "Kind": "Action", "Action": { "ActionId": 12, "ScheduleMode": "Timeline", "MaxExecutions": -1 } }"""
            : """{ "Kind": "Sequence", "Children": [] }""";
        const string source = "local-candidate.json";
        var candidate = new TriggerPlanJsonDatabase();
        candidate.LoadFromJson($$"""
            { "FormatVersion": 1, "Triggers": [
                { "TriggerId": 101, "EventId": 11, "ExecutionRoot": {{executionRoot}} }
            ] }
            """, source);
        var record = Assert.Single(candidate.Records);
        Assert.NotNull(record.ExecutionRoot);
        var entries = new[]
        {
            new TriggerPlanEntry<object>(new EventKey<object>(record.EventId), record.Plan,
                id: "101", source: source, lineNumber: 1, executionRoot: record.ExecutionRoot)
        };

        var result = TriggerPlanValidation.ValidateRuntimeBoundary(entries);

        Assert.Equal(!unsupportedSchedule, result.IsValid);
        if (unsupportedSchedule)
        {
            Assert.Contains(result.Errors, issue => issue.Code == ValidationErrorCodes.UNSUPPORTED_ACTION_SCHEDULE &&
                issue.Path == $"{source}:1.executionRoot.action.scheduleMode");
            Assert.Throws<TriggerValidationException>(() => TriggerPlanValidation.ThrowIfInvalidRuntimeBoundary(entries, source));
        }
        else
        {
            Assert.Contains(result.Warnings, issue => issue.Code == ValidationErrorCodes.EMPTY_EXECUTION_NODE &&
                issue.Path == $"{source}:1.executionRoot.children");
            TriggerPlanValidation.ThrowIfInvalidRuntimeBoundary(entries, source);
        }
        Assert.Same(record.ExecutionRoot, Assert.Single(candidate.Records).ExecutionRoot);
    }

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
        AssertLoadDiagnostics(strictDiagnostics);

        var loaded = loader.LoadDirectory("plans", "**/*.json", Options(false, permissiveDiagnostics));
        Assert.Equal(4, Assert.Single(loaded.Records).Plan.Priority);
        Assert.Contains(permissiveDiagnostics, diagnostic =>
            diagnostic.SourceName == invalidPath && diagnostic.Severity == TriggerPlanJsonDiagnosticSeverity.Error);
        AssertLoadDiagnostics(permissiveDiagnostics);
    }

    [Fact]
    public void Json_diagnostic_adapter_preserves_order_and_counts_unknown_severity_as_info()
    {
        var diagnostics = new[]
        {
            new TriggerPlanJsonDiagnostic(TriggerPlanJsonDiagnosticSeverity.Warning, "fallback", "fixture.json", "$.Strings"),
            new TriggerPlanJsonDiagnostic(TriggerPlanJsonDiagnosticSeverity.Info, "loaded", "fixture.json", "$"),
            new TriggerPlanJsonDiagnostic((TriggerPlanJsonDiagnosticSeverity)99, "unknown", "fixture.json", "$.Triggers"),
            new TriggerPlanJsonDiagnostic(TriggerPlanJsonDiagnosticSeverity.Error, "invalid", "fixture.json", "$.Triggers[0]")
        };
        var collector = AssertLoadDiagnostics(diagnostics);

        Assert.Equal(1, collector.Snapshot.LoadErrors);
        Assert.Equal(1, collector.Snapshot.LoadWarnings);
        Assert.Equal(2, collector.Snapshot.LoadInfos);
        Assert.Equal("99", collector.Records[2].Code);
        Assert.Equal(1, collector.Records[2].InfoCount);

        TriggerPlanJsonDiagnosticAdapter.RecordTo(collector, Array.Empty<TriggerPlanJsonDiagnostic>());
        TriggerPlanJsonDiagnosticAdapter.RecordTo(collector, null);
        Assert.Equal(diagnostics.Length, collector.Snapshot.TotalRecords);
    }

    private static TriggeringDiagnosticCollector AssertLoadDiagnostics(IReadOnlyList<TriggerPlanJsonDiagnostic> diagnostics)
    {
        var collector = new TriggeringDiagnosticCollector();
        TriggerPlanJsonDiagnosticAdapter.RecordTo(collector, diagnostics);

        Assert.Equal(diagnostics.Count, collector.Snapshot.TotalRecords);
        Assert.Equal(diagnostics.Count(diagnostic => diagnostic.Severity == TriggerPlanJsonDiagnosticSeverity.Error),
            collector.Snapshot.LoadErrors);
        for (var index = 0; index < diagnostics.Count; index++)
        {
            var input = diagnostics[index];
            var record = collector.Records[index];
            Assert.Equal(TriggeringDiagnosticEventKind.PlanLoad, record.Kind);
            Assert.Equal(input.Severity.ToString(), record.Code);
            Assert.Equal(input.SourceName, record.Source);
            Assert.Equal(input.Path, record.Path);
            Assert.Equal(input.Message, record.Message);
        }
        return collector;
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
                { "TriggerId": 101, "Priority": 1, "Scope": 1, "ExecutionRoot": { "Kind": "Succeed" } },
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
        Assert.Equal(TriggerPlanScope.Global, record.Scope);
        Assert.Null(record.ExecutionRoot);
        Assert.True(aggregate.TryGetPlanByTriggerId(101, out var plan));
        Assert.Equal(7, plan.Priority);
        Assert.False(aggregate.TryGetExecutionRootByTriggerId(101, out _));
        Assert.True(aggregate.TryGetPlanByTriggerId(102, out var unaffected));
        Assert.Equal(2, unaffected.Priority);
    }

    [Fact]
    public void Record_lookup_uses_the_last_row_without_inheriting_an_earlier_execution_root()
    {
        var database = new TriggerPlanJsonDatabase();
        database.LoadFromJson("""
            {
              "FormatVersion": 1,
              "Triggers": [
                { "TriggerId": 101, "Priority": 1, "EventId": 11, "Scope": 0,
                  "ExecutionRoot": { "Kind": "Succeed" } },
                { "TriggerId": 101, "Priority": 2, "EventId": 12, "Scope": 1 }
              ]
            }
            """);

        Assert.Equal(new[] { 1, 2 }, database.Records.Select(record => record.Plan.Priority));
        Assert.True(database.TryGetRecordByTriggerId(101, out var indexed));
        Assert.Equal(2, indexed.Plan.Priority);
        Assert.Equal(12, indexed.EventId);
        Assert.Equal(TriggerPlanScope.OwnerBound, indexed.Scope);
        Assert.Null(indexed.ExecutionRoot);
        Assert.True(database.TryGetPlanByTriggerId(101, out var plan));
        Assert.Equal(2, plan.Priority);
        Assert.True(database.TryGetExecutionRootByTriggerId(101, out var root));
        Assert.Same(database.Records[0].ExecutionRoot, root);
    }

    [Theory]
    [InlineData(false, 2, 12, TriggerPlanScope.OwnerBound)]
    [InlineData(true, 4, 14, TriggerPlanScope.Global)]
    public void Merge_uses_effective_source_and_target_records(
        bool replaceExisting, int expectedPriority, int expectedEvent, TriggerPlanScope expectedScope)
    {
        var target = new TriggerPlanJsonDatabase();
        target.LoadFromJson("""
            { "FormatVersion": 1, "Triggers": [
                { "TriggerId": 101, "Priority": 1, "ExecutionRoot": { "Kind": "Succeed" } },
                { "TriggerId": 102, "Priority": 1, "ExecutionRoot": { "Kind": "Succeed" } },
                { "TriggerId": 101, "Priority": 2, "EventId": 12, "Scope": 1 },
                { "TriggerId": 102, "Priority": 2 }
            ] }
            """);
        var source = new TriggerPlanJsonDatabase();
        source.LoadFromJson("""
            { "FormatVersion": 1, "Triggers": [
                { "TriggerId": 103, "Priority": 3, "ExecutionRoot": { "Kind": "Succeed" } },
                { "TriggerId": 104, "Priority": 3, "ExecutionRoot": { "Kind": "Succeed" } },
                { "TriggerId": 101, "Priority": 3, "ExecutionRoot": { "Kind": "Succeed" } },
                { "TriggerId": 103, "Priority": 4 },
                { "TriggerId": 101, "Priority": 4, "EventId": 14, "Scope": 0 }
            ] }
            """);
        var sourceRevision = source.ContentRevision;
        var sourceVersion = source.MutationVersion;

        target.MergeFrom(source, replaceExisting);

        Assert.Equal(new[] { 101, 102, 103, 104 }, target.Records.Select(record => record.TriggerId));
        Assert.Equal(new[] { expectedPriority, 2, 4, 3 }, target.Records.Select(record => record.Plan.Priority));
        foreach (var record in target.Records)
        {
            Assert.True(target.TryGetRecordByTriggerId(record.TriggerId, out var indexed));
            Assert.Equal(record, indexed);
            Assert.Equal(record.ExecutionRoot != null,
                target.TryGetExecutionRootByTriggerId(record.TriggerId, out var root));
            Assert.Same(record.ExecutionRoot, root);
        }
        Assert.Equal(expectedEvent, target.Records[0].EventId);
        Assert.Equal(expectedScope, target.Records[0].Scope);
        Assert.NotNull(target.Records[3].ExecutionRoot);
        Assert.Equal(new[] { 103, 104, 101, 103, 101 }, source.Records.Select(record => record.TriggerId));
        Assert.Equal(sourceRevision, source.ContentRevision);
        Assert.Equal(sourceVersion, source.MutationVersion);
        Assert.True(source.TryGetExecutionRootByTriggerId(103, out _));
    }

    [Fact]
    public void Successful_keyed_write_normalizes_existing_duplicate_rows()
    {
        var target = new TriggerPlanJsonDatabase();
        target.LoadFromJson("""
            { "FormatVersion": 1, "Triggers": [
                { "TriggerId": 101, "Priority": 1, "ExecutionRoot": { "Kind": "Succeed" } },
                { "TriggerId": 101, "Priority": 2 }
            ] }
            """);
        var plan = new TriggerPlan<object>(phase: 0, priority: 3, triggerId: 101);
        var record = new TriggerPlanJsonDatabase.Record(101, string.Empty, 0, TriggerPlanScope.Global, in plan);

        target.AddRecord(in record, replaceExisting: false);
        Assert.Equal(new[] { 1, 2 }, target.Records.Select(row => row.Plan.Priority));

        target.AddRecord(in record, replaceExisting: true);
        Assert.Equal(record, Assert.Single(target.Records));
        Assert.True(target.TryGetRecordByTriggerId(101, out var indexed));
        Assert.Equal(record, indexed);
        Assert.False(target.TryGetExecutionRootByTriggerId(101, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failed_load_does_not_publish_a_partial_database(bool failDuringConversion)
    {
        var database = new TriggerPlanJsonDatabase();
        database.LoadFromJson("""
            {
              "FormatVersion": 1,
              "Strings": { "1": "existing" },
              "Triggers": [{ "TriggerId": 101, "Priority": 7, "EventId": 31, "Scope": 1,
                "ExecutionRoot": { "Kind": "Succeed" } }]
            }
            """);
        var previousRecords = database.Records;
        var previousRoot = Assert.IsType<SucceedTriggerPlanExecutable>(Assert.Single(previousRecords).ExecutionRoot);
        var previousBlackboards = database.Blackboards;
        var previousRevision = database.ContentRevision;
        var previousMutationVersion = database.MutationVersion;
        var factory = new FailingCueFactory();
        database.CueFactory = factory;

        var input = failDuringConversion ? """
            {
              "FormatVersion": 1,
              "Strings": { "1": "new" },
              "Triggers": [{ "TriggerId": 101, "Priority": 99 }, { "TriggerId": 201 }]
            }
            """ : "{ not json";
        var error = Assert.Throws<InvalidOperationException>(() => database.LoadFromJson(input, "replacement.json"));

        if (failDuringConversion)
        {
            Assert.Same(factory.Error, error);
            Assert.Equal(2, factory.Calls);
        }
        else
        {
            Assert.Contains("replacement.json", error.Message);
            Assert.Equal(0, factory.Calls);
        }
        Assert.Same(previousRecords, database.Records);
        Assert.Same(previousBlackboards, database.Blackboards);
        Assert.Equal(previousRevision, database.ContentRevision);
        Assert.Equal(previousMutationVersion, database.MutationVersion);
        Assert.True(database.TryGetRecordByTriggerId(101, out var record));
        Assert.Equal(7, record.Plan.Priority);
        Assert.Equal(31, record.EventId);
        Assert.Equal(TriggerPlanScope.OwnerBound, record.Scope);
        Assert.Same(previousRoot, record.ExecutionRoot);
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
