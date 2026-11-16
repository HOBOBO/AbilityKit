using AbilityKit.Demo.Moba.Services.Triggering.PlanActions;
using AbilityKit.Triggering.Runtime.Plan;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class AreaClaimQualificationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(127)]
    public void Area_and_pattern_schemas_preserve_explicit_claim_selection(int mask)
    {
        var args = new Dictionary<string,ActionArgValue>
        {
            ["area_id"]=ActionArgValue.OfConst(4001,"area_id"),
            ["claim_unit_sub_type_mask"]=ActionArgValue.OfConst(mask,"claim_unit_sub_type_mask")
        };
        var array=args.ToArray();
        Assert.True(SpawnAreaSchema.Instance.TryValidateArgs(array,out var error),error);
        Assert.True(SpawnAreaBatchSchema.Instance.TryValidateArgs(array,out error),error);
        Assert.Equal(mask,SpawnAreaSchema.Instance.ParseArgs(args,default).ClaimUnitSubTypeMask);
        var batch=SpawnAreaBatchSchema.Instance.ParseArgs(args,default);
        Assert.Equal(mask,batch.ClaimUnitSubTypeMask);
        Assert.Equal(mask,batch.ToAreaArgs(1,1,false).ClaimUnitSubTypeMask);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(128)]
    [InlineData(1.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Invalid_claim_masks_fail_both_authoring_and_runtime_parse(double mask)
    {
        var args = new Dictionary<string,ActionArgValue>
        {
            ["area_id"]=ActionArgValue.OfConst(4001,"area_id"),
            ["claim_unit_sub_type_mask"]=ActionArgValue.OfConst(mask,"claim_unit_sub_type_mask")
        };
        Assert.False(SpawnAreaSchema.Instance.TryValidateArgs(args.ToArray(),out _));
        Assert.False(SpawnAreaBatchSchema.Instance.TryValidateArgs(args.ToArray(),out _));
        Assert.ThrowsAny<Exception>(()=>SpawnAreaSchema.Instance.ParseArgs(args,default));
        Assert.ThrowsAny<Exception>(()=>SpawnAreaBatchSchema.Instance.ParseArgs(args,default));
    }
}
