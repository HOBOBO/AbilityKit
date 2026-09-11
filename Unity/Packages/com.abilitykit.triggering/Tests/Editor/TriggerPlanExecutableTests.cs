using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Validation;
using NUnit.Framework;

namespace AbilityKit.Triggering.Tests
{
    public sealed class TriggerPlanExecutableTests
    {
        [Test]
        public void Sequence_Repeat_And_Until_ExecuteWithExpectedCounts()
        {
            var first = new CountingExecutable();
            var repeated = new CountingExecutable();
            var untilChild = new CountingExecutable();
            var untilCondition = new CountingCondition(false, false, true);
            var root = TriggerPlanExecutableDsl.Sequence(
                first,
                TriggerPlanExecutableDsl.Repeat(repeated, 3),
                TriggerPlanExecutableDsl.Until(untilChild, untilCondition, 5));

            var result = root.Execute<object>(null, default);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.ExecutedCount, Is.EqualTo(6));
            Assert.That(first.Count, Is.EqualTo(1));
            Assert.That(repeated.Count, Is.EqualTo(3));
            Assert.That(untilChild.Count, Is.EqualTo(2));
            Assert.That(untilCondition.Count, Is.EqualTo(3));
        }

        [Test]
        public void PlannedTrigger_WithExecutionRoot_ExecutesRootWhenLegacyActionsAreEmpty()
        {
            var root = new CountingExecutable();
            var plan = new TriggerPlan<object>(phase: 0, priority: 0, triggerId: 1001);
            var trigger = new PlannedTrigger<object, object>(
                in plan,
                (in object value, in ExecCtx<object> ctx) => root.Execute(value, in ctx));
            object args = new object();
            ExecCtx<object> ctx = default;

            Assert.That(trigger.Evaluate(in args, in ctx), Is.True);
            trigger.Execute(in args, in ctx);

            Assert.That(root.Count, Is.EqualTo(1));
        }

        [Test]
        public void PlannedTrigger_WithExecutionRoot_PreservesOnceExecutionControl()
        {
            var root = new CountingExecutable();
            var executionControl = new TriggerExecutionControlPlan(ETriggerExecutionMode.Once, maxExecutions: 1);
            var plan = new TriggerPlan<object>(
                phase: 0,
                priority: 0,
                triggerId: 1002,
                executionControl: in executionControl);
            var trigger = new PlannedTrigger<object, object>(
                in plan,
                (in object value, in ExecCtx<object> ctx) => root.Execute(value, in ctx));
            object args = new object();
            ExecCtx<object> ctx = default;

            trigger.Execute(in args, in ctx);
            trigger.Execute(in args, in ctx);

            Assert.That(root.Count, Is.EqualTo(1));
        }

