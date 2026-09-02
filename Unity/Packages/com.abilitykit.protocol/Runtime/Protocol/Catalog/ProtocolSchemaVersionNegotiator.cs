#nullable enable

using System;

namespace AbilityKit.Protocol.Catalog
{
    /// <summary>Chooses the highest schema version supported by both protocol peers.</summary>
    public static class ProtocolSchemaVersionNegotiator
    {
        public static bool TrySelect(
            ProtocolMessageDefinition definition,
            int peerMinimumSchemaVersion,
            int peerMaximumSchemaVersion,
            out int selectedSchemaVersion)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            var minimum = Math.Max(definition.MinimumSchemaVersion, peerMinimumSchemaVersion);
            var maximum = Math.Min(definition.MaximumSchemaVersion, peerMaximumSchemaVersion);
            if (minimum <= 0 || maximum < minimum)
            {
                selectedSchemaVersion = 0;
                return false;
            }

            selectedSchemaVersion = maximum;
            return true;
        }
    }
}
