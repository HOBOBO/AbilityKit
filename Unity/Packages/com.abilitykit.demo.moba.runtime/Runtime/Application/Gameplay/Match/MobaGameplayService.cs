using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Gameplay.Triggering;
using AbilityKit.Deterministic;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Demo.Moba.Gameplay
{
    public interface IMobaGameplayStartTransaction : IService
    {
        MobaGameplayPhase Phase { get; }
        bool IsRunning { get; }
        int CurrentGameplayId { get; }
        string MatchId { get; }
        string LastStartFailureReason { get; }
        bool TryPrepareStart(int gameplayId, out string error);
        bool TryPrepareStart(int gameplayId, string matchId, out string error);
        bool CommitPreparedStart();
        bool CommitPreparedStartDeferred();
        void PublishStarted();
        void CancelPreparedStart();
        void Reset();
    }

    [WorldService(typeof(IMobaGameplayStartTransaction), WorldLifetime.Scoped)]
    [WorldService(typeof(MobaGameplayService), WorldLifetime.Scoped)]
    public sealed class MobaGameplayService : IService, IMobaGameplayStartTransaction
    {
        [WorldInject(required: false)] private IFrameTime _frameTime = null;
        [WorldInject(required: false)] private IEventBus _eventBus = null;
        [WorldInject(required: false)] private IMobaGameplayEventSink _eventSink = null;
        [WorldInject(required: false)] private MobaGameplayConfigService _gameplayConfigs = null;
        [WorldInject(required: false)] private MobaGameplayTriggerBindingService _triggerBindings = null;
        [WorldInject(required: false)] private IWorldResolver _services = null;
        [WorldInject(required: false)] private MobaGameplayVariableService _variables = null;
 
        private MobaGameplayPhase _phase = MobaGameplayPhase.NotStarted;
        // Q32.32 raw 累计；float 属性/事件参数是表现边界单次换算视图。
        private long _elapsedRaw;
        private bool _timeExpired;
        private MobaGameplayResult _lastResult;
        private int _currentGameplayId;
        private GameplayMO _currentGameplay;
        private GameplayMO _preparedGameplay;
        private int _preparedStartFrame;
        private string _lastStartFailureReason;
        private string _matchId = string.Empty;
        private string _preparedMatchId;
        private bool _startedNotificationPending;
        private int _startNotificationFrame;
 
        public MobaGameplayPhase Phase => _phase;

        public bool IsRunning => _phase == MobaGameplayPhase.Running;

        public float ElapsedSeconds => AbilityKit.Deterministic.Fixed64.FromRaw(_elapsedRaw).ToSingle();
        public long ElapsedRaw => _elapsedRaw;
        public bool TimeExpired => _timeExpired;

        public MobaGameplayResult LastResult => _lastResult;

        public int CurrentGameplayId => _currentGameplayId;
        public string MatchId => _matchId;
        public int CurrentFrame => GetCurrentFrame();

        public GameplayMO CurrentGameplay => _currentGameplay;

        public MobaGameplayClockState GetClockState()
        {
            var durationMs = _currentGameplay?.DefaultDurationMs ?? 0;
            var remainingRaw = durationMs > 0
                ? Fixed64.FromRatio(durationMs, 1000).RawValue - _elapsedRaw
                : 0L;
            return new MobaGameplayClockState(
                _currentGameplayId,
                _phase,
                durationMs,
                ElapsedSeconds,
                Fixed64.FromRaw(Math.Max(0L, remainingRaw)).ToSingle(),
                Math.Min(durationMs, ToRemainingMilliseconds(Math.Max(0L, remainingRaw))),
                ToElapsedMilliseconds(_elapsedRaw));
        }

        private static int ToRemainingMilliseconds(long secondsRaw)
        {
            const long unit = 1L << 32;
            var wholeMilliseconds = (secondsRaw / unit) * 1000L;
            var fractionMilliseconds = ((secondsRaw % unit) * 1000L + unit - 1L) / unit;
            return (int)Math.Min(int.MaxValue, wholeMilliseconds + fractionMilliseconds);
        }

        private static int ToElapsedMilliseconds(long secondsRaw)
        {
            const long unit = 1L << 32;
            var wholeMilliseconds = (secondsRaw / unit) * 1000L;
            var fractionMilliseconds = ((secondsRaw % unit) * 1000L) / unit;
            return (int)Math.Min(int.MaxValue, wholeMilliseconds + fractionMilliseconds);
        }

        public string LastStartFailureReason => _lastStartFailureReason;
 
        public void StartDefault()
        {
            var gameplayConfigs = ResolveGameplayConfigService();
            if (gameplayConfigs == null)
            {
                Log.Error("[MobaGameplayService] default gameplay start failed: config service missing");
                return;
            }

            Start(gameplayConfigs.ResolveDefaultGameplayId());
        }
 
        public void Start()
        {
            StartDefault();
        }

        public void Start(int gameplayId)
        {
            if (!TryPrepareStart(gameplayId, out _))
            {
                return;
            }

            CommitPreparedStart();
        }

        public bool TryPrepareStart(int gameplayId, out string error)
        {
            return TryPrepareStart(gameplayId, _matchId, out error);
        }

        public bool TryPrepareStart(int gameplayId, string matchId, out string error)
        {
            error = null;
            if (_phase == MobaGameplayPhase.Running)
            {
                if (_currentGameplayId == gameplayId)
                {
                    return true;
                }

                error = $"gameplay is already running. currentGameplayId={_currentGameplayId}";
                _lastStartFailureReason = error;
                return false;
            }

            CancelPreparedStart();
            _lastStartFailureReason = null;
            try
            {
                var gameplay = ResolveGameplay(gameplayId);
                if (gameplay == null)
                {
                    error = BuildMissingGameplayConfigMessage(gameplayId);
                    _lastStartFailureReason = error;
                    Log.Error(error);
                    return false;
                }

                var frame = GetCurrentFrame();
                if (_triggerBindings == null)
                {
                    error = $"gameplay trigger binding service is missing. gameplayId={gameplayId}";
                    _lastStartFailureReason = error;
                    return false;
                }

                if (!_triggerBindings.Bind(gameplay))
                {
                    _triggerBindings.Unbind();
                    error = $"gameplay trigger binding failed. gameplayId={gameplayId}, detail={_triggerBindings.LastFailureReason}";
                    _lastStartFailureReason = error;
                    return false;
                }

                _preparedGameplay = gameplay;
                _preparedMatchId = matchId ?? string.Empty;
                _preparedStartFrame = frame;
                return true;
            }
            catch (Exception ex)
            {
                _triggerBindings?.Unbind();
                error = ex.Message;
                _lastStartFailureReason = error;
                Log.Exception(ex, $"[MobaGameplayService] gameplay preparation failed. gameplayId={gameplayId}");
                return false;
            }
        }

        public bool CommitPreparedStart()
        {
            if (!CommitPreparedStartDeferred()) return false;
            PublishStarted();
            return true;
        }

        public bool CommitPreparedStartDeferred()
        {
            if (_phase == MobaGameplayPhase.Running)
            {
                return true;
            }

            var gameplay = _preparedGameplay;
            if (gameplay == null)
            {
                _lastStartFailureReason = "gameplay start was not prepared";
                return false;
            }

            var frame = _preparedStartFrame;
            _preparedGameplay = null;
            _matchId = _preparedMatchId ?? string.Empty;
            _preparedMatchId = null;
            _preparedStartFrame = 0;
            _elapsedRaw = 0L;
            _timeExpired = false;
            _lastResult = default;
            _currentGameplayId = gameplay.Id;
            _currentGameplay = gameplay;
            _phase = MobaGameplayPhase.Running;
            _variables?.Clear();

            _startedNotificationPending = true;
            _startNotificationFrame = frame;

            return true;
        }

        public void PublishStarted()
        {
            if (!_startedNotificationPending || _phase != MobaGameplayPhase.Running) return;
            _startedNotificationPending = false;
            var frame = _startNotificationFrame;

            try
            {
                Publish(GameplayTriggerEvents.Started, new GameplayLifecycleEventArgs(frame, 0f, 0f, null));
                _eventSink?.OnGameplayStarted(frame);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[MobaGameplayService] gameplay started notification failed. gameplayId={_currentGameplayId}, frame={frame}");
            }

            if (MobaRuntimeLog.IsEnabled(
                    MobaRuntimeLogLevel.Info,
                    MobaRuntimeLogPurpose.Lifecycle))
            {
                MobaRuntimeLog.Info(
                    MobaRuntimeLogModule.Gameplay,
                    MobaRuntimeLogPurpose.Lifecycle,
                    nameof(MobaGameplayService),
                    $"Gameplay started. gameplayId={_currentGameplayId}, frame={frame}");
            }

        }

        public void CancelPreparedStart()
        {
            if (_preparedGameplay != null)
            {
                _triggerBindings?.Unbind();
            }

            _preparedGameplay = null;
            _preparedMatchId = null;
            _preparedStartFrame = 0;
        }

        public void Tick(float deltaTime)
        {
            if (_phase != MobaGameplayPhase.Running || deltaTime <= 0f)
            {
                return;
            }

            _elapsedRaw += AbilityKit.Core.Mathematics.DeterministicMathBridge.ToFixed(deltaTime).RawValue;
            Publish(GameplayTriggerEvents.Tick, new GameplayLifecycleEventArgs(GetCurrentFrame(), ElapsedSeconds, deltaTime, null));
            if (_phase == MobaGameplayPhase.Running && !_timeExpired && _currentGameplay.DefaultDurationMs > 0 &&
                _elapsedRaw >= Fixed64.FromRatio(_currentGameplay.DefaultDurationMs, 1000).RawValue)
            {
                _timeExpired = true;
                Publish(GameplayTriggerEvents.TimeExpired,
                    new GameplayLifecycleEventArgs(GetCurrentFrame(), ElapsedSeconds, 0f, "time_expired"));
            }
        }

        public void PublishScoreChanged(string reason, int teamId, double score)
        {
            if (!IsRunning) return;
            Publish(GameplayTriggerEvents.ScoreChanged,
                new GameplayLifecycleEventArgs(GetCurrentFrame(), ElapsedSeconds, 0f, reason,
                    teamId: teamId, score: score));
        }

        public bool End(string reason, int winTeamId = 0)
        {
            if (_phase != MobaGameplayPhase.Running || winTeamId < 0)
            {
                return false;
            }

            var result = new MobaGameplayResult(reason, winTeamId, GetCurrentFrame(), ElapsedSeconds);
            _phase = MobaGameplayPhase.Ending;
            _lastResult = result;

            try
            {
                Publish(GameplayTriggerEvents.Ended, new GameplayLifecycleEventArgs(result.EndFrame, result.ElapsedSeconds, 0f, result.Reason, result.WinTeamId));
                _eventSink?.OnGameplayEnded(in result);
            }
            finally
            {
                try { _triggerBindings?.Unbind(); }
                finally { _phase = MobaGameplayPhase.Ended; }
            }
            if (MobaRuntimeLog.IsEnabled(
                    MobaRuntimeLogLevel.Info,
                    MobaRuntimeLogPurpose.Lifecycle))
            {
                MobaRuntimeLog.Info(
                    MobaRuntimeLogModule.Gameplay,
                    MobaRuntimeLogPurpose.Lifecycle,
                    nameof(MobaGameplayService),
                    $"Gameplay ended. gameplayId={_currentGameplayId}, reason={result.Reason}, winTeamId={result.WinTeamId}, frame={result.EndFrame}, elapsed={result.ElapsedSeconds:F3}");
            }

            return true;
        }

        public void PublishGameplayEvent(string eventName, string reason = null)
        {
            if (_phase != MobaGameplayPhase.Running)
            {
                return;
            }

            Publish(eventName, new GameplayLifecycleEventArgs(GetCurrentFrame(), ElapsedSeconds, 0f, reason));
        }

        public void Reset()
        {
            CancelPreparedStart();
            _triggerBindings?.Unbind();
            _variables?.Clear();
            _elapsedRaw = 0L;
            _timeExpired = false;
            _lastResult = default;
            _currentGameplayId = 0;
            _matchId = string.Empty;
            _currentGameplay = null;
            _lastStartFailureReason = null;
            _startedNotificationPending = false;
            _startNotificationFrame = 0;
            _phase = MobaGameplayPhase.NotStarted;
        }

        public void Dispose()
        {
            Reset();
        }

        public void RestoreState(int gameplayId, MobaGameplayPhase phase, long elapsedRaw, bool timeExpired,
            string resultReason, int winTeamId, int endFrame, float resultElapsedSeconds)
        {
            if (elapsedRaw < 0 || !Enum.IsDefined(typeof(MobaGameplayPhase), phase) || phase == MobaGameplayPhase.Ending)
                throw new InvalidOperationException("Invalid gameplay rollback state.");
            var gameplay = gameplayId == 0 ? null : ResolveGameplay(gameplayId);
            if ((phase == MobaGameplayPhase.NotStarted) != (gameplayId == 0) ||
                (gameplayId != 0 && gameplay == null))
                throw new InvalidOperationException($"Cannot restore gameplay {gameplayId} in phase {phase}.");

            CancelPreparedStart();
            if (phase == MobaGameplayPhase.Running)
            {
                if (_triggerBindings == null)
                    throw new InvalidOperationException($"Cannot restore gameplay {gameplayId} without trigger bindings.");

                if (!_triggerBindings.Bind(gameplay))
                    throw new InvalidOperationException($"Cannot restore gameplay trigger bindings for {gameplayId}.");
            }
            else _triggerBindings?.Unbind();

            _phase = phase;
            _currentGameplayId = gameplayId;
            _currentGameplay = gameplay;
            _elapsedRaw = elapsedRaw;
            _timeExpired = timeExpired;
            _lastResult = phase == MobaGameplayPhase.Ended
                ? new MobaGameplayResult(resultReason, winTeamId, endFrame, resultElapsedSeconds)
                : default;
            _lastStartFailureReason = null;
            _startedNotificationPending = false;
            _startNotificationFrame = 0;
        }

        private GameplayMO ResolveGameplay(int gameplayId)
        {
            var gameplayConfigs = ResolveGameplayConfigService();
            if (gameplayConfigs != null && gameplayConfigs.TryGetGameplay(gameplayId, out var gameplay))
            {
                return gameplay;
            }

            if (_services != null
                && _services.TryResolve<MobaConfigDatabase>(out var configs)
                && configs != null
                && configs.TryGetGameplay(gameplayId, out gameplay))
            {
                return gameplay;
            }

            return null;
        }

        private string BuildMissingGameplayConfigMessage(int gameplayId)
        {
            var hasServices = _services != null;
            var gameplayConfigs = ResolveGameplayConfigService();
            var hasGameplayConfigs = gameplayConfigs != null;
            var gameplayConfigsHit = hasGameplayConfigs && gameplayConfigs.TryGetGameplay(gameplayId, out _);

            var hasDatabase = false;
            var databaseHit = false;
            var databaseVersion = 0L;
            string databaseResolveError = null;
            if (_services != null)
            {
                try
                {
                    var configs = _services.Resolve<MobaConfigDatabase>();
                    hasDatabase = configs != null;
                    if (configs != null)
                    {
                        databaseVersion = configs.Version;
                        databaseHit = configs.TryGetGameplay(gameplayId, out _);
                    }
                }
                catch (Exception ex)
                {
                    databaseResolveError = $"{ex.GetType().Name}: {ex.Message}";
                }
            }

            var databaseResolveErrorText = databaseResolveError ?? "<none>";
            return $"[MobaGameplayService] gameplay start failed: missing config. gameplayId={gameplayId}, hasServices={hasServices}, hasGameplayConfigService={hasGameplayConfigs}, gameplayConfigHit={gameplayConfigsHit}, hasDatabase={hasDatabase}, databaseHit={databaseHit}, databaseVersion={databaseVersion}, databaseResolveError={databaseResolveErrorText}";
        }

        private MobaGameplayConfigService ResolveGameplayConfigService()
        {
            if (_gameplayConfigs != null)
            {
                return _gameplayConfigs;
            }

            if (_services != null && _services.TryResolve<MobaGameplayConfigService>(out var gameplayConfigs))
            {
                _gameplayConfigs = gameplayConfigs;
            }

            return _gameplayConfigs;
        }

        private int GetCurrentFrame()
        {
            if (_frameTime != null) return _frameTime.Frame.Value;
            throw new InvalidOperationException("MobaGameplayService requires IFrameTime for gameplay lifecycle frames.");
        }

        private void Publish(string eventName, GameplayLifecycleEventArgs args)
        {
            if (_eventBus == null || string.IsNullOrEmpty(eventName))
            {
                return;
            }

            var key = GameplayTriggerEvents.GetKey(eventName);
            _eventBus.Publish(key, in args);
        }
    }

    public readonly struct MobaGameplayClockState
    {
        public int GameplayId { get; }
        public MobaGameplayPhase Phase { get; }
        public int DurationMilliseconds { get; }
        public float ElapsedSeconds { get; }
        public float RemainingSeconds { get; }
        public int RemainingMilliseconds { get; }
        public int ElapsedMilliseconds { get; }

        public MobaGameplayClockState(int gameplayId, MobaGameplayPhase phase,
            int durationMilliseconds, float elapsedSeconds, float remainingSeconds,
            int remainingMilliseconds, int elapsedMilliseconds)
        {
            GameplayId = gameplayId;
            Phase = phase;
            DurationMilliseconds = durationMilliseconds;
            ElapsedSeconds = elapsedSeconds;
            RemainingSeconds = remainingSeconds;
            RemainingMilliseconds = remainingMilliseconds;
            ElapsedMilliseconds = elapsedMilliseconds;
        }
    }
}
