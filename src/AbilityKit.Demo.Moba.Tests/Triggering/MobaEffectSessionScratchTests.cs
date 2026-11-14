using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Triggering.Blackboard;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Triggering;

public sealed class MobaEffectSessionScratchTests
{
    [Fact]
    public void Nested_and_sibling_effect_results_are_isolated_and_end_invalidates_reads()
    {
        var key=BlackboardIdMapper.KeyId("skill_runtime.effect.sample");
        var parent=new MobaEffectSessionScratch();
        var parentResolver=new MobaEffectSessionBlackboardResolver(parent,11,null);
        Assert.True(parentResolver.TryResolve(MobaSkillRuntimeTriggerBoards.Effect,out var parentBoard));
        parentBoard.SetDouble(key,12);

        var child=new MobaEffectSessionScratch();
        var childResolver=new MobaEffectSessionBlackboardResolver(child,12,parentResolver);
        Assert.True(childResolver.TryResolve(MobaSkillRuntimeTriggerBoards.Effect,out var childBoard));
        Assert.False(childBoard.TryGetDouble(key,out _));
        childBoard.SetDouble(key,42);
        Assert.True(parentBoard.TryGetDouble(key,out var parentValue));Assert.Equal(12,parentValue);
        child.End();
        Assert.False(childResolver.TryResolve(MobaSkillRuntimeTriggerBoards.Effect,out _));
        Assert.False(childBoard.TryGetDouble(key,out _));
        Assert.True(parentBoard.TryGetDouble(key,out parentValue));Assert.Equal(12,parentValue);

        var sibling=new MobaEffectSessionScratch();
        var siblingResolver=new MobaEffectSessionBlackboardResolver(sibling,13,parentResolver);
        Assert.True(siblingResolver.TryResolve(MobaSkillRuntimeTriggerBoards.Effect,out var siblingBoard));
        Assert.False(siblingBoard.TryGetDouble(key,out _));
        parent.End();
        Assert.False(parentResolver.TryResolve(MobaSkillRuntimeTriggerBoards.Effect,out _));
        Assert.False(parentBoard.TryGetDouble(key,out _));
        sibling.End();
    }

    [Fact]
    public void Standalone_effect_cannot_fall_back_to_persistent_cast_target_or_child_boards()
    {
        var cast=new MobaSkillRuntimeBlackboard();
        var castResolver=new MobaSkillRuntimeBlackboardResolver(cast,1,2,3);
        var scratch=new MobaEffectSessionScratch();
        var resolver=new MobaEffectSessionBlackboardResolver(scratch,4,castResolver);
        foreach(var board in new[]{MobaSkillRuntimeTriggerBoards.Cast,MobaSkillRuntimeTriggerBoards.Target,MobaSkillRuntimeTriggerBoards.Child})
            Assert.False(resolver.TryResolve(board,out _));
        Assert.True(resolver.TryResolve(MobaSkillRuntimeTriggerBoards.Effect,out _));
        scratch.End();
    }
}
