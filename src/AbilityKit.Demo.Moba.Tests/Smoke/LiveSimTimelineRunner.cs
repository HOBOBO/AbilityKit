using System;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.LogicWorld;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Protocol.Moba;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>
/// Seam #4 切片 3：acceptance timeline 的 dotnet 移植。
/// 镜像 <c>MobaAcceptanceRunner.ExecuteTimeline/ExecuteAction</c>：按 atMs 排序推进 sim 时钟；
/// wait/tick → tick；环境动词 → <see cref="LiveSimSetupActionExecutor"/>（切片 2）；
/// 技能动词（press/release/hold/cancel/cast_skill/skill_input）→ 富 <c>SkillInputEvent</c> →
/// <c>PlayerInputCommand</c> → <c>IMobaInputCoordinator.TrySubmit</c>——与 harness 完全同一条提交路径。
/// </summary>
public sealed class LiveSimTimelineRunner
{
    private readonly ConsoleBattleBootstrapper _bootstrapper;
    private readonly LiveSimSetupActionExecutor _setup;

    public LiveSimTimelineRunner(ConsoleBattleBootstrapper bootstrapper, LiveSimSetupActionExecutor setup)
    {
        _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
        _setup = setup ?? throw new ArgumentNullException(nameof(setup));
    }

    /// <summary>镜像 ExecuteTimeline：按 atMs 排序，推进 sim 时钟到每个 step 的时间点再执行。</summary>
    public void Run(MobaAcceptanceTimelineStepExpectation[] timeline)
    {
        if (timeline == null || timeline.Length == 0) return;

        var steps = timeline.Where(s => s != null).OrderBy(s => Math.Max(0, s.atMs)).ToArray();
        var cursorMs = 0;
        foreach (var step in steps)
        {
            var atMs = Math.Max(0, step.atMs);
            if (atMs > cursorMs) _setup.TickMilliseconds(atMs - cursorMs);
            cursorMs = Math.Max(cursorMs, atMs);
            ExecuteStep(step);
        }
    }

    private void ExecuteStep(MobaAcceptanceTimelineStepExpectation step)
    {
        if (LiveSimSetupActionExecutor.IsWaitAction(step.action))
        {
            _setup.TickMilliseconds(step.durationMs);
            return;
        }

        if (LiveSimSetupActionExecutor.IsEnvironmentCommand(step.action))
        {
            _setup.Execute(ConvertTimelineStepToSetupAction(step));
            return;
        }

        if (IsSkillAction(step.action))
        {
            SubmitSkillInputAndGetResult(
                ResolveActorAlias(step.actorAlias), step.slot, ResolveSkillInputPhase(step.action),
                step.targetAlias, step.targetActorId, step.position, step.direction,
                $"timeline action={step.action} atMs={Math.Max(0, step.atMs)}");
            // 镜像 AdvanceAcceptedSkillInput：输入提交给 Frame+1，先走一帧让 runtime 消费命令。
            _setup.Tick(1);
            return;
        }

        Assert.Fail($"Unsupported timeline action: {step.action}");
    }

