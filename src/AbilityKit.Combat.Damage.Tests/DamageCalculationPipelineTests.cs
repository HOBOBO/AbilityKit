using AbilityKit.Combat;
using AbilityKit.Dataflow;
using Xunit;

namespace AbilityKit.Combat.Damage.Tests;

public sealed class DamageCalculationPipelineTests
{
    [Fact]
    public void Invalid_request_aborts_at_validation_and_preserves_request_output()
    {
        var request = DamageRequest.Create(new object(), null!, new object(), 100, DamageType.Physical);
        var context = new DamageCalculationContext();

        var result = DamageCalculationPipeline.CreateDefault().Execute(request, context);

        Assert.True(result.IsAborted);
        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(100, result.Output.Request.BaseValue);
        Assert.Equal(request.Target, result.Output.Request.Target);
        Assert.Equal(request.BaseValue, context.Result.Request.BaseValue);
    }

    [Fact]
    public void Default_pipeline_executes_all_eight_stages()
    {
        var request = CreateRequest(100);
        var context = new DamageCalculationContext
        {
            TargetCurrentHealth = 80
        };

        var result = DamageCalculationPipeline.CreateDefault().Execute(request, context);

        Assert.True(result.IsSuccess);
        Assert.Equal(8, result.ProcessedCount);
        Assert.Equal(100, result.Output.RawDamage);
        Assert.Equal(100, result.Output.FinalDamage);
        Assert.Equal(80, result.Output.ActualDamage);
        Assert.Equal(20, result.Output.Overkill);
        Assert.Equal(result.Output.FinalDamage, context.Result.FinalDamage);
    }

    [Fact]
    public async Task Shared_pipeline_keeps_concurrent_execution_results_isolated()
    {
        var pipeline = DamageCalculationPipeline.CreateDefault();
        var tasks = Enumerable.Range(0, 64).Select(index => Task.Run(() =>
        {
            var context = new DamageCalculationContext
            {
                AttackerPhysicalDamage = index,
                TargetCurrentHealth = 1000
            };

            var result = pipeline.Execute(CreateRequest(100), context);
            return (Index: index, Result: result, Context: context);
        }));

        var executions = await Task.WhenAll(tasks);

        foreach (var execution in executions)
        {
            Assert.True(execution.Result.IsSuccess);
            Assert.Equal(100 + execution.Index, execution.Result.Output.FinalDamage);
            Assert.Equal(100 + execution.Index, execution.Context.Result.FinalDamage);
        }
    }

    [Fact]
    public void Clear_resets_damage_specific_and_base_context_state()
    {
        var slot = new DataflowSlot<int>("Transient");
        var context = new DamageCalculationContext
        {
            Request = CreateRequest(100),
            Result = DamageResult.Create(CreateRequest(100)),
            TargetArmor = 20,
            TargetMagicResist = 30,
            TargetMaxHealth = 500,
            TargetCurrentHealth = 400,
            AttackerPhysicalDamage = 50,
            AttackerMagicDamage = 60
        };
        context.SetSource(new object());
        context.SetData(slot, 1);
        context.Abort();

        context.Clear();

        Assert.Equal(default, context.Request);
        Assert.Equal(default, context.Result);
        Assert.Equal(0, context.TargetArmor);
        Assert.Equal(0, context.TargetMagicResist);
        Assert.Equal(0, context.TargetMaxHealth);
        Assert.Equal(0, context.TargetCurrentHealth);
        Assert.Equal(0, context.AttackerPhysicalDamage);
        Assert.Equal(0, context.AttackerMagicDamage);
        Assert.Null(context.Source);
        Assert.False(context.ContainsData(slot));
        Assert.False(context.IsAborted);
    }

    private static DamageRequest CreateRequest(float baseValue)
    {
        return DamageRequest.Create(
            new object(),
            new object(),
            new object(),
            baseValue,
            DamageType.Physical);
    }
}
