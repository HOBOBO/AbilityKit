using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Core.Mathematics;
using Xunit;

namespace AbilityKit.Combat.Motion.Tests;

public sealed class MotionPipelinePolicyStateTests
{
    [Fact]
    public void Capture_returns_sorted_independent_rules_and_preserves_arbitrary_values()
    {
        var policy = new MotionPipelinePolicy();
        policy.SetSuppressedGroups(int.MaxValue, int.MinValue, 0, int.MinValue);
        policy.SetSuppressedGroups(-7, 9);

        var state = policy.CaptureState();

        Assert.Equal(new[] { -7, int.MaxValue }, state.Rules.Select(rule => rule.SuppressorGroupId));
        Assert.Equal(new[] { int.MinValue, 0, int.MinValue }, state.Rules[1].SuppressedGroupIds);
        Assert.NotSame(state.Rules[1].SuppressedGroupIds, GetPolicyArray(policy, int.MaxValue));
        MotionPipelinePolicy.ValidateState(in state);
    }

    [Fact]
    public void Default_and_empty_policy_capture_as_complete_valid_values()
    {
        var defaultState = MotionPipelinePolicy.CreateDefault().CaptureState();
        Assert.Equal(3, defaultState.Rules.Length);
        Assert.Equal(defaultState.Rules.Select(rule => rule.SuppressorGroupId).Order(),
            defaultState.Rules.Select(rule => rule.SuppressorGroupId));
        MotionPipelinePolicy.ValidateState(in defaultState);

        var emptyState = new MotionPipelinePolicy().CaptureState();
        Assert.Empty(emptyState.Rules);
        MotionPipelinePolicy.ValidateState(in emptyState);
    }

    [Fact]
    public void Captures_are_isolated_from_getter_setter_and_other_capture_mutations()
    {
        var policy = new MotionPipelinePolicy();
        var input = new[] { 3, 4 };
        policy.SetSuppressedGroups(1, input);
        var first = policy.CaptureState();
        var second = policy.CaptureState();

        input[0] = 99;
        policy.TryGetSuppressedGroups(1, out var legacyArray);
        legacyArray[1] = 88;
        first.Rules[0].SuppressedGroupIds[0] = 77;

        Assert.Equal(new[] { 3, 4 }, second.Rules[0].SuppressedGroupIds);
        Assert.Equal(new[] { 3, 88 }, policy.CaptureState().Rules[0].SuppressedGroupIds);
    }

    [Fact]
    public void Restore_replaces_all_rules_and_owns_input_arrays()
    {
        var policy = new MotionPipelinePolicy();
        policy.SetSuppressedGroups(1, 2);
        policy.SetSuppressedGroups(3, 4);
        var target = new MotionPipelinePolicyState
        {
            Rules = new[]
            {
                new MotionGroupSuppressionState { SuppressorGroupId = -9, SuppressedGroupIds = new[] { 8, 8 } },
            },
        };

        policy.RestoreState(in target);
        Assert.Equal(new[] { -9 }, policy.CaptureState().Rules.Select(rule => rule.SuppressorGroupId));
        target.Rules[0].SuppressedGroupIds[0] = 100;
        Assert.Equal(new[] { 8, 8 }, policy.CaptureState().Rules[0].SuppressedGroupIds);

        var empty = new MotionPipelinePolicyState { Rules = Array.Empty<MotionGroupSuppressionState>() };
        policy.RestoreState(in empty);
        policy.RestoreState(in empty);
        Assert.Empty(policy.CaptureState().Rules);
    }

    [Theory]
    [InlineData("null-rules", "Rules")]
    [InlineData("null-groups", "Rules[0].SuppressedGroupIds")]
    [InlineData("empty-groups", "Rules[0].SuppressedGroupIds")]
    [InlineData("duplicate-key", "Rules[1].SuppressorGroupId")]
    [InlineData("unordered-key", "Rules[1].SuppressorGroupId")]
    public void Invalid_states_are_read_only_and_restore_failure_keeps_policy_and_arbitration(string kind, string field)
    {
        var policy = new MotionPipelinePolicy();
        policy.SetSuppressedGroups(5, MotionGroups.Locomotion);
        var before = policy.CaptureState();
        var invalid = InvalidState(kind);

        var validation = Assert.Throws<ArgumentException>(() => MotionPipelinePolicy.ValidateState(in invalid));
        Assert.Contains(field, validation.Message);
        Assert.Throws<ArgumentException>(() => policy.RestoreState(in invalid));
        AssertPolicyState(before, policy.CaptureState());
        Assert.Equal(Run(policy.CaptureState()), Run(before));
    }