    /// <summary>镜像 harness SubmitSkillInputAndGetResult(alias,…)：富命令经输入协调器提交。</summary>
    private void SubmitSkillInputAndGetResult(
        string actorAlias, int slot, SkillInputPhase phase, string targetAlias, int targetActorId,
        MobaAcceptanceVector3Expectation position, MobaAcceptanceVector3Expectation direction, string context)
    {
        if (!_setup.TryGetPlayerId(actorAlias, out var playerId))
        {
            playerId = new PlayerId(LiveSimSetupActionExecutor.DefaultLocalPlayerId);
        }

        var resolvedTargetActorId = targetActorId;
        if (resolvedTargetActorId <= 0 && !string.IsNullOrEmpty(targetAlias))
        {
            Assert.True(_setup.TryGetActorId(targetAlias, out resolvedTargetActorId),
                $"Target actor alias missing: {targetAlias}");
        }

        // 镜像 harness 的 press-aim 归一化：正式 HUD 输入在 release/带目标提交时才带 aim，
        // 纯方向 press 归一为无 aim，保证 acceptance 场景与运行时输入语义一致。
        var ignorePressAim = phase == SkillInputPhase.Press
                             && resolvedTargetActorId <= 0
                             && IsZeroVector(position)
                             && !IsZeroVector(direction);
        var aimPos = ignorePressAim ? default : ToVec3(position);
        var aimDir = ignorePressAim ? default : ToVec3(direction);

        var services = _bootstrapper.RuntimeServices;
        Assert.NotNull(services);
        Assert.True(services!.TryResolve<IMobaInputCoordinator>(out var input) && input != null,
            "IMobaInputCoordinator must be resolvable from the console world.");

        var castFrame = new FrameIndex(_bootstrapper.Context.LastFrame + 1);
        var skillInput = new SkillInputEvent(slot: slot, phase: phase, targetActorId: resolvedTargetActorId, aimPos: in aimPos, aimDir: in aimDir);
        var command = new PlayerInputCommand(castFrame, playerId, MobaOpCodes.Input.SkillInput, SkillInputCodec.Serialize(in skillInput));
        var result = input!.TrySubmit(castFrame, new[] { command });

        Assert.True(result.Succeeded,
            $"Scenario skill input rejected. {context}, actorAlias={actorAlias}, slot={slot}, phase={phase}, castFrame={castFrame.Value}, result={result}");
        Assert.True(result.HandledCount > 0,
            $"Scenario skill input not handled. {context}, result={result}");
    }

    private static string ResolveActorAlias(string actorAlias)
        => string.IsNullOrEmpty(actorAlias) ? LiveSimSetupActionExecutor.DefaultLocalPlayerId : actorAlias;

    internal static bool IsSkillAction(string action)
    {
        return string.IsNullOrEmpty(action)
               || string.Equals(action, "cast_skill", StringComparison.OrdinalIgnoreCase)
               || string.Equals(action, "skill_input", StringComparison.OrdinalIgnoreCase)
               || string.Equals(action, "press", StringComparison.OrdinalIgnoreCase)
               || string.Equals(action, "release", StringComparison.OrdinalIgnoreCase)
               || string.Equals(action, "hold", StringComparison.OrdinalIgnoreCase)
               || string.Equals(action, "cancel", StringComparison.OrdinalIgnoreCase);
    }

    private static SkillInputPhase ResolveSkillInputPhase(string action)
    {
        return Normalize(action) switch
        {
            "hold" => SkillInputPhase.Hold,
            "release" => SkillInputPhase.Release,
            "cancel" => SkillInputPhase.Cancel,
            _ => SkillInputPhase.Press,
        };
    }

    private static string Normalize(string action) => string.IsNullOrEmpty(action) ? string.Empty : action.Trim().ToLowerInvariant();

    private static Vec3 ToVec3(MobaAcceptanceVector3Expectation value)
        => value == null ? Vec3.Zero : new Vec3(value.x, value.y, value.z);

    private static bool IsZeroVector(MobaAcceptanceVector3Expectation value)
        => value == null
           || (Math.Abs(value.x) <= float.Epsilon && Math.Abs(value.y) <= float.Epsilon && Math.Abs(value.z) <= float.Epsilon);

    private static MobaAcceptanceSetupActionExpectation ConvertTimelineStepToSetupAction(MobaAcceptanceTimelineStepExpectation step)
        => new()
        {
            action = step.action,
            actorAlias = step.actorAlias,
            targetAlias = step.targetAlias,
            playerId = step.playerId,
            slot = step.slot,
            skillId = step.skillId,
            targetActorId = step.targetActorId,
            durationMs = step.durationMs,
            property = step.property,
            value = step.value,
            intValue = step.intValue,
            position = step.position,
            direction = step.direction,
            payload = step.payload,
            note = step.note,
        };
}
