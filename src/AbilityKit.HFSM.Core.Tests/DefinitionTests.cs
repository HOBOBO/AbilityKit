using AbilityKit.HFSM;
using AbilityKit.HFSM.Runtime;
using AbilityKit.HFSM.Definition;
using Xunit;

namespace AbilityKit.HFSM.Core.Tests;

public sealed class DefinitionTests
{
    [Fact]
    public void ValidatorRejectsBrokenHierarchyAndTransitionReferences()
    {
        var definition = Fixtures.Flat(Fixtures.State("idle", childMachine: "missing"));
        definition.Machines[0].Transitions.Add(
            Fixtures.Transition("go", "unknown", "also-unknown"));

        var result = DefinitionValidator.Validate(definition);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "HFSM023");
        Assert.Contains(result.Issues, issue => issue.Code == "HFSM024");
        Assert.Contains(result.Issues, issue => issue.Code == "HFSM030");
    }

    [Fact]
    public void ValidatorRejectsSharedAndUnreachableChildMachines()
    {
        var definition = new StateMachineDefinition
        {
            RootMachineId = "root",
            Machines =
            {
                new MachineDefinition
                {
                    Id = "root",
                    InitialStateId = "a",
                    States =
                    {
                        Fixtures.State("a", childMachine: "child"),
                        Fixtures.State("b", childMachine: "child"),
                    },
                },
                new MachineDefinition
                {
                    Id = "child",
                    InitialStateId = "idle",
                    States = { Fixtures.State("idle") },
                },
                new MachineDefinition
                {
                    Id = "orphan",
                    InitialStateId = "idle",
                    States = { Fixtures.State("idle") },
                },
            },
        };

        var result = DefinitionValidator.Validate(definition);

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

    private static StateMachineDefinition CreateHashFixture(bool reverseLists)
    {
        var root = new MachineDefinition
        {
            Id = "root",
            InitialStateId = "idle",
            States =
            {
                Fixtures.State("idle"),
                Fixtures.State("run"),
            },
            Transitions =
            {
                Fixtures.Transition("b", "idle", "run", priority: 2),
                Fixtures.Transition("a", "idle", "run", priority: 1),
            },
        };
        var child = new MachineDefinition
        {
            Id = "child",
            InitialStateId = "only",
            States = { Fixtures.State("only") },
        };
        if (reverseLists)
        {
            root.States.Reverse();
            root.Transitions.Reverse();
        }

        var definition = new StateMachineDefinition { RootMachineId = "root" };
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
