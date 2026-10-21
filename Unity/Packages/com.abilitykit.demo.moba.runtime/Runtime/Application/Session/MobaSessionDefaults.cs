using System;

namespace AbilityKit.Demo.Moba.Session
{
    public sealed class MobaSessionDefaults
    {
        public static readonly MobaSessionDefaults Default = new MobaSessionDefaults();

        public string WorldId { get; set; } = "1";
        public string WorldType { get; set; } = Worlds.Blueprints.MobaBattleWorldBlueprint.Type;
        public string MatchId { get; set; } = "session";
        public int MapId { get; set; } = 1;
        public int GameplayId { get; set; } = 1;
        public int TickRate { get; set; } = 30;
        public int InputDelayFrames { get; set; }
        public int RandomSeed { get; set; } = 1;
        public float SnapshotRotation { get; set; }

        public static MobaSessionDefaults OrDefault(MobaSessionDefaults defaults)
        {
            return defaults ?? Default;
        }

        public int ResolveMapId(int configuredMapId)
        {
            return configuredMapId > 0 ? configuredMapId : MapId;
        }

        public int ResolveGameplayId(int configuredGameplayId)
        {
            return configuredGameplayId > 0 ? configuredGameplayId : GameplayId;
        }

        public int ResolveTickRate(int configuredTickRate)
        {
            return configuredTickRate > 0 ? configuredTickRate : TickRate;
        }

        public int ResolveSeed(int seed)
        {
            return seed != 0 ? seed : RandomSeed;
        }

        public string ResolveWorldId(string worldId)
        {
            return string.IsNullOrEmpty(worldId) ? WorldId : worldId;
        }

        public string ResolveWorldType(string worldType)
        {
            return string.IsNullOrEmpty(worldType) ? WorldType : worldType;
        }

        public string ResolveMatchId(string matchId)
        {
            return string.IsNullOrEmpty(matchId) ? MatchId : matchId;
        }

    }
}
