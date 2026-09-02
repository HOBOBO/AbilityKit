#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AbilityKit.Protocol.Catalog
{
    public enum ProtocolCatalogNegotiationFailureKind
    {
        None = 0,
        InvalidCatalog = 1,
        CatalogIdMismatch = 2,
        ProjectIdMismatch = 3,
        DomainMismatch = 4,
        MessageIdentityMismatch = 5,
        SchemaVersionMismatch = 6
    }

    public sealed class ProtocolCatalogNegotiationResult
    {
        internal ProtocolCatalogNegotiationResult(
            bool compatible,
            ProtocolCatalogNegotiationFailureKind failureKind,
            string failureMessage,
            IReadOnlyDictionary<string, int> selectedSchemaVersions,
            IReadOnlyList<string> incompatibleMessageIds)
        {
            IsCompatible = compatible;
            FailureKind = failureKind;
            FailureMessage = failureMessage ?? string.Empty;
            SelectedSchemaVersions = new ReadOnlyDictionary<string, int>(
                new Dictionary<string, int>(selectedSchemaVersions, StringComparer.Ordinal));
            IncompatibleMessageIds = new ReadOnlyCollection<string>(
                new List<string>(incompatibleMessageIds));
        }

        public bool IsCompatible { get; }
        public ProtocolCatalogNegotiationFailureKind FailureKind { get; }
        public string FailureMessage { get; }
        public IReadOnlyDictionary<string, int> SelectedSchemaVersions { get; }
        public IReadOnlyList<string> IncompatibleMessageIds { get; }

        public bool TryGetSchemaVersion(string messageId, out int schemaVersion) =>
            SelectedSchemaVersions.TryGetValue(messageId ?? string.Empty, out schemaVersion);
    }

    /// <summary>
    /// Negotiates the common schema versions for two catalog advertisements. Catalog revision is
    /// reported for diagnostics but is not itself a wire schema version. New message IDs may be
    /// added independently; a shared message must retain the same transport identity and have an
    /// overlapping version range.
    /// </summary>
    public static class ProtocolCatalogNegotiator
    {
        public static ProtocolCatalogNegotiationResult Negotiate(
            ProtocolCatalogDefinition local,
            ProtocolCatalogDefinition remote)
        {
            if (local == null) throw new ArgumentNullException(nameof(local));
            if (remote == null) throw new ArgumentNullException(nameof(remote));

            var localValidation = ProtocolCatalogValidator.Validate(local);
            if (!localValidation.IsValid)
                return Failure(ProtocolCatalogNegotiationFailureKind.InvalidCatalog,
                    $"Local catalog is invalid: {localValidation.Diagnostics[0]}.");
            var remoteValidation = ProtocolCatalogValidator.Validate(remote);
            if (!remoteValidation.IsValid)
                return Failure(ProtocolCatalogNegotiationFailureKind.InvalidCatalog,
                    $"Remote catalog is invalid: {remoteValidation.Diagnostics[0]}.");

            if (!string.Equals(local.CatalogId, remote.CatalogId, StringComparison.Ordinal))
                return Failure(ProtocolCatalogNegotiationFailureKind.CatalogIdMismatch,
                    $"Catalog ids differ: '{local.CatalogId}' and '{remote.CatalogId}'.");
            if (!string.Equals(local.ProjectId, remote.ProjectId, StringComparison.Ordinal))
                return Failure(ProtocolCatalogNegotiationFailureKind.ProjectIdMismatch,
                    $"Project ids differ: '{local.ProjectId}' and '{remote.ProjectId}'.");
            if (!string.Equals(local.Domain, remote.Domain, StringComparison.Ordinal))
                return Failure(ProtocolCatalogNegotiationFailureKind.DomainMismatch,
                    $"Catalog domains differ: '{local.Domain}' and '{remote.Domain}'.");

            var remoteMessages = new Dictionary<string, ProtocolMessageDefinition>(StringComparer.Ordinal);
            for (var i = 0; i < remote.Messages.Count; i++)
                remoteMessages[remote.Messages[i].Id] = remote.Messages[i];

            var selected = new Dictionary<string, int>(StringComparer.Ordinal);
            var incompatible = new List<string>();
            for (var i = 0; i < local.Messages.Count; i++)
            {
                var message = local.Messages[i];
                if (!remoteMessages.TryGetValue(message.Id, out var peer))
                    continue;

                if (message.OpCode != peer.OpCode ||
                    message.Direction != peer.Direction ||
                    message.Kind != peer.Kind ||
                    !string.Equals(message.Codec, peer.Codec, StringComparison.OrdinalIgnoreCase))
                    return Failure(
                        ProtocolCatalogNegotiationFailureKind.MessageIdentityMismatch,
                        $"Message '{message.Id}' has incompatible transport identity.");

                var minimum = Math.Max(message.MinimumSchemaVersion, peer.MinimumSchemaVersion);
                var maximum = Math.Min(message.MaximumSchemaVersion, peer.MaximumSchemaVersion);
                if (minimum > maximum)
                    incompatible.Add(message.Id);
                else
                    selected.Add(message.Id, maximum);
            }

            if (incompatible.Count != 0)
            {
                return new ProtocolCatalogNegotiationResult(
                    false,
                    ProtocolCatalogNegotiationFailureKind.SchemaVersionMismatch,
                    $"No shared schema version for: {string.Join(", ", incompatible)}.",
                    selected,
                    incompatible);
            }

            return new ProtocolCatalogNegotiationResult(
                true,
                ProtocolCatalogNegotiationFailureKind.None,
                string.Empty,
                selected,
                Array.Empty<string>());
        }

        private static ProtocolCatalogNegotiationResult Failure(
            ProtocolCatalogNegotiationFailureKind kind,
            string message) =>
            new ProtocolCatalogNegotiationResult(
                false,
                kind,
                message,
                new Dictionary<string, int>(StringComparer.Ordinal),
                Array.Empty<string>());
    }
}
