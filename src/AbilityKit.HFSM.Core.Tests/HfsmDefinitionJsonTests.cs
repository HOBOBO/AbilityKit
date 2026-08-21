using AbilityKit.HFSM;
using Xunit;

namespace AbilityKit.HFSM.Core.Tests;

public sealed class HfsmDefinitionJsonTests
{
    [Fact]
    public void SaveUsesCanonicalOrderAndRawIntegerValues()
    {
        var definition = HfsmFixtures.Flat(
            HfsmFixtures.State("z"),
            HfsmFixtures.State("a", "behavior"));
        definition.DefinitionId = "combat";
        definition.Machines[0].InitialStateId = "a";
        definition.Machines[0].Transitions.Add(HfsmFixtures.Transition(
            "go",
            "a",
            "z",
            condition: "ready",
            priority: 7,
            force: true,
            minimumDurationRaw: 4294967296L));

        var json = HfsmDefinitionJson.Save(definition);

        Assert.Equal(GoldenJson, json);
    }

    [Fact]
    public void RoundTripPreservesSemanticHashAndCanonicalBytes()
    {
        var definition = HfsmFixtures.Flat(
            HfsmFixtures.State("z"),
            HfsmFixtures.State("a"));
        definition.Machines[0].InitialStateId = "a";
        definition.Machines[0].Transitions.Add(HfsmFixtures.Transition("go", "a", "z"));

        var first = HfsmDefinitionJson.Save(definition);
        var restored = HfsmDefinitionJson.Load(first);
        var second = HfsmDefinitionJson.Save(restored);

        Assert.Equal(definition.ComputeDefinitionHash(), restored.ComputeDefinitionHash());
        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("{\"formatVersion\":1,\"formatVersion\":1}")]
    [InlineData("{\"formatVersion\":1,\"definitionId\":\"x\",\"rootMachineId\":\"root\",\"machines\":[],\"unknown\":true}")]
    public void LoadRejectsDuplicateAndUnknownProperties(string json)
    {
        Assert.Throws<HfsmDefinitionJsonException>(() => HfsmDefinitionJson.Load(json));
    }

    [Fact]
    public void LoadRunsDefinitionValidation()
    {
        var json = GoldenJson.Replace("\"rootMachineId\": \"root\"", "\"rootMachineId\": \"missing\"");

        var exception = Assert.Throws<HfsmDefinitionException>(() => HfsmDefinitionJson.Load(json));

        Assert.Contains(exception.Issues, issue => issue.Code == "HFSM007");
    }

    private const string GoldenJson = """
{
  "formatVersion": 1,
  "definitionId": "combat",
  "rootMachineId": "root",
  "machines": [
    {
      "id": "root",
      "initialStateId": "a",
      "rememberLastState": false,
      "states": [
        {
          "id": "a",
          "behaviorKey": "behavior",
          "childMachineId": "",
          "requiresExitApproval": false
        },
        {
          "id": "z",
          "behaviorKey": "",
          "childMachineId": "",
          "requiresExitApproval": false
        }
      ],
      "transitions": [
        {
          "id": "go",
          "fromAnyState": false,
          "fromStateId": "a",
          "toStateId": "z",
          "triggerId": "",
          "conditionKey": "ready",
          "actionKey": "",
          "priority": 7,
          "forceImmediate": true,
          "minimumActiveDurationRaw": 4294967296
        }
      ]
    }
  ]
}
""";
}
