using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host.Extensions.Time;
using AbilityKit.Ability.Host.Extensions.WorldStart;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 权威驱动的状态同步世界：不安装输入帧模块，不因渲染更新而推进时钟。
    /// </summary>
    internal static class StateSyncWorldRuntime
    {
        internal static HostRuntimeModuleHost CreateModules(float fixedDeltaSeconds) => new HostRuntimeModuleHost()
            .Add(new ServerFrameTimeModule(fixedDeltaSeconds, advanceOnHostTickFallback: false))
            .Add(new WorldAutoStartModule());

        internal static void EnsureStarted(
            BattleStartPlan plan, BattleSessionRemoteDrivenWorldRuntime handles, float fixedDeltaSeconds)
        {
            if (handles.World != null) return;

            var runtime = RemoteDrivenWorldRuntime.Create(plan, CreateModules(fixedDeltaSeconds));
            handles.BindWorldRuntime(runtime);
            if (runtime.World.Services.TryResolve<MobaAuthorityFrameService>(out var authorityFrame) && authorityFrame != null)
            {
                authorityFrame.BindWorld(runtime.WorldId);
                authorityFrame.ConfigureRollbackHistoryFrames(0);
            }
            SessionWorldBootstrapValidator.ValidateServices(runtime.World, "StateSyncLocalWorld");
        }

        internal static bool TryCommitTick(IWorld world, int tick, float fixedDeltaSeconds)
        {
            if (world?.Services == null || !world.Services.TryResolve<IFrameTime>(out var time) ||
                time is not FrameTime clock || tick < clock.Frame.Value || !(fixedDeltaSeconds > 0f)) return false;

            clock.Reset(new FrameIndex(tick), tick * fixedDeltaSeconds, fixedDeltaSeconds);
            return true;
        }
    }
}