    [Theory]
    [InlineData(MotionGroups.Path, MotionGroups.Locomotion)]
    [InlineData(MotionGroups.Ability, MotionGroups.Locomotion)]
    [InlineData(MotionGroups.Control, MotionGroups.Path)]
    [InlineData(-123, int.MaxValue)]
    public void Restore_reproduces_pipeline_arbitration_frame_by_frame(int suppressor, int suppressed)
    {
        var original = new MotionPipelinePolicy();
        original.SetSuppressedGroups(suppressor, suppressed);
        var state = original.CaptureState();
        var expected = Run(state);

        var changed = new MotionPipelinePolicy();
        changed.SetSuppressedGroups(suppressor, int.MinValue);
        changed.SetSuppressedGroups(456, 789);
        changed.RestoreState(in state);

        Assert.Equal(expected, Run(changed.CaptureState()));
    }

    private static MotionPipelinePolicyState InvalidState(string kind)
    {
        return kind switch
        {
            "null-rules" => new MotionPipelinePolicyState { Rules = null! },
            "null-groups" => new MotionPipelinePolicyState { Rules = new[] { Rule(1, null!) } },
            "empty-groups" => new MotionPipelinePolicyState { Rules = new[] { Rule(1, Array.Empty<int>()) } },
            "duplicate-key" => new MotionPipelinePolicyState { Rules = new[] { Rule(1, new[] { 2 }), Rule(1, new[] { 3 }) } },
            "unordered-key" => new MotionPipelinePolicyState { Rules = new[] { Rule(2, new[] { 3 }), Rule(1, new[] { 4 }) } },
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static MotionGroupSuppressionState Rule(int id, int[] groups) => new()
    {
        SuppressorGroupId = id,
        SuppressedGroupIds = groups,
    };

    private static int[] GetPolicyArray(MotionPipelinePolicy policy, int groupId)
    {
        policy.TryGetSuppressedGroups(groupId, out var groups);
        return groups;
    }

    private static MotionState[] Run(MotionPipelinePolicyState state)
    {
        using var pipeline = new MotionPipeline { Policy = new MotionPipelinePolicy() };
        pipeline.Policy.RestoreState(in state);
        pipeline.AddSource(new FixedDeltaMotionSource(new Vec3(1, 0, 0), 1, 10, MotionGroups.Locomotion,
            MotionStacking.OverrideLowerPriority));
        pipeline.AddSource(new FixedDeltaMotionSource(new Vec3(0, 0, 2), 1, 10, MotionGroups.Path,
            MotionStacking.OverrideLowerPriority));
        pipeline.AddSource(new FixedDeltaMotionSource(new Vec3(0, 3, 0), 1, 10, MotionGroups.Ability,
            MotionStacking.OverrideLowerPriority));
        pipeline.AddSource(new FixedDeltaMotionSource(new Vec3(-4, 0, 0), 1, 10, MotionGroups.Control,
            MotionStacking.OverrideLowerPriority));
        pipeline.AddSource(new FixedDeltaMotionSource(new Vec3(0, 0, -5), 1, 10, int.MaxValue,
            MotionStacking.OverrideLowerPriority));
        pipeline.AddSource(new FixedDeltaMotionSource(new Vec3(0, 6, 0), 1, 10, -123,
            MotionStacking.OverrideLowerPriority));

        var motion = new MotionState(Vec3.Zero);
        var output = new MotionOutput();
        var frames = new MotionState[4];
        for (var i = 0; i < frames.Length; i++)
        {
            pipeline.Tick(1, ref motion, .125f, ref output);
            frames[i] = motion;
        }

        return frames;
    }

    private static void AssertPolicyState(MotionPipelinePolicyState expected, MotionPipelinePolicyState actual)
    {
        Assert.Equal(expected.Rules.Length, actual.Rules.Length);
        for (var i = 0; i < expected.Rules.Length; i++)
        {
            Assert.Equal(expected.Rules[i].SuppressorGroupId, actual.Rules[i].SuppressorGroupId);
            Assert.Equal(expected.Rules[i].SuppressedGroupIds, actual.Rules[i].SuppressedGroupIds);
        }
    }
}
