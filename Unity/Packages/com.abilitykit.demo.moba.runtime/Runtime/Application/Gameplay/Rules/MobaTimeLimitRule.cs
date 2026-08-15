namespace AbilityKit.Demo.Moba.Gameplay
{
    public sealed class MobaTimeLimitRule : IMobaGameplayRule
    {
        private readonly long _durationRaw;
        private readonly string _onExpiredEvent;
        private MobaGameplayService _gameplay;
        // Q32.32 raw 累计（整数加法无漂移）。
        private long _elapsedRaw;
        private bool _expired;

        public MobaTimeLimitRule(float durationSeconds, string onExpiredEvent)
        {
            _durationRaw = durationSeconds > 0f
                ? AbilityKit.Core.Mathematics.DeterministicMathBridge.ToFixed(durationSeconds).RawValue
                : 0L;
            _onExpiredEvent = string.IsNullOrEmpty(onExpiredEvent) ? "gameplay.time_expired" : onExpiredEvent;
        }

        public string RuleId => "time_limit";

        public void Start(MobaGameplayService gameplay)
        {
            _gameplay = gameplay;
            _elapsedRaw = 0L;
            _expired = _durationRaw <= 0L;
        }

        public void Tick(float deltaTime)
        {
            if (_gameplay == null || _expired || deltaTime <= 0f)
            {
                return;
            }

            _elapsedRaw += AbilityKit.Core.Mathematics.DeterministicMathBridge.ToFixed(deltaTime).RawValue;
            if (_elapsedRaw < _durationRaw)
            {
                return;
            }

            _expired = true;
            _gameplay.PublishGameplayEvent(_onExpiredEvent, "time_expired");
        }

        public void Stop()
        {
            _gameplay = null;
            _expired = true;
        }
    }
}
