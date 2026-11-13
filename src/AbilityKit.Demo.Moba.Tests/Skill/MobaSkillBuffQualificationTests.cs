using System.Collections.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class MobaSkillBuffQualificationTests
{
    [Fact]
    public void Reservation_competes_releases_on_cancel_and_never_consumes_a_refreshed_source()
    {
        var casts = new MobaSkillCastRuntimeService();
        var owner = new ActorContext().CreateEntity();
        owner.AddActorId(7);
        owner.AddBuffs(new List<BuffRuntime> { new BuffRuntime { BuffId = 501, SourceId = 7, SourceContextId = 1001 } });
        var first = Create(); var second = Create();
        var wrongRoot = new MobaSkillCastRuntimeHandle(first.RuntimeId, first.Generation, first.RootContextId + 1);
        Assert.False(casts.TryReserveBuffQualification(wrongRoot, 501, owner));
        Assert.True(casts.TryReserveBuffQualification(first.Handle, 501, owner));
        Assert.False(casts.TryReserveBuffQualification(second.Handle, 501, owner));
        casts.ForceTerminate(first.Handle, MobaSkillRuntimeEndReason.RollbackCleanup);
        Assert.True(casts.TryReserveBuffQualification(second.Handle, 501, owner));
        var snapshot = casts.CaptureRollbackSnapshot();
        owner.buffs.Active[0].SourceContextId = 1002;
        Assert.False(casts.TryClaimBuffQualification(second.Handle, owner, out _, out _, out _));
        var third = Create();
        Assert.True(casts.TryReserveBuffQualification(third.Handle, 501, owner));
        wrongRoot = new MobaSkillCastRuntimeHandle(third.RuntimeId, third.Generation, third.RootContextId + 1);
        Assert.False(casts.TryClaimBuffQualification(wrongRoot, owner, out _, out _, out _));
        Assert.True(casts.TryClaimBuffQualification(third.Handle, owner, out var buff, out var source, out var context));
        Assert.Equal(501, buff); Assert.Equal(7, source); Assert.Equal(1002, context);
        Assert.False(casts.TryClaimBuffQualification(third.Handle, owner, out _, out _, out _));
        casts.RestoreRollbackSnapshot(snapshot);
        owner.buffs.Active[0].SourceContextId = 1001;
        Assert.True(casts.TryClaimBuffQualification(second.Handle, owner, out _, out _, out context));
        Assert.Equal(1001, context);
        Assert.False(casts.TryClaimBuffQualification(second.Handle, owner, out _, out _, out _));

        MobaSkillCastRuntime Create() => casts.Create(new MobaSkillCastRuntimeCreateRequest(101, 1, 1, 1, 7, 8, Vec3.Zero, Vec3.Forward, 2000 + casts.Count));
    }

    [Fact]
    public void Refund_uses_frozen_total_checks_original_identity_and_survives_restore()
    {
        var casts = new MobaSkillCastRuntimeService();
        var cast = casts.Create(new MobaSkillCastRuntimeCreateRequest(101, 4, 1, 1, 7, 8, Vec3.Zero, Vec3.Forward, 2001));
        casts.FreezeEffectiveCooldown(cast.Handle, 1001);
        var wrongRoot = new MobaSkillCastRuntimeHandle(cast.RuntimeId, cast.Generation, cast.RootContextId + 1);
        Assert.False(casts.TryResolveCooldownRefund(wrongRoot, 7, 101, 4, .5, out _));
        var snapshot = casts.CaptureRollbackSnapshot();
        Assert.True(casts.TryResolveCooldownRefund(cast.Handle, 7, 101, 4, .5, out var refund));
        Assert.Equal(501, refund);
        Assert.True(casts.TryResolveCooldownRefund(cast.Handle, 7, 101, 4, 0, out refund));
        Assert.Equal(0, refund);
        Assert.False(casts.TryResolveCooldownRefund(cast.Handle, 8, 101, 4, .4, out _));
        Assert.False(casts.TryResolveCooldownRefund(cast.Handle, 7, 102, 4, .4, out _));
        Assert.False(casts.TryResolveCooldownRefund(cast.Handle, 7, 101, 3, .4, out _));
        casts.ForceTerminate(cast.Handle, MobaSkillRuntimeEndReason.RollbackCleanup);
        Assert.False(casts.TryResolveCooldownRefund(cast.Handle, 7, 101, 4, .4, out _));
        casts.RestoreRollbackSnapshot(snapshot);
        Assert.True(casts.TryResolveCooldownRefund(cast.Handle, 7, 101, 4, .4, out refund));
        Assert.Equal(400, refund);
    }
}
