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
        if (scenario.TickRate is < 1 or > 240) errors.Add("tickRate must be between 1 and 240");
        if (scenario.TimeoutMs <= 0) errors.Add("timeoutMs must be positive");

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
            if (scenario.Timeline[i].AtMs < 0) errors.Add($"timeline[{i}].atMs must be non-negative");
        for (var i = 0; i < scenario.Commands.Count; i++)
        {
            if (scenario.Commands[i].AtMs < 0) errors.Add($"commands[{i}].atMs must be non-negative");
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
