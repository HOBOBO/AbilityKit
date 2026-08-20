using System.Collections.Generic;
using AbilityKit.Ability.Behavior;
using AbilityKit.AI.Abstractions;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Input;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Services.Behavior.AI;
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

    [Fact]
    public void SharedActionCodec_MapsModelActionToCanonicalIntent()
    {
        var action = new AiActionBuffer(MobaBrainActionCodec.ActionSpec);
        action.Continuous[0] = 2f;
        action.Continuous[1] = -0.5f;
        action.Discrete[0] = 9;

        var intent = MobaBrainActionCodec.Decode(action);

        Assert.Equal(1f, intent.MoveX);
        Assert.Equal(-0.5f, intent.MoveZ);
        Assert.True(intent.HasCast);
        Assert.Equal(3, intent.SkillSlot);
    }

    [Fact]
    public void SharedObservationEncoder_ProducesVersionedFiniteObservation()
    {
        var options = new MobaBrainObservationOptions(maxObservedEntities: 2);
        var encoder = new MobaBrainObservationEncoder(options);
        var buffer = new AiObservationBuffer(encoder.ObservationSpec);
        var states = new[]
        {
            new LogicWorldEntityState(10)
            {
                X = 4f,
                Hp = 80f,
                HpMax = 100f,
                TeamId = 1,
                IsDead = false,
            },
            new LogicWorldEntityState(20)
            {
                X = float.NaN,
                Hp = 50f,
                HpMax = 100f,
                TeamId = 2,
                IsDead = false,
            },
        };

        encoder.Write(states, ownerActorId: 10, frame: 30, inputReady: true, inMatch: true, buffer);

        Assert.Equal("moba.runtime-state.v1", buffer.Spec.Id);
        Assert.Equal(24, buffer.Length);
        Assert.Equal(1f, buffer[9]);
        Assert.All(buffer.Values, value => Assert.True(float.IsFinite(value)));
    }
}
