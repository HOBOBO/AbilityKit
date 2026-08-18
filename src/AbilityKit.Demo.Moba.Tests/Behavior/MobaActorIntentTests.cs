using System.Collections.Generic;
using AbilityKit.Ability.Behavior;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Input;
using AbilityKit.Demo.Moba.Services.Behavior;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Behavior;

public sealed class MobaActorIntentTests
{
    [Fact]
    public void BrainOutput_MapsMovementAndCastToCanonicalIntent()
    {
        var output = new BehaviorOutput();
        var target = new Vec3(10f, 0f, 20f);
        output.SetMovement(target, null, 5f);
        output.AddEvent(MobaBrainExecutor.SkillCastEventId, new Dictionary<string, object>
        {
            [MobaBrainExecutor.SkillIdParam] = 101,
            [MobaBrainExecutor.SkillSlotParam] = 2,
            [MobaBrainExecutor.TargetActorIdParam] = 9,
            [MobaBrainExecutor.AimPositionParam] = target,
            [MobaBrainExecutor.AimDirectionParam] = Vec3.Forward,
        });

        var intent = MobaBrainIntentReader.Read(output);

        Assert.Equal(MobaActorMovementIntentKind.TargetPosition, intent.MovementKind);
        Assert.Equal(target, intent.MoveTarget);
        Assert.True(intent.HasCast);
        Assert.Equal(101, intent.SkillId);
        Assert.Equal(2, intent.SkillSlot);
        Assert.Equal(9, intent.TargetActorId);
    }

    [Fact]
    public void DirectionIntent_ClampsAxesAndRejectsNonFiniteMovement()
    {
        var clamped = MobaActorIntent.MoveDirection(2f, -2f);
        var invalid = MobaActorIntent.MoveTo(new Vec3(float.NaN, 0f, 1f));

        Assert.Equal(1f, clamped.MoveX);
        Assert.Equal(-1f, clamped.MoveZ);
        Assert.True(clamped.HasFiniteMovement());
        Assert.False(invalid.HasFiniteMovement());
    }

    [Fact]
    public void CastIntent_RejectsInvalidAimAndSkillSlot()
    {
        var invalidAim = MobaActorIntent.Cast(
            1,
            aimDirection: new Vec3(float.NaN, 0f, 1f));
        var invalidSlot = MobaActorIntent.Cast(0);

        Assert.False(invalidAim.IsValid());
        Assert.False(invalidSlot.IsValid());
    }
}
