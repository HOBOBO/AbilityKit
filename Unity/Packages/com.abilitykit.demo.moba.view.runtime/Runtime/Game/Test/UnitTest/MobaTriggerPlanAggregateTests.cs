using System;
using System.Linq;
using System.Reflection;
using AbilityKit.Triggering.Runtime.Plan.Json;
using NUnit.Framework;

public sealed class MobaTriggerPlanAggregateTests
{
    [Test]
    public void Compile_SortsTriggersByIdDeterministically()
    {
        var documents = new[]
        {
            Source("z.json", 200),
            Source("a.json", 100)
        };

        var first = TriggerPlanAggregateCompiler.Compile(documents);
        var second = TriggerPlanAggregateCompiler.Compile(documents.Reverse());
        var database = new TriggerPlanJsonDatabase();
        database.LoadFromJson(first, "compiled-test-aggregate");
        var ids = database.Records.Select(record => record.TriggerId).ToArray();

        CollectionAssert.AreEqual(new[] { 100, 200 }, ids);
        Assert.AreEqual(first, second);
    }

    [Test]
    public void Compile_RejectsDuplicateTriggerIdsWithBothSourceNames()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            TriggerPlanAggregateCompiler.Compile(new[]
            {
                Source("first.json", 100),
                Source("second.json", 100)
            }));

        StringAssert.Contains("Duplicate trigger ID 100", exception.Message);
        StringAssert.Contains("first.json", exception.Message);
        StringAssert.Contains("second.json", exception.Message);
    }

    [Test]
    public void CheckedInAggregate_MatchesProductionTriggerAuthoringModules()
    {
        var migration = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(
                "AbilityKit.Ability.Editor.Utilities.TriggerAuthoringMobaMigration"))
            .FirstOrDefault(type => type != null);
        Assert.NotNull(migration, "Trigger Authoring editor assembly was not loaded.");
        var check = migration.GetMethod("CheckRuntimeBatch", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(check);
        try
        {
            check.Invoke(null, null);
        }
        catch (TargetInvocationException ex)
        {
            throw ex.InnerException ?? ex;
        }
    }

    private static TriggerPlanAggregateCompiler.SourceDocument Source(
        string name,
        int triggerId)
    {
        return new TriggerPlanAggregateCompiler.SourceDocument(
            name,
            "{\"triggers\":[{\"id\":" + triggerId
            + ",\"event\":\"\",\"enabled\":true,\"actions\":[]}]}" );
    }

}
