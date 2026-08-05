using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.Extensions.FrameSync.CatchUp
{
    /// <summary>
    /// 客户端帧同步 CatchUp 模块。
    /// 提供服务端 CatchUp 请求/消费方法供重连流程使用。
    ///
    /// NOTE: This module is fully implemented but not yet wired into the client reconnect
    /// flow. Activation planned for v0.2.0 when the reconnect path is finalized.
    /// Until then, <see cref="TryCatchUp"/> is callable but has no upstream caller.
    /// </summary>
    public sealed class FrameSyncCatchUpClientModule : IHostRuntimeModule
    {
        private HostRuntime? _runtime;

        /// <summary>帧同步 CatchUp 策略配置。</summary>
        public FrameSyncCatchUpPolicyOptions PolicyOptions { get; set; } = FrameSyncCatchUpPolicyOptions.Default;

        /// <summary>服务端 CatchUp 请求发送委托。由上层 transport 注入。</summary>
        public Func<FrameSyncCatchUpRequest, FrameSyncCatchUpPayload?>? SendCatchUpRequest { get; set; }

        void IHostRuntimeModule.Install(HostRuntime runtime, HostRuntimeOptions options)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        void IHostRuntimeModule.Uninstall(HostRuntime runtime, HostRuntimeOptions options)
        {
            _runtime = null;
        }

        /// <summary>
        /// 判断是否需要请求服务端 CatchUp，返回请求（若需要）或 null。
        /// </summary>
        public FrameSyncCatchUpRequest? DecideCatchUp(
            WorldId worldId,
            FrameIndex authorityFrame,
            FrameIndex clientLastConfirmedFrame)
        {
            var decision = FrameSyncCatchUpPolicy.Decide(
                worldId,
                authorityFrame,
                clientLastConfirmedFrame,
                PolicyOptions);

            return decision.Kind switch
            {
                FrameSyncCatchUpDecisionKind.None => null,
                FrameSyncCatchUpDecisionKind.SendSnapshot => null, // 调用方应回退到全量快照路径
                FrameSyncCatchUpDecisionKind.SendInputs => decision.CatchUpRequest,
                _ => null
            };
        }

        /// <summary>
        /// 将从服务端收到的 CatchUp 帧输入应用到本地预测世界。
        /// </summary>
        public bool ApplyCatchUpPayload(in FrameSyncCatchUpPayload payload)
        {
            if (_runtime is null || payload.Inputs is null || payload.Inputs.Length == 0)
                return false;
            if (!_runtime.TryGetWorld(payload.WorldId, out var world))
                return false;

            try
            {
                var targetFrame = payload.StartFrame.Value + payload.Inputs.Length - 1;
                var finalFrame = WorldCatchUpDriver.CatchUpAndFeedSnapshots(
                    _runtime,
                    world,
                    lastTickedFrame: payload.StartFrame.Value - 1,
                    driveTargetFrame: targetFrame,
                    fixedDelta: 1.0f / 30f, // 使用默认 tick rate
                    stepsBudget: payload.Inputs.Length,
                    provider: null!,
                    maxSnapshotsPerStep: 0,
                    feed: _ => { });

                return finalFrame >= targetFrame;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 请求服务端 CatchUp 并消费结果。组合 DecideCatchUp → SendCatchUpRequest → ApplyCatchUpPayload。
        /// 返回 true 表示追帧成功或无需追帧；返回 false 表示服务端历史不完整，调用方应回退到全量快照路径。
        /// </summary>
        public bool TryCatchUp(WorldId worldId, FrameIndex authorityFrame, FrameIndex clientLastConfirmedFrame)
        {
            if (SendCatchUpRequest is null)
                return false;

            var request = DecideCatchUp(worldId, authorityFrame, clientLastConfirmedFrame);
            if (request is null)
                return true; // 无需追帧，视为成功

            var payload = SendCatchUpRequest(request.Value);
            if (payload is null)
                return false;

            return ApplyCatchUpPayload(payload.Value);
        }
    }
}
