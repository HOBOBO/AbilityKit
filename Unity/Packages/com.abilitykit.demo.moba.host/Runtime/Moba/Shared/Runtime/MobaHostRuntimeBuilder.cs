using System;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Extensions.Rollback;
using AbilityKit.Ability.Host.Extensions.Time;
using AbilityKit.Ability.Host.Extensions.WorldStart;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;

namespace AbilityKit.Ability.Host.Extensions.Moba.Runtime
{
    public readonly struct MobaHostRuntimeProfile
    {
        public readonly bool EnableFrameSync;
        public readonly bool EnableServerFrameTime;
        public readonly bool EnableWorldAutoStart;
        public readonly bool EnableRollback;
        public readonly int RollbackHistoryFrames;
        public readonly int RollbackCaptureEveryNFrames;
        public readonly float FixedDeltaSeconds;

        public MobaHostRuntimeProfile(
            bool enableFrameSync,
            bool enableServerFrameTime,
            bool enableWorldAutoStart,
            bool enableRollback,
            int rollbackHistoryFrames,
            int rollbackCaptureEveryNFrames,
            float fixedDeltaSeconds = 0f)
        {
            EnableFrameSync = enableFrameSync;
            EnableServerFrameTime = enableServerFrameTime;
            EnableWorldAutoStart = enableWorldAutoStart;
            EnableRollback = enableRollback;
            RollbackHistoryFrames = rollbackHistoryFrames;
            RollbackCaptureEveryNFrames = rollbackCaptureEveryNFrames;
            FixedDeltaSeconds = fixedDeltaSeconds;
        }

        public static MobaHostRuntimeProfile LocalAuthoritative(bool enableRollback)
        {
            return new MobaHostRuntimeProfile(
                enableFrameSync: true,
                enableServerFrameTime: true,
                enableWorldAutoStart: true,
                enableRollback: enableRollback,
                rollbackHistoryFrames: 600,
                rollbackCaptureEveryNFrames: 30);
        }

        public MobaHostRuntimeProfile Normalize()
        {
            var history = RollbackHistoryFrames > 0 ? RollbackHistoryFrames : 600;
            var captureEvery = RollbackCaptureEveryNFrames > 0 ? RollbackCaptureEveryNFrames : 30;
            return new MobaHostRuntimeProfile(
                EnableFrameSync,
                EnableServerFrameTime,
                EnableWorldAutoStart,
                EnableRollback,
                history,
                captureEvery,
                FixedDeltaSeconds);
        }
    }

    public readonly struct MobaHostRuntimeBuildResult
    {
        public readonly HostRuntime Runtime;
        public readonly HostRuntimeOptions Options;
        public readonly HostRuntimeModuleHost Modules;
        public readonly ServerRollbackModule RollbackModule;

        public MobaHostRuntimeBuildResult(
            HostRuntime runtime,
            HostRuntimeOptions options,
            HostRuntimeModuleHost modules,
            ServerRollbackModule rollbackModule)
        {
            Runtime = runtime;
            Options = options;
            Modules = modules;
            RollbackModule = rollbackModule;
        }
    }

    public static class MobaHostRuntimeBuilder
    {
        public static MobaHostRuntimeBuildResult CreateRuntime(
            IWorldManager worldManager,
            in MobaHostRuntimeProfile profile,
            Func<IWorld, RollbackRegistry> buildRollbackRegistry)
        {
            if (worldManager == null) throw new ArgumentNullException(nameof(worldManager));

            var options = new HostRuntimeOptions();
            var runtime = new HostRuntime(worldManager, options);
            var modules = CreateModules(in profile, buildRollbackRegistry, out var rollbackModule);
            modules.InstallAll(runtime, options);

            return new MobaHostRuntimeBuildResult(runtime, options, modules, rollbackModule);
        }

        public static HostRuntimeModuleHost CreateModules(
            in MobaHostRuntimeProfile profile,
            Func<IWorld, RollbackRegistry> buildRollbackRegistry,
            out ServerRollbackModule rollbackModule)
        {
            rollbackModule = null;
            var normalized = profile.Normalize();
            var modules = new HostRuntimeModuleHost();

            if (normalized.EnableFrameSync)
            {
                modules.Add(new FrameSyncDriverModule());
            }

            if (normalized.EnableServerFrameTime)
            {
                modules.Add(new ServerFrameTimeModule(normalized.FixedDeltaSeconds));
            }

            if (normalized.EnableWorldAutoStart)
            {
                modules.Add(new WorldAutoStartModule());
            }

            if (normalized.EnableRollback)
            {
                rollbackModule = new ServerRollbackModule(
                    normalized.RollbackHistoryFrames,
                    normalized.RollbackCaptureEveryNFrames,
                    buildRollbackRegistry);
                modules.Add(rollbackModule);
            }

            return modules;
        }
    }
}
