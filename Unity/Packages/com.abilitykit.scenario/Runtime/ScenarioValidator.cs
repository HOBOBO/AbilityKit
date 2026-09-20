using System;
using System.Collections.Generic;

namespace AbilityKit.Scenario
{

/// <summary>玩法中立的场景校验器：只校验场景结构（caseId/世界/actor/障碍/时间线/命令），不解释断言插件。</summary>
public static class TestScenarioValidator
{
    public static IReadOnlyList<string> Validate(TestScenario scenario)
    {
        if (scenario is null) throw new ArgumentNullException(nameof(scenario));
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(scenario.CaseId)) errors.Add("caseId is required");
        if (string.IsNullOrWhiteSpace(scenario.WorldProfileId)) errors.Add("worldProfileId is required");
        var execution = scenario.ResolveExecution();
        if (execution.TickRate is < 1 or > 240) errors.Add("execution.tickRate must be between 1 and 240");
        if (execution.MaxDurationMs <= 0) errors.Add("execution.maxDurationMs must be positive");
        if (execution.SettleDurationMs < 0) errors.Add("execution.settleDurationMs must be non-negative");
        if (execution.EndCondition == null)
        {
            errors.Add("execution.endCondition is required");
        }
        else if (execution.EndCondition.Kind == TestEndConditionKinds.Duration)
        {
            if (execution.EndCondition.DurationMs <= 0)
                errors.Add("execution.endCondition.durationMs must be positive");
            else if ((long)execution.EndCondition.DurationMs + execution.SettleDurationMs > execution.MaxDurationMs)
                errors.Add("execution duration plus settleDurationMs must not exceed maxDurationMs");
        }
        else if (execution.EndCondition.Kind != TestEndConditionKinds.TimelineComplete)
        {
            errors.Add($"unsupported execution.endCondition.kind '{execution.EndCondition.Kind}'");
        }

        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var actor in scenario.Actors)
        {
            if (string.IsNullOrWhiteSpace(actor.Alias)) errors.Add("actor alias is required");
            else if (!aliases.Add(actor.Alias)) errors.Add($"duplicate actor alias '{actor.Alias}'");
            if (actor.TeamId < 0) errors.Add($"actor '{actor.Alias}' has invalid teamId");
        }
        var obstacleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var obstacle in scenario.Obstacles)
        {
            if (string.IsNullOrWhiteSpace(obstacle.Id)) errors.Add("obstacle id is required");
            else if (!obstacleIds.Add(obstacle.Id)) errors.Add($"duplicate obstacle id '{obstacle.Id}'");
            if (obstacle.Size.X < 0 || obstacle.Size.Y < 0 || obstacle.Size.Z < 0)
                errors.Add($"obstacle '{obstacle.Id}' has invalid size");
        }
        for (var i = 0; i < scenario.Timeline.Count; i++)
        {
            var step = scenario.Timeline[i];
            if (step.AtMs < 0) errors.Add($"timeline[{i}].atMs must be non-negative");
            if (step.DurationMs < 0) errors.Add($"timeline[{i}].durationMs must be non-negative");
            if (step.AtMs > execution.MaxDurationMs)
                errors.Add($"timeline[{i}].atMs exceeds execution.maxDurationMs");
            if (execution.EndCondition?.Kind == TestEndConditionKinds.Duration &&
                step.AtMs > execution.EndCondition.DurationMs)
                errors.Add($"timeline[{i}].atMs exceeds duration end condition");
        }
        for (var i = 0; i < scenario.Commands.Count; i++)
        {
            if (scenario.Commands[i].AtMs < 0) errors.Add($"commands[{i}].atMs must be non-negative");
            if (scenario.Commands[i].AtMs > execution.MaxDurationMs)
                errors.Add($"commands[{i}].atMs exceeds execution.maxDurationMs");
            if (execution.EndCondition?.Kind == TestEndConditionKinds.Duration &&
                scenario.Commands[i].AtMs > execution.EndCondition.DurationMs)
                errors.Add($"commands[{i}].atMs exceeds duration end condition");
            if (string.IsNullOrWhiteSpace(scenario.Commands[i].Name)) errors.Add($"commands[{i}].name is required");
        }
        return errors;
    }

    public static void ThrowIfInvalid(TestScenario scenario)
    {
        var errors = Validate(scenario);
        if (errors.Count > 0) throw new InvalidOperationException("Invalid test scenario: " + string.Join("; ", errors));
    }
}

}
