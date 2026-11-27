using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Validation;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class TriggerPlanValidationProfilesTests
{
    [Fact]
    public void Runtime_boundary_does_not_apply_the_development_flat_action_limit()
    {
        var entry = CreateEntry(actionCount: 101);
        var database = new TriggerPlanDatabase<object>(new[] { entry });
        ValidationContext<object> context = default;

        Assert.True(TriggerPlanValidation.ValidateRuntimeBoundary(database.Plans, in context).IsValid);
        var development = CompositeTriggerValidator<object>.CreateForDevelopment();
        Assert.Contains(development.Validate(in database, in context).Errors,
            issue => issue.Code == ValidationErrorCodes.EXCEEDS_ACTION_COUNT && issue.Path == "fixture:1.actions");
        var withinLimit = new TriggerPlanDatabase<object>(new[] { CreateEntry(actionCount: 100) });
        Assert.True(development.Validate(in withinLimit, in context).IsValid);
    }

    [Fact]
    public void Composed_flat_action_limit_uses_explicit_validator_configuration()
    {
        var database = new TriggerPlanDatabase<object>(new[] { CreateEntry(actionCount: 3) });
        ValidationContext<object> context = default;
        var review = new CompositeTriggerValidator<object>().Add(CreateLimits(maxActionCount: 2));

        var result = review.Validate(in database, in context);

        Assert.Contains(result.Errors, issue => issue.Code == ValidationErrorCodes.EXCEEDS_ACTION_COUNT);
    }

    [Fact]
    public void Flat_action_limit_does_not_count_execution_tree_actions()
    {
        var children = Enumerable.Range(0, 11)
            .Select(_ => (ITriggerPlanExecutable)new ActionCallTriggerPlanExecutable(new ActionCallPlan(new ActionId(12))))
            .ToArray();
        var root = new SequenceTriggerPlanExecutable(children);
        var database = new TriggerPlanDatabase<object>(new[] { CreateEntry(actionCount: 0, executionRoot: root) });
        ValidationContext<object> context = default;

        var result = CreateLimits(maxActionCount: 2).Validate(in database, in context);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Flat_action_limit_is_per_plan_not_per_database()
    {
        var database = new TriggerPlanDatabase<object>(new[] { CreateEntry(2), CreateEntry(2) });
        ValidationContext<object> context = default;

        Assert.True(CreateLimits(maxActionCount: 2).Validate(in database, in context).IsValid);
    }

    [Theory]
    [InlineData(null, null, false, false)]
    [InlineData("", "", false, false)]
    [InlineData("21", "12", false, false)]
    [InlineData("22", "", true, false)]
    [InlineData("", "13", false, true)]
    [InlineData("22", "13", true, true)]
    public void Runtime_boundary_checks_supplied_catalogs_in_both_overloads(
        string? functionId, string? actionId, bool missingFunction, bool missingAction)
    {
        var functions = CreateCatalog(functionId);
        var actions = CreateCatalog(actionId);
        var calls = new[] { new ActionCallPlan(new ActionId(12)) };
        var predicate = new FunctionId(21);
        var typedPlan = TriggerPlanFactory.When<int>(0, 0, predicate, actions: calls);
        var batchPlan = TriggerPlanFactory.When<object>(0, 0, predicate, actions: calls);
        var typedContext = new ValidationContext<int>(functions, actions);
        var batchContext = new ValidationContext<object>(functions, actions);
        var entries = new[]
        {
            new TriggerPlanEntry<object>(new EventKey<object>(11), batchPlan, "rule", "fixture", 1)
        };
        var expected = new List<(string Code, string Path)>();
        if (missingFunction)
            expected.Add((ValidationErrorCodes.FUNCTION_NOT_FOUND, "fixture:1.predicate"));
        if (missingAction)
            expected.Add((ValidationErrorCodes.ACTION_NOT_FOUND, "fixture:1.actions[0]"));

        var key = new EventKey<int>(11);
        var typed = TriggerPlanValidation.ValidateRuntimeBoundary(key, in typedPlan, "rule", "fixture", 1, in typedContext);
        var batch = TriggerPlanValidation.ValidateRuntimeBoundary(entries, in batchContext);

        Assert.Equal(expected, typed.Errors.Select(issue => (issue.Code, issue.Path)));
        Assert.Equal(expected, batch.Errors.Select(issue => (issue.Code, issue.Path)));
        Assert.True(TriggerPlanValidation.ValidateRuntimeBoundary(key, in typedPlan).IsValid);
        Assert.True(TriggerPlanValidation.ValidateRuntimeBoundary(entries).IsValid);
    }

    private static HashSet<string>? CreateCatalog(string? id) =>
        id == null ? null : id.Length == 0 ? new HashSet<string>() : new HashSet<string> { id };

    private static FlatTriggerPlanLimitsValidator<object> CreateLimits(int maxActionCount) =>
        new(maxNestingDepth: 50, maxNodeCount: 500, maxComplexity: 200, maxActionCount: maxActionCount);

    private static TriggerPlanEntry<object> CreateEntry(int actionCount, ITriggerPlanExecutable? executionRoot = null)
    {
        var actions = Enumerable.Repeat(new ActionCallPlan(new ActionId(12)), actionCount).ToArray();
        var plan = TriggerPlanFactory.Create<object>(actions: actions);
        return new TriggerPlanEntry<object>(new EventKey<object>(11), plan, id: "rule", source: "fixture",
            lineNumber: 1, executionRoot: executionRoot!);
    }
}
