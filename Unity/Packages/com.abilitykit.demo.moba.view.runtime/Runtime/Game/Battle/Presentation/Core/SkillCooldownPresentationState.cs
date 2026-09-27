using System;
using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Platform-neutral projection of a skill snapshot onto local presentation time.
    /// </summary>
    public readonly struct SkillCooldownPresentationState
    {
        private readonly SkillAvailabilityState _availability;
        private readonly int _disableReason;
        private readonly float _totalSeconds;
        private readonly float _remainingAtReceiveSeconds;
        private readonly float _localReceiveTimeSeconds;

        private SkillCooldownPresentationState(
            SkillAvailabilityState availability,
            int disableReason,
            float totalSeconds,
            float remainingAtReceiveSeconds,
            float localReceiveTimeSeconds)
        {
            _availability = availability;
            _disableReason = disableReason;
            _totalSeconds = totalSeconds;
            _remainingAtReceiveSeconds = remainingAtReceiveSeconds;
            _localReceiveTimeSeconds = localReceiveTimeSeconds;
        }

        public bool IsDisabled =>
            _availability == SkillAvailabilityState.Disabled && _disableReason != 0;

        public static SkillCooldownPresentationState FromSnapshot(
            in SkillStateData state,
            float localReceiveTimeSeconds)
        {
            var remainingMs = state.CooldownRemainingMs;
            if (state.Availability == SkillAvailabilityState.CoolingDown &&
                state.CooldownEndTimeMs > state.ServerTimeMs)
            {
                remainingMs = Math.Max(
                    remainingMs,
                    ClampToInt(state.CooldownEndTimeMs - state.ServerTimeMs));
            }

            remainingMs = Math.Max(0, remainingMs);
            var totalMs = Math.Max(Math.Max(0, state.CooldownTotalMs), remainingMs);
            return new SkillCooldownPresentationState(
                state.Availability,
                state.DisableReason,
                totalMs / 1000f,
                remainingMs / 1000f,
                localReceiveTimeSeconds);
        }

        public bool BlocksInput(float nowSeconds)
        {
            return IsDisabled || GetRemainingSeconds(nowSeconds) > 0f;
        }

        public SkillCooldownDisplayState GetDisplayState(float nowSeconds)
        {
            if (IsDisabled)
            {
                return new SkillCooldownDisplayState(
                    showOverlay: true,
                    showCountdown: false,
                    remainingSeconds: 0f,
                    fillAmount: 1f,
                    isDisabled: true);
            }

            var remainingSeconds = GetRemainingSeconds(nowSeconds);
            if (remainingSeconds <= 0f)
            {
                return default;
            }

            var fillAmount = _totalSeconds > 0f
                ? Clamp01(remainingSeconds / _totalSeconds)
                : 1f;
            return new SkillCooldownDisplayState(
                showOverlay: true,
                showCountdown: true,
                remainingSeconds: remainingSeconds,
                fillAmount: fillAmount,
                isDisabled: false);
        }

        public float GetRemainingSeconds(float nowSeconds)
        {
            if (_availability != SkillAvailabilityState.CoolingDown) return 0f;

            var elapsed = Math.Max(0f, nowSeconds - _localReceiveTimeSeconds);
            return Math.Max(0f, _remainingAtReceiveSeconds - elapsed);
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f) return 0f;
            return value >= 1f ? 1f : value;
        }

        private static int ClampToInt(long value)
        {
            if (value <= 0L) return 0;
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }
    }

    public readonly struct SkillCooldownDisplayState
    {
        public bool ShowOverlay { get; }
        public bool ShowCountdown { get; }
        public float RemainingSeconds { get; }
        public float FillAmount { get; }
        public bool IsDisabled { get; }

        public SkillCooldownDisplayState(
            bool showOverlay,
            bool showCountdown,
            float remainingSeconds,
            float fillAmount,
            bool isDisabled)
        {
            ShowOverlay = showOverlay;
            ShowCountdown = showCountdown;
            RemainingSeconds = remainingSeconds;
            FillAmount = fillAmount;
            IsDisabled = isDisabled;
        }
    }
}
