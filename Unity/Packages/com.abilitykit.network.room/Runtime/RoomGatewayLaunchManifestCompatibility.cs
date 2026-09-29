#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Network.Room
{
    public static class RoomGatewayLaunchManifestCompatibility
    {
        public static void Require(RoomGatewaySnapshot room, int version,
            IEnumerable<string> references, IReadOnlyDictionary<string, string>? metadata = null)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            if (room.LaunchManifestVersion != version ||
                !string.Equals(room.LaunchManifestHash, RoomLaunchManifestHash.Compute(references, metadata),
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Room launch manifest is incompatible with the installed gameplay rules or assets.");
        }

        public static string ComputeHash(IEnumerable<string> references,
            IReadOnlyDictionary<string, string>? metadata = null) =>
            RoomLaunchManifestHash.Compute(references, metadata);
    }
}
