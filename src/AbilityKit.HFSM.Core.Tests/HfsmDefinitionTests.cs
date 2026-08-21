using AbilityKit.HFSM;
using Xunit;

namespace AbilityKit.HFSM.Core.Tests;

public sealed class HfsmDefinitionTests
{
    [Fact]
    public void ValidatorRejectsBrokenHierarchyAndTransitionReferences()
    {
        var definition = HfsmFixtures.Flat(HfsmFixtures.State("idle", childMachine: "missing"));
        definition.Machines[0].Transitions.Add(
            HfsmFixtures.Transition("go", "unknown", "also-unknown"));

        var result = HfsmDefinitionValidator.Validate(definition);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "HFSM023");
        Assert.Contains(result.Issues, issue => issue.Code == "HFSM024");
        Assert.Contains(result.Issues, issue => issue.Code == "HFSM030");
    }

    [Fact]
    public void ValidatorRejectsSharedAndUnreachableChildMachines()
    {
        var definition = new HfsmDefinition
        {
            RootMachineId = "root",
            Machines =
            {
                new HfsmMachineDefinition
                {
                    Id = "root",
                    InitialStateId = "a",
                    States =
                    {
                        HfsmFixtures.State("a", childMachine: "child"),
                        HfsmFixtures.State("b", childMachine: "child"),
                    },
                },
                new HfsmMachineDefinition
                {
                    Id = "child",
                    InitialStateId = "idle",
                    States = { HfsmFixtures.State("idle") },
                },
                new HfsmMachineDefinition
                {
                    Id = "orphan",
                    InitialStateId = "idle",
                    States = { HfsmFixtures.State("idle") },
                },
            },
        };

        var result = HfsmDefinitionValidator.Validate(definition);

        Assert.Contains(result.Issues, issue => issue.Code == "HFSM032");
        Assert.Contains(result.Issues, issue => issue.Code == "HFSM034" && issue.Path.Contains("orphan"));
    }

    [Fact]
    public void SemanticHashIgnoresSourceListOrderButTracksRuntimeChanges()
    {
        var first = CreateHashFixture(reverseLists: false);
        var reordered = CreateHashFixture(reverseLists: true);

        Assert.Equal(first.ComputeDefinitionHash(), reordered.ComputeDefinitionHash());

        reordered.Machines.Single(machine => machine.Id == "root")
            .Transitions.Single(transition => transition.Id == "a").Priority++;
        Assert.NotEqual(first.ComputeDefinitionHash(), reordered.ComputeDefinitionHash());
    }

    private static HfsmDefinition CreateHashFixture(bool reverseLists)
    {
        var root = new HfsmMachineDefinition
        {
            Id = "root",
            InitialStateId = "idle",
            States =
            {
                HfsmFixtures.State("idle"),
                HfsmFixtures.State("run"),
            },
            Transitions =
            {
                HfsmFixtures.Transition("b", "idle", "run", priority: 2),
                HfsmFixtures.Transition("a", "idle", "run", priority: 1),
            },
        };
        var child = new HfsmMachineDefinition
        {
            Id = "child",
            InitialStateId = "only",
            States = { HfsmFixtures.State("only") },
        };
        if (reverseLists)
        {
            root.States.Reverse();
            root.Transitions.Reverse();
        }

        var definition = new HfsmDefinition { RootMachineId = "root" };
        if (reverseLists)
        {
            definition.Machines.Add(child);
            definition.Machines.Add(root);
        }
        else
        {
            definition.Machines.Add(root);
            definition.Machines.Add(child);
        }

        // Keep both fixtures valid and reachable without changing root state ordering semantics.
        root.States.Single(state => state.Id == "run").ChildMachineId = "child";
        return definition;
    }
}
