#nullable enable

using System.Collections.Generic;
using AbilityKit.Demo.Shooter.View.Hosting;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    /// <summary>
    /// 远程合成渲染批的姿态连续性探针：逐渲染帧跟踪每个实体（含己方角色）的位移，
    /// 统计"反向拉扯"（新位置沿上一帧移动方向倒退超过阈值）与前向跳变。
    /// 用于无头排查"实体被拉扯/己方角色抽搐"类问题——把体感症状变成可对比的数字。
    /// 挂载点：ShooterRemotePresentationFrameBuilder.Build（每个渲染帧的最终合成批）。
    /// </summary>
    public static class ShooterRemotePoseContinuityProbe
    {
        private const float BackwardThreshold = 0.08f;
        private const float ForwardSnapMultiple = 4f;
        private const float MinimumMotion = 0.0015f;

        private struct Track
        {
            public float LastX;
            public float LastY;
            public float LastDeltaX;
            public float LastDeltaY;
            public bool HasLast;
            public long Observations;
            public int BackwardJumps;
            public int ForwardSnaps;
            public float MaxBackwardDistance;
            public float MaxForwardJump;
        }

        private const long HeartbeatIntervalFrames = 600;

        private static readonly object Gate = new();
        private static readonly Dictionary<int, Track> EnemyTracks = new();
        private static Track _ownTrack;
        private static int _controlledPlayerId;
        private static long _frameCount;
        private static long _lastHeartbeatFrame;

        /// <summary>诊断汇总（每实体聚合）。线程安全快照后重置累计窗口。</summary>
        public sealed class Summary
        {
            public long Frames = -1;
            public long OwnObservations;
            public int OwnBackwardJumps;
            public float OwnMaxBackwardDistance;
            public int EnemiesTracked;
            public int EnemiesWithBackwardJumps;
            public int EnemyBackwardJumpsTotal;
            public float EnemyMaxBackwardDistance;
            public int EnemyForwardSnapsTotal;
        }

        public static void Configure(int controlledPlayerId)
        {
            lock (Gate)
            {
                _controlledPlayerId = controlledPlayerId;
            }
        }

        public static void Observe(in ShooterSnapshotViewBatch batch)
        {
            var transforms = batch.TransformChanges;
            if (transforms.Count == 0)
            {
                return;
            }

            lock (Gate)
            {
                _frameCount++;
                for (var i = 0; i < transforms.Count; i++)
                {
                    var transform = transforms[i];
                    var isOwn = transform.Key.Kind == ShooterViewEntityKind.Player &&
                        transform.Key.EntityId == _controlledPlayerId;
                    if (isOwn)
                    {
                        ObserveTrack(ref _ownTrack, transform.X, transform.Y, transform.Key.EntityId, isOwn: true);
                    }
                    else if (transform.Key.Kind == ShooterViewEntityKind.Enemy)
                    {
                        if (!EnemyTracks.TryGetValue(transform.Key.EntityId, out var track))
                        {
                            track = default;
                        }

                        ObserveTrack(ref track, transform.X, transform.Y, transform.Key.EntityId, isOwn: false);
                        EnemyTracks[transform.Key.EntityId] = track;
                    }
                }

                if (_frameCount - _lastHeartbeatFrame >= HeartbeatIntervalFrames)
                {
                    _lastHeartbeatFrame = _frameCount;
                    if (DiagnosticsLoggingEnabled)
                    {
                        var summary = Take();
                        LogLine(
                            $"[PoseContinuity] frames={summary.Frames} ownPos=({_ownTrack.LastX:F2},{_ownTrack.LastY:F2}) ownObs={summary.OwnObservations} ownBack={summary.OwnBackwardJumps} ownMaxBack={summary.OwnMaxBackwardDistance:F3} enemies={summary.EnemiesTracked} enemiesWithBack={summary.EnemiesWithBackwardJumps} enemyBackTotal={summary.EnemyBackwardJumpsTotal} enemyMaxBack={summary.EnemyMaxBackwardDistance:F3} enemySnaps={summary.EnemyForwardSnapsTotal} inputAttempts={_gatewayInputAttempts} inputFailures={_gatewayInputFailures} inputResyncFlags={_gatewayInputResyncFlags}");
                    }
                }
            }
        }

        private static void ObserveTrack(ref Track track, float x, float y, int entityId, bool isOwn)
        {
            track.Observations++;
            if (!track.HasLast)
            {
                track.LastX = x;
                track.LastY = y;
                track.HasLast = true;
                return;
            }

            var deltaX = x - track.LastX;
            var deltaY = y - track.LastY;
            var distance = SquaredRoot(deltaX * deltaX + deltaY * deltaY);
            var lastDistance = SquaredRoot(track.LastDeltaX * track.LastDeltaX + track.LastDeltaY * track.LastDeltaY);
            if (distance > MinimumMotion && lastDistance > MinimumMotion)
            {
                var dot = deltaX * track.LastDeltaX + deltaY * track.LastDeltaY;
                if (dot < 0f && distance > BackwardThreshold)
                {
                    track.BackwardJumps++;
                    track.MaxBackwardDistance = System.Math.Max(track.MaxBackwardDistance, distance);
                    if (distance > 0.15f)
                    {
                        LogLine(
                            $"[PoseContinuity.Event] frame={_frameCount} entity={(isOwn ? "own" : entityId.ToString())} backward={distance:F3}");
                    }
                }
                else if (distance > lastDistance * ForwardSnapMultiple && distance > BackwardThreshold * 3f)
                {
                    track.ForwardSnaps++;
                    track.MaxForwardJump = System.Math.Max(track.MaxForwardJump, distance);
                    if (distance > 0.45f)
                    {
                        LogLine(
                            $"[PoseContinuity.Event] frame={_frameCount} entity={(isOwn ? "own" : entityId.ToString())} forwardSnap={distance:F3}");
                    }
                }
            }

            track.LastDeltaX = deltaX;
            track.LastDeltaY = deltaY;
            track.LastX = x;
            track.LastY = y;
        }

        private static float SquaredRoot(float squared)
        {
            return (float)System.Math.Sqrt(squared);
        }

        /// <summary>
        /// 持续驾驶注入器：按固定节奏（每 45 渲染帧换向 + 停顿 + 开火）通过会话提交输入，
        /// 模拟真实游玩的连续移动/转向负载。经环境变量 ABILITYKIT_SHOOTER_SUSTAINED_INPUT=1 启用，
        /// 供无头复现"连续操作下的拉扯/抽搐"；默认关闭不影响正常运行。
        /// </summary>
        public static void TryDriveSustainedInput(ShooterClientSession session, int controlledPlayerId)
        {
            if (!_sustainedInputEnabled)
            {
                return;
            }

            lock (Gate)
            {
                _sustainedInputFrame++;
                // 每渲染帧提交（复刻真实编辑器会话的提交节奏：宿主循环逐帧调用输入泵，
                // 本机 RTT 下实际到达服务端的速率≈编辑器帧率，会触发服务端限速→
                // ShouldResync→恢复状态封锁本地输入，即"过一会就不能控制"）。
                var phase = (_sustainedInputFrame / 90) % 6;
                float moveX;
                float moveY;
                switch (phase)
                {
                    case 0: moveX = 1f; moveY = 0f; break;
                    case 1: moveX = 0f; moveY = 1f; break;
                    case 2: moveX = 0f; moveY = 0f; break;
                    case 3: moveX = -1f; moveY = 0.6f; break;
                    case 4: moveX = 0.7f; moveY = -0.7f; break;
                    default: moveX = 0f; moveY = 0f; break;
                }

                var angle = _sustainedInputFrame * 0.05f;
                var command = ShooterClientInputBuilder.CreateCommand(
                    controlledPlayerId,
                    moveX,
                    moveY,
                    System.MathF.Cos(angle),
                    System.MathF.Sin(angle),
                    _sustainedInputFrame % 20 < 10,
                    0);
                // 走真实网关提交路径（本地预测 + 网关上行 + 服务端准入守卫 + ShouldResync
                // 恢复联动），复刻正式宿主每渲染帧提交的节奏。
                var context = new ShooterGatewayBattleInputContext(
                    "probe-session",
                    "probe-battle",
                    session.Presentation.ViewModel.WorldId,
                    session.GatewayInputFrame,
                    (uint)controlledPlayerId);
                _gatewayInputAttempts++;
                _ = SubmitAndLogAsync(session, context, command);
            }
        }

        private static void LogLine(string message)
        {
#if UNITY_5_3_OR_NEWER
            UnityEngine.Debug.Log(message);
#else
            System.Console.WriteLine(message);
#endif
        }

        // 姿态诊断日志默认关闭（避免刷屏影响性能）；排查时设 ABILITYKIT_SHOOTER_POSE_DIAGNOSTICS=1。
        private static readonly bool DiagnosticsLoggingEnabled =
            string.Equals(
                System.Environment.GetEnvironmentVariable("ABILITYKIT_SHOOTER_POSE_DIAGNOSTICS"),
                "1",
                System.StringComparison.OrdinalIgnoreCase);

        private static bool _sustainedInputEnabled =
            string.Equals(
                System.Environment.GetEnvironmentVariable("ABILITYKIT_SHOOTER_SUSTAINED_INPUT"),
                "1",
                System.StringComparison.OrdinalIgnoreCase);
        private static long _sustainedInputFrame;
        private static long _gatewayInputAttempts;
        private static long _gatewayInputFailures;
        private static long _gatewayInputResyncFlags;

        private static async System.Threading.Tasks.Task SubmitAndLogAsync(
            ShooterClientSession session,
            ShooterGatewayBattleInputContext context,
            ShooterPlayerCommand command)
        {
            try
            {
                var result = await session.SubmitLocalInputToGatewayAsync(context, command);
                if (!result.Remote.Success)
                {
                    _gatewayInputFailures++;
                    if (result.Remote.ShouldResync)
                    {
                        _gatewayInputResyncFlags++;
                    }

                    LogLine(
                        $"[PoseContinuity.Input] submit rejected status={result.Remote.Status} shouldResync={result.Remote.ShouldResync} attempts={_gatewayInputAttempts}");
                }
            }
            catch (System.Exception exception)
            {
                _gatewayInputFailures++;
                LogLine($"[PoseContinuity.Input] submit threw {exception.GetType().Name}: {exception.Message}");
            }
        }

        public static Summary Take()
        {
            lock (Gate)
            {
                var summary = new Summary
                {
                    Frames = _frameCount,
                    OwnObservations = _ownTrack.Observations,
                    OwnBackwardJumps = _ownTrack.BackwardJumps,
                    OwnMaxBackwardDistance = _ownTrack.MaxBackwardDistance
                };
                foreach (var track in EnemyTracks.Values)
                {
                    summary.EnemiesTracked++;
                    summary.EnemyBackwardJumpsTotal += track.BackwardJumps;
                    summary.EnemyForwardSnapsTotal += track.ForwardSnaps;
                    if (track.BackwardJumps > 0)
                    {
                        summary.EnemiesWithBackwardJumps++;
                    }

                    summary.EnemyMaxBackwardDistance = System.Math.Max(summary.EnemyMaxBackwardDistance, track.MaxBackwardDistance);
                }

                return summary;
            }
        }

        public static void Reset()
        {
            lock (Gate)
            {
                EnemyTracks.Clear();
                _ownTrack = default;
                _frameCount = 0;
            }
        }
    }
}
