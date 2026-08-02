using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync.CatchUp;
using AbilityKit.Ability.Host.Extensions.FrameSync.Spectator;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Transport;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// BattleSessionFeature 的观战模式集成 partial。
    ///
    /// 观战者通过独立的 INetworkClient 创建专用网关连接，不共享主战斗的 NetworkTransport。
    /// 仅接收 FramePushed 广播，不提交任何输入。
    ///
    /// 零侵入主流程——未调用 TryStartSpectating 时完全不影响正常战斗。
    ///
    /// 使用方式：
    /// <code>
    /// var gwClient = CreateGatewayClient(host, port);
    /// gwClient.Connect(host, port);
    /// // 等待 OnConnected 事件触发...
    /// feature.TryStartSpectating(gwClient, roomId, worldFactory);
    /// // 每帧 Update：
    /// feature.UpdateSpectatorWorld(stepsBudget: 10);
    /// // 获取渲染数据：
    /// var world = feature.SpectatorWorld;
    /// </code>
    /// </summary>
    public sealed partial class BattleSessionFeature
    {
        /// <summary>观战世界驱动器，null 表示未在观战模式。</summary>
        public SpectatorWorldDriver? SpectatorDriver { get; private set; }

        /// <summary>观战世界（渲染端可直接读取实体状态），null 表示未就绪。</summary>
        public IWorld? SpectatorWorld => SpectatorDriver?.World;

        /// <summary>是否处于观战模式。</summary>
        public bool IsSpectating => SpectatorDriver != null;

        private INetworkClient? _spectatorClient;
        private readonly CancellationTokenSource _spectatorCts = new();

        /// <summary>
        /// 启动观战模式。使用独立的网关客户端连接。
        /// worldFactory 应使用与正常客户端相同的世界蓝图以确保确定性。
        /// </summary>
        public async void TryStartSpectating(INetworkClient gatewayClient, ulong roomId, Func<IWorld> worldFactory)
        {
            if (IsSpectating) return;
            if (gatewayClient == null) throw new ArgumentNullException(nameof(gatewayClient));
            if (worldFactory == null) throw new ArgumentNullException(nameof(worldFactory));

            _spectatorClient = gatewayClient;

            // 注册推送处理器（FramePushed + CatchUpPayload）
            gatewayClient.OnServerPush += HandleSpectatorPush;

            try
            {
                // 1. 发送 SpectatorSubscribe 请求
                var subReqPayload = BitConverter.GetBytes(roomId);
                var subRes = await gatewayClient.SendRequestAsync(
                    OpCodes.SpectatorSubscribe,
                    subReqPayload,
                    _spectatorCts.Token);

                if (subRes == null || subRes.Length < 16)
                {
                    Debug.LogError("[BattleSessionFeature.Spectator] SpectatorSubscribe returned empty response.");
                    StopSpectating();
                    return;
                }

                var res = WireCustomBinary.DeserializeMetrics(new ArraySegment<byte>(subRes));
                var worldId = res.WorldId;
                var tickRate = res.TickRate;
                var currentFrame = res.CurrentFrame;

                Debug.Log($"[BattleSessionFeature.Spectator] Subscribed. WorldId={worldId} TickRate={tickRate} " +
                          $"CurrentFrame={currentFrame}");

                // 2. 创建观战世界驱动器
                var driver = new SpectatorWorldDriver();
                driver.Initialize(worldId, tickRate, worldFactory);
                SpectatorDriver = driver;

                // 3. 通过 CatchUp 追帧到当前帧
                if (currentFrame > 0)
                {
                    await RequestSpectatorCatchUpAsync(gatewayClient, roomId, worldId, currentFrame);
                }

                Debug.Log($"[BattleSessionFeature.Spectator] Spectator mode ready. RoomId={roomId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BattleSessionFeature.Spectator] Start failed: {ex.Message}");
                StopSpectating();
            }
        }

        private void HandleSpectatorPush(uint opCode, byte[] payload)
        {
            if (SpectatorDriver is not { IsReady: true }) return;

            try
            {
                switch (opCode)
                {
                    case OpCodes.FramePushed:
                        OnSpectatorFramePushed(payload);
                        break;
                    case OpCodes.CatchUpPayloadPush:
                        OnSpectatorCatchUpPayload(payload);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BattleSessionFeature.Spectator] Push handler error: {ex.Message}");
            }
        }

        private void OnSpectatorFramePushed(byte[] payload)
        {
            var evt = WireCustomBinary.DeserializeFramePushedPush(new ArraySegment<byte>(payload));

            var commands = new PlayerInputCommand[evt.Inputs?.Length ?? 0];
            if (evt.Inputs is { Length: > 0 })
            {
                for (var i = 0; i < evt.Inputs.Length; i++)
                {
                    var input = evt.Inputs[i];
                    commands[i] = new PlayerInputCommand(
                        new FrameIndex(evt.Frame),
                        new PlayerId(input.PlayerId.ToString()),
                        input.OpCode,
                        input.Payload ?? Array.Empty<byte>());
                }
            }

            SpectatorDriver!.FeedFrameInputs(evt.Frame, commands);
        }

        private void OnSpectatorCatchUpPayload(byte[] payload)
        {
            var push = WireCustomBinary.DeserializeCatchUpPayloadPush(new ArraySegment<byte>(payload));

            var allInputs = new PlayerInputCommand[push.Frames?.Length ?? 0][];
            for (var i = 0; i < allInputs.Length; i++)
            {
                var f = push.Frames[i];
                var cmds = new PlayerInputCommand[f.Inputs?.Length ?? 0];
                for (var j = 0; j < cmds.Length; j++)
                {
                    var inp = f.Inputs[j];
                    cmds[j] = new PlayerInputCommand(
                        new FrameIndex(f.Frame),
                        new PlayerId(inp.PlayerId.ToString()),
                        inp.OpCode,
                        inp.Payload ?? Array.Empty<byte>());
                }

                allInputs[i] = cmds;
            }

            var catchUpPayload = new FrameSyncCatchUpPayload(
                new WorldId(push.WorldId.ToString()),
                new FrameIndex(push.StartFrame),
                allInputs);

            SpectatorDriver!.FeedCatchUpPayload(catchUpPayload);
            Debug.Log($"[BattleSessionFeature.Spectator] CatchUp payload applied. Frames={allInputs.Length}");
        }

        private static async Task RequestSpectatorCatchUpAsync(
            INetworkClient client, ulong roomId, ulong worldId, int toFrame)
        {
            var wireReq = new WireCatchUpRequest(roomId, worldId, -1, toFrame);
            var payload = WireCustomBinary.Serialize(wireReq);

            await client.SendRequestAsync(
                OpCodes.CatchUpRequest,
                payload.Array ?? Array.Empty<byte>(),
                CancellationToken.None);

            Debug.Log($"[BattleSessionFeature.Spectator] CatchUp request sent. Range=[0, {toFrame}]");
        }

        /// <summary>停止观战模式。</summary>
        public void StopSpectating()
        {
            if (_spectatorClient != null)
            {
                _spectatorClient.OnServerPush -= HandleSpectatorPush;
            }

            _spectatorClient = null;
            SpectatorDriver = null;
            Debug.Log("[BattleSessionFeature.Spectator] Spectator mode stopped.");
        }

        /// <summary>
        /// 每帧调用，推进观战世界。建议在 Update 中调用。
        /// </summary>
        /// <param name="stepsBudget">单次调用最大推进帧数，防止单帧卡死。</param>
        public void UpdateSpectatorWorld(int stepsBudget = 10)
        {
            if (SpectatorDriver is not { IsReady: true }) return;

            var stepped = 0;
            while (stepped < stepsBudget && SpectatorDriver.TryTick())
            {
                stepped++;
            }
        }
    }
}
