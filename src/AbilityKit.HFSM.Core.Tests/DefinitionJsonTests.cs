using AbilityKit.HFSM;
using AbilityKit.HFSM.Runtime;
using AbilityKit.HFSM.Definition;
using Xunit;

namespace AbilityKit.HFSM.Core.Tests;

public sealed class DefinitionJsonTests
{
    [Fact]
    public void SaveUsesCanonicalOrderAndRawIntegerValues()
    {
        var definition = Fixtures.Flat(
            Fixtures.State("z"),
            Fixtures.State("a", "behavior"));
        definition.DefinitionId = "combat";
        definition.Machines[0].InitialStateId = "a";
        definition.Machines[0].Transitions.Add(Fixtures.Transition(
            "go",
            "a",
            "z",
            condition: "ready",
            priority: 7,
            force: true,
            minimumDurationRaw: 4294967296L));

        var json = DefinitionJson.Save(definition);

        Assert.Equal(GoldenJson, json);
    }

    [Fact]
    public void RoundTripPreservesSemanticHashAndCanonicalBytes()
    {
        var definition = Fixtures.Flat(
            Fixtures.State("z"),
            Fixtures.State("a"));
        definition.Machines[0].InitialStateId = "a";
        definition.Machines[0].Transitions.Add(Fixtures.Transition("go", "a", "z"));

        var first = DefinitionJson.Save(definition);
        var restored = DefinitionJson.Load(first);
        var second = DefinitionJson.Save(restored);

        Assert.Equal(definition.ComputeDefinitionHash(), restored.ComputeDefinitionHash());
        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("{\"formatVersion\":1,\"formatVersion\":1}")]
    [InlineData("{\"formatVersion\":1,\"definitionId\":\"x\",\"rootMachineId\":\"root\",\"machines\":[],\"unknown\":true}")]
    public void LoadRejectsDuplicateAndUnknownProperties(string json)
    {
        Assert.Throws<DefinitionJsonException>(() => DefinitionJson.Load(json));
    }

    [Fact]
    public void LoadRunsDefinitionValidation()
    {
        var json = GoldenJson.Replace("\"rootMachineId\": \"root\"", "\"rootMachineId\": \"missing\"");

        var exception = Assert.Throws<DefinitionException>(() => DefinitionJson.Load(json));

        Assert.Contains(exception.Issues, issue => issue.Code == "HFSM007");
    }

    [Fact]
    public void LoadMigratesVersionOneDefinitionsToCurrentDefaults()
    {
        var versionOne = GoldenJson
            .Replace("\"formatVersion\": 2", "\"formatVersion\": 1")
            .Replace("          \"isGhostState\": false,\n", string.Empty)
            .Replace("          \"parallelBehaviorKeys\": [],\n", string.Empty)
            .Replace("          \"parallelExitPolicy\": \"Any\"\n", string.Empty)
            .Replace("          \"requiresExitApproval\": false,\n", "          \"requiresExitApproval\": false\n")
            .Replace("          \"exitMachine\": false,\n", string.Empty);

        var definition = DefinitionJson.Load(versionOne);

        Assert.Equal(StateMachineDefinition.CurrentFormatVersion, definition.FormatVersion);
        Assert.All(definition.Machines.SelectMany(machine => machine.States), state =>
        {
            Assert.False(state.IsGhostState);
            Assert.Empty(state.ParallelBehaviorKeys);
            Assert.Equal(ParallelExitPolicy.Any, state.ParallelExitPolicy);
        });
        Assert.False(definition.Machines[0].Transitions[0].ExitMachine);
    }

    private const string GoldenJson = """
{
  "formatVersion": 2,
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
          "requiresExitApproval": false,
          "isGhostState": false,
          "parallelBehaviorKeys": [],
          "parallelExitPolicy": "Any"
        },
        {
          "id": "z",
          "behaviorKey": "",
          "childMachineId": "",
          "requiresExitApproval": false,
          "isGhostState": false,
          "parallelBehaviorKeys": [],
          "parallelExitPolicy": "Any"
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
          "exitMachine": false,
          "minimumActiveDurationRaw": 4294967296
        }
      ]
    }
  ]
}
""";
}