        [Test]
        public void Validator_RejectsTimelineActionInsideExecutableTree()
        {
            var actionId = new ActionId(StableStringId.Get("test:trigger_plan_executable_validator:timeline"));
            var timelineAction = new ActionCallPlan(
                actionId,
                0,
                default,
                default,
                null,
                EActionScheduleMode.Timeline,
                100f,
                -1,
                true,
                EActionExecutionPolicy.Immediate);
            var root = TriggerPlanExecutableDsl.Sequence(TriggerPlanExecutableDsl.Action(timelineAction));
            var validator = new TriggerPlanExecutableValidator();

            var result = validator.Validate(root);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Matches<ValidationIssue>(issue => issue.Code == ValidationErrorCodes.UNSUPPORTED_ACTION_SCHEDULE));
        }

        [Test]
        public void Validator_RejectsUntilWithoutCondition()
        {
            var root = TriggerPlanExecutableDsl.Until(new CountingExecutable(), null, 2);
            var validator = new TriggerPlanExecutableValidator();

            var result = validator.Validate(root);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Matches<ValidationIssue>(issue => issue.Code == ValidationErrorCodes.INVALID_EXECUTION_NODE));
        }

        [Test]
        public void Validator_WarnsForEmptyCompositeNode()
        {
            var root = TriggerPlanExecutableDsl.Sequence();
            var validator = new TriggerPlanExecutableValidator();

            var result = validator.Validate(root);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Warnings, Has.Some.Matches<ValidationIssue>(issue => issue.Code == ValidationErrorCodes.EMPTY_EXECUTION_NODE));
        }

        [Test]
        public void ScheduledExecutable_ExecutesWrappedChildSynchronously()
        {
            var child = new CountingExecutable();
            var root = TriggerPlanExecutableDsl.Periodic(child, 250f, maxExecutions: 3);

            var result = root.Execute<object>(null, default);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.ExecutedCount, Is.EqualTo(1));
            Assert.That(child.Count, Is.EqualTo(1));
            Assert.That(root.Kind, Is.EqualTo(ETriggerPlanExecutableKind.Scheduled));
            Assert.That(root.ScheduleMode, Is.EqualTo(EScheduleMode.Periodic));
            Assert.That(root.IntervalMs, Is.EqualTo(250f));
            Assert.That(root.MaxExecutions, Is.EqualTo(3));
        }

        [Test]
        public void Validator_RejectsInvalidScheduledExecutableConfig()
        {
            var root = TriggerPlanExecutableDsl.Periodic(null, 0f, maxExecutions: 0);
            var validator = new TriggerPlanExecutableValidator();

            var result = validator.Validate(root);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Matches<ValidationIssue>(issue => issue.Code == ValidationErrorCodes.INVALID_EXECUTION_NODE && issue.Path.EndsWith("intervalMs")));
            Assert.That(result.Errors, Has.Some.Matches<ValidationIssue>(issue => issue.Code == ValidationErrorCodes.INVALID_EXECUTION_NODE && issue.Path.EndsWith("maxExecutions")));
            Assert.That(result.Errors, Has.Some.Matches<ValidationIssue>(issue => issue.Code == ValidationErrorCodes.INVALID_EXECUTION_NODE && issue.Path.EndsWith("child")));
        }

        [Test]
        public void MetadataExecutable_DelegatesExecutionAndPreservesValues()
        {
            var child = new CountingExecutable();
            var root = TriggerPlanExecutableDsl.Tags(child, "damage.fire", "status.burning");

            var result = root.Execute<object>(null, default);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(child.Count, Is.EqualTo(1));
            Assert.That(root.Kind, Is.EqualTo(ETriggerPlanExecutableKind.Metadata));
            Assert.That(root.MetadataKind, Is.EqualTo(ETriggerPlanMetadataKind.Tags));
            Assert.That(root.Values["tag0"], Is.EqualTo("damage.fire"));
            Assert.That(root.Values["tag1"], Is.EqualTo("status.burning"));
        }

        [Test]
        public void DecoratorMetadataDsl_DescribesDurationAndContinuousWithoutLegacyExecutableDependency()
        {
            var durationChild = new CountingExecutable();
            var duration = TriggerPlanExecutableDsl.Duration(durationChild, 1500f, autoStart: false);
            var continuousChild = new CountingExecutable();
            var continuous = TriggerPlanExecutableDsl.ContinuousMetadata(continuousChild, "channel:burning");

            var durationResult = duration.Execute<object>(null, default);
            var continuousResult = continuous.Execute<object>(null, default);

            Assert.That(durationResult.IsSuccess, Is.True);
            Assert.That(continuousResult.IsSuccess, Is.True);
            Assert.That(durationChild.Count, Is.EqualTo(1));
            Assert.That(continuousChild.Count, Is.EqualTo(1));
            Assert.That(duration.MetadataKind, Is.EqualTo(ETriggerPlanMetadataKind.Duration));
            Assert.That(duration.Values["durationMs"], Is.EqualTo("1500"));
            Assert.That(duration.Values["autoStart"], Is.EqualTo("false"));
            Assert.That(continuous.MetadataKind, Is.EqualTo(ETriggerPlanMetadataKind.Continuous));
            Assert.That(continuous.Values["continuationId"], Is.EqualTo("channel:burning"));
        }

        [Test]
        public void Validator_RejectsMetadataWithoutChild()
        {
            var root = TriggerPlanExecutableDsl.Tags(null, "damage.fire");
            var validator = new TriggerPlanExecutableValidator();

            var result = validator.Validate(root);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Matches<ValidationIssue>(issue => issue.Code == ValidationErrorCodes.INVALID_EXECUTION_NODE && issue.Path.EndsWith("child")));
        }

        private sealed class CountingExecutable : ITriggerPlanExecutable
        {
            public int Count { get; private set; }
            public string Name => "Counting";
            public ETriggerPlanExecutableKind Kind => ETriggerPlanExecutableKind.Succeed;
            public float Weight => 1f;

            public TriggerPlanExecutionResult Execute<TCtx>(object args, in ExecCtx<TCtx> ctx) where TCtx : class
            {
                Count++;
                return TriggerPlanExecutionResult.Success();
            }
        }

        private sealed class CountingCondition : ITriggerPlanCondition
        {
            private readonly bool[] _values;

            public int Count { get; private set; }

            public CountingCondition(params bool[] values)
            {
                _values = values;
            }

            public bool Evaluate<TCtx>(object args, in ExecCtx<TCtx> ctx) where TCtx : class
            {
                var index = Count++;
                return _values != null && index < _values.Length && _values[index];
            }
        }
    }
}
