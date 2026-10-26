using System;
using System.Globalization;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Battle
{
    public sealed class NetworkIdCodec
    {
        public static NetworkIdCodec Numeric { get; } = new NetworkIdCodec(
            playerId => ParseUInt32(playerId.Value, nameof(playerId)),
            value => new PlayerId(value.ToString(CultureInfo.InvariantCulture)),
            worldId => ParseUInt64(worldId.Value, nameof(worldId)),
            value => new WorldId(value.ToString(CultureInfo.InvariantCulture)));

        private readonly Func<PlayerId, uint> _encodePlayerId;
        private readonly Func<uint, PlayerId> _decodePlayerId;
        private readonly Func<WorldId, ulong> _encodeWorldId;
        private readonly Func<ulong, WorldId> _decodeWorldId;

        public NetworkIdCodec(
            Func<PlayerId, uint> encodePlayerId,
            Func<uint, PlayerId> decodePlayerId,
            Func<WorldId, ulong> encodeWorldId,
            Func<ulong, WorldId> decodeWorldId)
        {
            _encodePlayerId = encodePlayerId ?? throw new ArgumentNullException(nameof(encodePlayerId));
            _decodePlayerId = decodePlayerId ?? throw new ArgumentNullException(nameof(decodePlayerId));
            _encodeWorldId = encodeWorldId ?? throw new ArgumentNullException(nameof(encodeWorldId));
            _decodeWorldId = decodeWorldId ?? throw new ArgumentNullException(nameof(decodeWorldId));
        }

        public uint Encode(PlayerId playerId) => _encodePlayerId(playerId);
        public PlayerId DecodePlayer(uint value) => _decodePlayerId(value);
        public ulong Encode(WorldId worldId) => _encodeWorldId(worldId);
        public WorldId DecodeWorld(ulong value) => _decodeWorldId(value);

        private static uint ParseUInt32(string value, string parameterName) =>
            uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : throw new FormatException($"{parameterName} '{value}' is not a valid unsigned integer.");

        private static ulong ParseUInt64(string value, string parameterName) =>
            ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : throw new FormatException($"{parameterName} '{value}' is not a valid unsigned integer.");
    }
}
