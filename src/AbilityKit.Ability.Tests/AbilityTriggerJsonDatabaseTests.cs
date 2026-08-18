using AbilityKit.Ability.Triggering.Json;
using AbilityKit.Ability.Triggering.Runtime;
using Xunit;

namespace AbilityKit.Ability.Tests;

public sealed class AbilityTriggerJsonDatabaseTests
{
    [Fact]
    public void LoadFromJson_MigratesAllowExternalFalseToExplicitCondition()
    {
        const string json = """
            {
              "Triggers": [
                {
                  "TriggerId": 101,
                  "EventId": "combat.hit",
                  "AllowExternal": false,
                  "Conditions": [],
                  "Actions": []
                }
              ]
            }
            """;

        var database = new AbilityTriggerJsonDatabase();

        database.LoadFromJson(json);

        var record = Assert.Single(database.EnumerateAll());
        var condition = Assert.Single(record.Def.Conditions);
        Assert.Equal(TriggerConditionTypes.ArgEq, condition.Type);
        Assert.Equal("common.is_external", condition.Args["key"]?.ToString());
        Assert.Equal("const", condition.Args["value_source"]?.ToString());
        Assert.Equal("0", condition.Args["value"]?.ToString());
    }

    [Fact]
    public void LoadFromJson_DoesNotDuplicateExistingExternalCondition()
    {
        const string json = """
            {
              "Triggers": [
                {
                  "TriggerId": 102,
                  "EventId": "combat.hit",
                  "AllowExternal": false,
                  "Conditions": [
                    {
                      "Type": "arg_eq",
                      "Args": {
                        "key": "common.is_external",
                        "value_source": "const",
                        "value": 0
                      }
                    }
                  ],
                  "Actions": []
                }
              ]
            }
            """;

        var database = new AbilityTriggerJsonDatabase();

        database.LoadFromJson(json);

        var record = Assert.Single(database.EnumerateAll());
        Assert.Single(record.Def.Conditions);
    }
}
