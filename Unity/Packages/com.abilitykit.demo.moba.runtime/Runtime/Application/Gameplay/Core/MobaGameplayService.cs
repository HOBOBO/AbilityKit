using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Gameplay.Triggering;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Demo.Moba.Gameplay
{
    [WorldService(typeof(MobaGameplayService), WorldLifetime.Scoped)]
    public sealed class MobaGameplayService : IService
    {
        [WorldInject(required: false)] private IFrameTime _frameTime = null;
        [WorldInject(required: false)] private IEventBus _eventBus = null;
        [WorldInject(required: false)] private IMobaGameplayEventSink _eventSink = null;
        [WorldInject(required: false)] private MobaGameplayConfigService _gameplayConfigs = null;
        [WorldInject(required: false)] private MobaGameplayTriggerBindingService _triggerBindings = null;
        [WorldInject(required: false)] private IWorldResolver _services = null;
 
        private MobaGameplayPhase _phase = MobaGameplayPhase.NotStarted;
        // Q32.32 raw 累计；float 属性/事件参数是表现边界单次换算视图。
        private long _elapsedRaw;
        private MobaGameplayResult _lastResult;
        private int _currentGameplayId;
        private GameplayMO _currentGameplay;
        private GameplayMO _preparedGameplay;
        private int _preparedStartFrame;
        private string _lastStartFailureReason;
 
        public MobaGameplayPhase Phase => _phase;

        public bool IsRunning => _phase == MobaGameplayPhase.Running;

        public float ElapsedSeconds => AbilityKit.Deterministic.Fixed64.FromRaw(_elapsedRaw).ToSingle();

        public MobaGameplayResult LastResult => _lastResult;

        public int CurrentGameplayId => _currentGameplayId;

        public GameplayMO CurrentGameplay => _currentGameplay;

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
                if (_triggerBindings != null && !_triggerBindings.Bind(gameplay))
                {
                    _triggerBindings.Unbind();
                    error = $"gameplay trigger binding failed. gameplayId={gameplayId}";
                    _lastStartFailureReason = error;
                    return false;
                }

                _preparedGameplay = gameplay;
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
            _preparedStartFrame = 0;
            _elapsedRaw = 0L;
            _lastResult = default;
            _currentGameplayId = gameplay.Id;
            _currentGameplay = gameplay;
            _phase = MobaGameplayPhase.Running;

            try
            {
                Publish(GameplayTriggerEvents.Started, new GameplayLifecycleEventArgs(frame, 0f, 0f, null));
                _eventSink?.OnGameplayStarted(frame);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[MobaGameplayService] gameplay started notification failed. gameplayId={gameplay.Id}, frame={frame}");
            }

            Log.Info($"[MobaGameplayService] gameplay started. gameplayId={gameplay.Id}, frame={frame}");
            return true;
        }

        public void CancelPreparedStart()
        {
            if (_preparedGameplay != null)
            {
                _triggerBindings?.Unbind();
            }

            _preparedGameplay = null;
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
        }

        public bool End(string reason, int winTeamId = 0)
        {
            if (_phase == MobaGameplayPhase.Ended || _phase == MobaGameplayPhase.Ending)
            {
                return false;
            }

            _phase = MobaGameplayPhase.Ending;
            var result = new MobaGameplayResult(reason, winTeamId, GetCurrentFrame(), ElapsedSeconds);
            _lastResult = result;

            Publish(GameplayTriggerEvents.Ended, new GameplayLifecycleEventArgs(result.EndFrame, result.ElapsedSeconds, 0f, result.Reason, result.WinTeamId));
            _eventSink?.OnGameplayEnded(in result);

            _triggerBindings?.Unbind();
            _phase = MobaGameplayPhase.Ended;
            Log.Info($"[MobaGameplayService] gameplay ended. gameplayId={_currentGameplayId}, reason={result.Reason}, winTeamId={result.WinTeamId}, frame={result.EndFrame}, elapsed={result.ElapsedSeconds:F3}");
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
            _elapsedRaw = 0L;
            _lastResult = default;
            _currentGameplayId = 0;
            _currentGameplay = null;
            _lastStartFailureReason = null;
            _phase = MobaGameplayPhase.NotStarted;
        }

        public void Dispose()
        {
            Reset();
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
}
