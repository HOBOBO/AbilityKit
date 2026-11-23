using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 战斗中暂停/恢复演示控制。入口形式与 shooter 的断线演示相似，但恢复机制严格采用
    /// MOBA 锁步帧同步：补齐遗漏的权威输入帧，再由既有预测、回滚和重放管线追帧；
    /// 不请求或注入 shooter 状态同步使用的权威状态快照。
    /// <para>
    /// 暂停 = 冻结输入提交（<see cref="BattleContext.CanSubmitGameplayInput"/>）+ 关闭战斗数据面连接：
    /// 手动 Close 清掉连接的 open-request 标志，自动重连随之停止；本地不再收到服务器帧，
    /// 服务器房间与战斗继续推进。
    /// </para>
    /// 恢复时重绑结算流，从连续权威历史追到固定目标并完成重放后才开放输入。
    /// </summary>
    internal sealed class MobaBattleRecoveryRuntime
    {

        private enum RecoveryPhase
        {
            None = 0,
            ReconnectPending,
            CatchingUp,
        }

        private bool _isPaused;
        private int _pausedAtConfirmedFrame;
        private long _pausedAtUnixMs;
        private long _resumedAtUnixMs;
        private RecoveryPhase _recoveryPhase;
        private BattleLogicSession? _recoverySession;
        private int _catchUpTargetFrame;
        private int _latestLiveFrame;
        private int _catchUpPayloadCount;
        private int _catchUpFrameCount;
        private bool _catchUpRequestStarted;
        private bool _catchUpRequestCompleted;
        private string? _recoveryError;
        private string? _coldStartBattleId;

        public bool IsPaused => _isPaused;

        /// <summary>恢复流程是否在途（重连/补帧/追帧中）。恢复完成后回 false。</summary>
        public bool IsRecovering => _recoveryPhase != RecoveryPhase.None;

        public int PausedAtConfirmedFrame => _pausedAtConfirmedFrame;
        public long PausedAtUnixMs => _pausedAtUnixMs;
        public long ResumedAtUnixMs => _resumedAtUnixMs;
        public string RecoveryPhaseName => _recoveryPhase.ToString();
        public bool RecoveryTransportAuthenticated => _recoverySession?.NetworkTransport?.IsAuthenticated == true;
        public int RecoveryTargetFrame => _catchUpTargetFrame;
        public int LatestLiveFrame => _latestLiveFrame;
        public int CatchUpPayloadCount => _catchUpPayloadCount;
        public int CatchUpFrameCount => _catchUpFrameCount;
        public bool CatchUpRequestStarted => _catchUpRequestStarted;
        public bool CatchUpRequestCompleted => _catchUpRequestCompleted;
        public string? RecoveryError => _recoveryError;

        public void Reset()
        {
            _isPaused = false;
            _pausedAtConfirmedFrame = 0;
            _pausedAtUnixMs = 0;
            _resumedAtUnixMs = 0;
            _coldStartBattleId = null;
            ClearRecovery();
        }

        /// <summary>
        /// 暂停：断开战斗连接模拟断线。要求战斗上下文携带在线战斗会话。失败多为无会话（本地/未开局）。
        /// </summary>
        public bool Pause(BattleContext context)
        {
            var session = context?.Session;
            if (_isPaused || session == null || context.SettledFrames == null)
            {
                return false;
            }

            context.SetGameplayInputBlocked(BattleGameplayInputBlockReason.PauseRecovery, true);
            _pausedAtConfirmedFrame = TryGetConfirmedFrame(context, out var confirmed) ? confirmed : context.LastFrame;
            session.Disconnect();
            _isPaused = true;
            _pausedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _resumedAtUnixMs = 0;
            return true;
        }

        /// <summary>
        /// 冷启动同场重连：新本地世界刚按房间配置创建时冻结输入，从第 0 帧之前（-1）请求
        /// 完整的已保留锁步输入历史。该入口只允许每个 battleId 发起一次，避免恢复完成后重复追帧。
        /// </summary>
        public bool BeginColdStartRecovery(BattleContext context)
        {
            if (context?.Session == null || context.SettledFrames == null ||
                context.Plan.HostMode != BattleHostMode.GatewayRemote ||
                string.IsNullOrWhiteSpace(context.Plan.Gateway.BattleId) ||
                string.Equals(_coldStartBattleId, context.Plan.Gateway.BattleId, StringComparison.Ordinal))
            {
                return false;
            }

            _coldStartBattleId = context.Plan.Gateway.BattleId;
            context.SetGameplayInputBlocked(BattleGameplayInputBlockReason.PauseRecovery, true);
            _isPaused = true;
            _pausedAtConfirmedFrame = -1;
            _pausedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _resumedAtUnixMs = 0;
            return StartRecovery(context.Session);
        }

        /// <summary>
        /// 恢复：同一会话重连 + 帧历史补帧 + 追上后重开输入。只发起（Connect），
        /// 后续推进由 <see cref="TickRecovery"/> 在正式 Battle Session 生命周期中逐帧驱动。
        /// </summary>
        public bool Resume(BattleContext context)
        {
            if (!_isPaused || context == null)
            {
                return false;
            }

            var session = context.Session;
            if (session == null)
            {
                _recoveryError = "Battle session is unavailable for resume.";
                return false;
            }

            return StartRecovery(session);
        }

        private bool StartRecovery(BattleLogicSession session)
        {
            ClearRecovery();
            _recoverySession = session;
            _recoveryPhase = RecoveryPhase.ReconnectPending;
            _recoveryError = null;
            session.Connect();
            return true;
        }

        /// <summary>恢复流程逐帧推进：等待鉴权 → 请求补帧并重订阅 → 注入历史输入 → 回滚重放追上固定目标 → 重开输入。</summary>
        public void TickRecovery(BattleContext context)
        {
            if (_recoveryPhase == RecoveryPhase.None)
            {
                return;
            }

            var session = _recoverySession;
            var transport = session?.NetworkTransport;
            if (session == null || transport == null || context == null)
            {
                FailRecovery("Recovery session or transport disappeared.");
                return;
            }

            try
            {
                var settled = context.SettledFrames;
                if (settled == null) { FailRecovery("Settled recovery session is unavailable."); return; }
                if (settled.Fault != null) { FailRecovery(settled.Fault); return; }
                if (!transport.IsAuthenticated) return;
                _recoveryPhase = RecoveryPhase.CatchingUp;
                _catchUpTargetFrame = settled.RecoveryTarget;
                _catchUpRequestStarted = settled.CatchUpStarted;
                _catchUpRequestCompleted = settled.CanSubmit;
                _catchUpPayloadCount = settled.CatchUpPayloadCount;
                _catchUpFrameCount = settled.CatchUpFrameCount;
                _latestLiveFrame = settled.Receiver?.MaxReceivedFrame ?? 0;
                if (settled.CanSubmit && context.PredictionStats?.IsReplaying != true)
                    CompleteRecovery(context);
                return;
            }
            catch (Exception ex)
            {
                FailRecovery(ex.Message);
            }
        }

        private void CompleteRecovery(BattleContext context)
        {
            _recoveryPhase = RecoveryPhase.None;
            _recoverySession = null;
            _isPaused = false;
            _resumedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _recoveryError = null;
            context.SetGameplayInputBlocked(BattleGameplayInputBlockReason.PauseRecovery, false);
        }

        private void FailRecovery(string message)
        {
            _recoveryPhase = RecoveryPhase.None;
            _recoverySession = null;
            _recoveryError = message;
            // 恢复失败时保持暂停和输入冻结。MOBA lockstep 不允许退化为 Shooter 全量状态快照，
            // 必须由上层明确重试或退出战斗。
            _isPaused = true;
            Debug.LogError("[MobaBattlePauseController] Resume recovery failed: " + message);
        }

        private void ClearRecovery()
        {
            _recoveryPhase = RecoveryPhase.None;
            _recoverySession = null;
            _catchUpTargetFrame = 0;
            _latestLiveFrame = 0;
            _catchUpPayloadCount = 0;
            _catchUpFrameCount = 0;
            _catchUpRequestStarted = false;
            _catchUpRequestCompleted = false;
            _recoveryError = null;
        }

        private static bool TryGetConfirmedFrame(BattleContext context, out int confirmedFrame)
        {
            confirmedFrame = 0;
            var prediction = context.PredictionStats;
            return prediction != null &&
                   prediction.TryGetFrames(
                       new WorldId(context.Plan.World.WorldId),
                       out var confirmed,
                       out _) &&
                   (confirmedFrame = confirmed.Value) >= 0;
        }
    }

    // Compatibility entry for debug tools. BattleSessionRuntime owns the recovery state.
    public static class MobaBattlePauseController
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<BattleContext, MobaBattleRecoveryRuntime> Sessions =
            new Dictionary<BattleContext, MobaBattleRecoveryRuntime>();
        private static MobaBattleRecoveryRuntime _active;

        internal static void Register(BattleContext context, MobaBattleRecoveryRuntime recovery)
        {
            if (context == null || recovery == null) throw new ArgumentNullException(context == null ? nameof(context) : nameof(recovery));
            lock (Gate)
            {
                Sessions[context] = recovery;
                _active = recovery;
            }
        }

        internal static void Unregister(BattleContext context, MobaBattleRecoveryRuntime recovery)
        {
            if (context == null || recovery == null) return;
            lock (Gate)
            {
                if (Sessions.TryGetValue(context, out var current) && ReferenceEquals(current, recovery))
                    Sessions.Remove(context);
                if (ReferenceEquals(_active, recovery))
                {
                    _active = null;
                    foreach (var remaining in Sessions.Values) _active = remaining;
                }
            }
            recovery.Reset();
        }

        private static MobaBattleRecoveryRuntime Find(BattleContext context)
        {
            lock (Gate) return context != null && Sessions.TryGetValue(context, out var recovery) ? recovery : null;
        }

        internal static MobaBattleRecoveryRuntime For(BattleContext context) => Find(context);

        private static MobaBattleRecoveryRuntime Active
        {
            get { lock (Gate) return _active; }
        }

        public static bool IsPaused => Active?.IsPaused == true;
        public static bool IsRecovering => Active?.IsRecovering == true;
        public static int PausedAtConfirmedFrame => Active?.PausedAtConfirmedFrame ?? 0;
        public static long PausedAtUnixMs => Active?.PausedAtUnixMs ?? 0;
        public static long ResumedAtUnixMs => Active?.ResumedAtUnixMs ?? 0;
        public static string RecoveryPhaseName => Active?.RecoveryPhaseName ?? "None";
        public static bool RecoveryTransportAuthenticated => Active?.RecoveryTransportAuthenticated == true;
        public static int RecoveryTargetFrame => Active?.RecoveryTargetFrame ?? 0;
        public static int LatestLiveFrame => Active?.LatestLiveFrame ?? 0;
        public static int CatchUpPayloadCount => Active?.CatchUpPayloadCount ?? 0;
        public static int CatchUpFrameCount => Active?.CatchUpFrameCount ?? 0;
        public static bool CatchUpRequestStarted => Active?.CatchUpRequestStarted == true;
        public static bool CatchUpRequestCompleted => Active?.CatchUpRequestCompleted == true;
        public static string? RecoveryError => Active?.RecoveryError;

        public static void Reset() => Active?.Reset();
        public static bool Pause(BattleContext context) => Find(context)?.Pause(context) == true;
        public static bool Resume(BattleContext context) => Find(context)?.Resume(context) == true;
        public static bool BeginColdStartRecovery(BattleContext context) =>
            Find(context)?.BeginColdStartRecovery(context) == true;
        public static void TickRecovery(BattleContext context) => Find(context)?.TickRecovery(context);
    }
}
