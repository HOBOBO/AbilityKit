using AbilityKit.Demo.Moba.Services;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class MobaSkillInputHandleResultTests
{
    [Fact]
    public void Failed_creates_stable_input_failure_code()
    {
        var result = MobaSkillInputHandleResult.Failed("skill.input.invalidSlot", "Invalid skill slot.");

        Assert.False(result.Success);
        Assert.Equal("skill.input.invalidSlot", result.Code);
        Assert.Equal("Input", result.Failure.Source);
        Assert.Equal("Invalid skill slot.", result.Message);
    }

    [Fact]
    public void From_cast_preserves_structured_cast_failure_code()
    {
        var failure = new MobaSkillCastFailure("Preparation", null, "skill.cast.slotNotFound", "Skill not found in slot.");
        var runtimeHandle = new MobaSkillCastRuntimeHandle(701, 3, 901);
        var cast = MobaSkillCastResult.From(false, "Skill not found in slot.", in runtimeHandle, in failure);

        var result = MobaSkillInputHandleResult.FromCast(in cast);

        Assert.False(result.Success);
        Assert.Equal("skill.cast.slotNotFound", result.Code);
        Assert.Equal("Preparation", result.Failure.Source);
        Assert.Equal("Skill not found in slot.", result.Message);
        Assert.Equal(runtimeHandle, result.RuntimeHandle);
    }

    [Fact]
    public void From_cast_maps_success_to_input_success_message()
    {
        var runtimeHandle = new MobaSkillCastRuntimeHandle(700, 2, 900);
        var cast = MobaSkillCastResult.From(true, null, in runtimeHandle);

        var result = MobaSkillInputHandleResult.FromCast(in cast, "skill.input.cast.started");

        Assert.True(result.Success);
        Assert.Equal("skill.input.cast.started", result.Message);
        Assert.False(result.Failure.HasValue);
        Assert.Equal(runtimeHandle, result.RuntimeHandle);
    }

    [Fact]
    public void Input_command_diagnostic_enrichment_preserves_existing_fields()
    {
        var runtimeHandle = new MobaSkillCastRuntimeHandle(700, 2, 900);
        var source = new MobaInputCommandResult(
            false,
            MobaInputCommandFailureCode.SkillRejected,
            "Skill rejected.",
            "player-one",
            actorId: 7,
            opCode: 17);

        var result = source
            .WithSkillDiagnostic(2, 1, 41, in runtimeHandle)
            .WithDiagnosticCommandId(4401);

        Assert.False(result.Succeeded);
        Assert.Equal(MobaInputCommandFailureCode.SkillRejected, result.FailureCode);
        Assert.Equal("Skill rejected.", result.Message);
        Assert.Equal("player-one", result.PlayerId);
        Assert.Equal(7, result.ActorId);
        Assert.Equal(17, result.OpCode);
        Assert.Equal(4401, result.DiagnosticCommandId);
        Assert.Equal(2, result.SkillSlot);
        Assert.Equal(1, result.SkillPhase);
        Assert.Equal(41, result.TargetActorId);
        Assert.Equal(runtimeHandle, result.SkillRuntimeHandle);
    }
}
