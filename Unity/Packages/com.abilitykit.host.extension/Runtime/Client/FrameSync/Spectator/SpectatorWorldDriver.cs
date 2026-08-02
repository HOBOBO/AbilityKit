using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync.CatchUp;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Logging;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Ability.Host.Extensions.FrameSync.Spectator
{
    /// <summary>
    /// 观战世界驱动器。接收服务端 FramePushed 帧输入，驱动本地确定性世界。
    /// 不提交输入、不做预测、不对账。通过 CatchUp 追帧后跟随实时帧推进。
    ///
    /// 使用方式：
    /// 1. Initialize(worldId, tickRate, worldFactory)
    /// 2. 调用 RequestCatchUpAsync 获取追帧数据并 FeedCatchUpPayload 快进到当前帧
    /// 3. 对每个收到的 FramePushed 事件调用 FeedFrameInputs
    /// 4. 每帧调用 TryTick 推进世界并从返回的 IWorld 提取渲染状态
    /// </summary>
    public sealed class SpectatorWorldDriver
    {
        private IWorld? _world;
        private IWorldInputSink? _inputSink;
        private ulong _worldId;
        private int _tickRate = 30;
        private int _currentFrame;
        private float _fixedDelta;
        private bool _initialized;

        /// <summary>用于追帧的 jitter buffer，延迟设为 0（观战者不需要预测窗口）。</summary>
        private FrameJitterBuffer<PlayerInputCommand[]>? _jitterBuffer;

        /// <summary>观战世界当前帧号。</summary>
        public int CurrentFrame => _currentFrame;

        /// <summary>观战世界是否已就绪。</summary>
        public bool IsReady => _initialized && _world != null;

        /// <summary>当前观战世界实例（就绪后非 null）。</summary>
        public IWorld? World => _world;

        /// <summary>
        /// 初始化观战世界。worldFactory 应使用与正常客户端相同的世界蓝图以确保确定性。
        /// </summary>
        public void Initialize(ulong worldId, int tickRate, Func<IWorld> worldFactory)
        {
            if (worldFactory == null) throw new ArgumentNullException(nameof(worldFactory));

            _worldId = worldId;
            _tickRate = tickRate > 0 ? tickRate : 30;
            _fixedDelta = 1.0f / _tickRate;
            _currentFrame = -1;

            _world = worldFactory();
            _inputSink = _world.Services.TryResolve<IWorldInputSink>(out var sink) ? sink : null;

            // 观战者 jitter buffer：无延迟（delay=0），缺失帧用空数组填充
            _jitterBuffer = new FrameJitterBuffer<PlayerInputCommand[]>(
                delayFrames: 0,
                MissingFrameMode.FillDefault,
                missingFrameFactory: () => Array.Empty<PlayerInputCommand>());

            _initialized = true;
            Log.Info(
                $"[SpectatorWorldDriver] Initialized. WorldId={worldId} TickRate={tickRate}");
        }

        /// <summary>
        /// 从服务端 FramePushed 事件投喂一帧输入到 jitter buffer。
        /// </summary>
        public void FeedFrameInputs(int frame, PlayerInputCommand[] inputs)
        {
            if (!_initialized || _jitterBuffer == null) return;

            _jitterBuffer.Add(frame, inputs ?? Array.Empty<PlayerInputCommand>());
        }

        /// <summary>
        /// 投喂 CatchUp 批量追帧数据，快速追赶服务端当前帧。
        /// </summary>
        public void FeedCatchUpPayload(in FrameSyncCatchUpPayload payload)
        {
            if (!_initialized || _jitterBuffer == null) return;

            if (payload.Inputs is { Length: > 0 })
            {
                var startFrame = payload.StartFrame.Value;
                for (var i = 0; i < payload.Inputs.Length; i++)
                {
                    _jitterBuffer.Add(startFrame + i, payload.Inputs[i]);
                }
            }
        }

        /// <summary>
        /// 尝试推进一帧。返回 true 表示成功推进（含空帧追赶），false 表示无待消费帧。
        /// </summary>
        public bool TryTick()
        {
            if (!_initialized || _world == null || _jitterBuffer == null)
                return false;

            var nextFrame = _currentFrame + 1;

            if (!_jitterBuffer.TryConsume(nextFrame, out var commands))
                return false;

            // 提交输入到世界
            if (_inputSink != null && commands is { Length: > 0 })
            {
                _inputSink.Submit(new FrameIndex(nextFrame), commands);
            }

            // 推进世界
            _world.Tick(_fixedDelta);
            _currentFrame = nextFrame;
            return true;
        }

        /// <summary>
        /// 批量追帧到目标帧（调用方已通过 FeedCatchUpPayload 或 FeedFrameInputs 投喂输入）。
        /// 受 stepsBudget 限制，防止单帧卡死。
        /// </summary>
        /// <param name="targetFrame">目标帧号。</param>
        /// <param name="stepsBudget">单次调用最大推进帧数。</param>
        /// <returns>实际推进到的帧号。</returns>
        public int CatchUpTo(int targetFrame, int stepsBudget = 600)
        {
            if (!_initialized || _world == null)
                return _currentFrame;

            var steps = 0;
            while (steps < stepsBudget && _currentFrame < targetFrame)
            {
                if (!TryTick())
                    break;

                steps++;
            }

            Log.Info(
                $"[SpectatorWorldDriver] CatchUp complete. TargetFrame={targetFrame} " +
                $"ReachedFrame={_currentFrame} Steps={steps}");

            return _currentFrame;
        }
    }
}
