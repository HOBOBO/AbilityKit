using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>Maps the enter-game wire response into platform-neutral presentation data.</summary>
    public static class BattleEnterGameSnapshotMapper
    {
        public static BattleEnterGameSnapshot Map(in EnterMobaGameRes response)
        {
            var players = MapPlayers(response.Players);
            var loadouts = MapLoadouts(response.PlayersLoadout);
            var payload = response.Payload == null
                ? System.Array.Empty<byte>()
                : (byte[])response.Payload.Clone();

            var hasPosition = MobaEnterGamePayloadCodec.TryDeserializePosition(
                response.OpCode,
                response.Payload,
                out var position);

            return new BattleEnterGameSnapshot(
                response.WorldId.Value,
                response.PlayerId.Value,
                response.LocalActorId,
                response.RandomSeed,
                response.TickRate,
                response.InputDelayFrames,
                players,
                response.OpCode,
                payload,
                loadouts,
                hasPosition,
                position.X,
                position.Y,
                position.Z);
        }

        public static BattleEnterGamePlayer[] MapPlayers(MobaPlayerEntry[] source)
        {
            if (source == null || source.Length == 0)
            {
                return System.Array.Empty<BattleEnterGamePlayer>();
            }

            var players = new BattleEnterGamePlayer[source.Length];
            for (var i = 0; i < players.Length; i++)
            {
                var player = source[i];
                players[i] = new BattleEnterGamePlayer(
                    player.PlayerId.Value,
                    player.TeamId,
                    player.HeroId,
                    player.SpawnIndex);
            }

            return players;
        }

        public static BattlePlayerLoadout[] MapLoadouts(MobaPlayerLoadout[] source)
        {
            if (source == null || source.Length == 0)
            {
                return System.Array.Empty<BattlePlayerLoadout>();
            }

            var loadouts = new BattlePlayerLoadout[source.Length];
            for (var i = 0; i < loadouts.Length; i++)
            {
                loadouts[i] = Map(in source[i]);
            }

            return loadouts;
        }

        public static BattlePlayerLoadout Map(in MobaPlayerLoadout source)
        {
            return new BattlePlayerLoadout(
                source.PlayerId.Value,
                source.TeamId,
                source.HeroId,
                source.AttributeTemplateId,
                source.Level,
                source.BasicAttackSkillId,
                source.SkillIds == null ? null : (int[])source.SkillIds.Clone(),
                source.SpawnIndex,
                source.UnitSubType,
                source.MainType,
                source.HasSpawnPosition,
                source.SpawnX,
                source.SpawnY,
                source.SpawnZ,
                source.BrainId,
                source.EnableBrainOnSpawn);
        }
    }
}
