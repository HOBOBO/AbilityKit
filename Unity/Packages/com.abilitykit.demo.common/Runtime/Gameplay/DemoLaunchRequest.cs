#nullable enable

using System;

namespace AbilityKit.Demo.Common.Gameplay
{
    public enum DemoGameplayId
    {
        Moba = 0,
        Shooter = 1
    }

    public enum DemoLaunchMode
    {
        Local = 0,
        Multiplayer = 1
    }

    public readonly struct DemoLaunchRequest
    {
        public DemoLaunchRequest(
            DemoGameplayId gameplay,
            DemoLaunchMode mode,
            string? profileId = null)
        {
            if (!Enum.IsDefined(typeof(DemoGameplayId), gameplay))
            {
                throw new ArgumentOutOfRangeException(nameof(gameplay), gameplay, "Unknown demo gameplay.");
            }

            if (!Enum.IsDefined(typeof(DemoLaunchMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown demo launch mode.");
            }

            Gameplay = gameplay;
            Mode = mode;
            ProfileId = profileId?.Trim() ?? string.Empty;
        }

        public DemoGameplayId Gameplay { get; }
        public DemoLaunchMode Mode { get; }
        public string ProfileId { get; }
    }
}
