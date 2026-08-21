#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Protocol.Catalog
{
    public enum ProtocolCatalogDiagnosticSeverity
    {
        Warning = 0,
        Error = 1
    }

    public sealed class ProtocolCatalogDiagnostic
    {
        public ProtocolCatalogDiagnostic(
            ProtocolCatalogDiagnosticSeverity severity,
            string code,
            string catalogId,
            string messageId,
            string detail)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            CatalogId = catalogId ?? string.Empty;
            MessageId = messageId ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public ProtocolCatalogDiagnosticSeverity Severity { get; }
        public string Code { get; }
        public string CatalogId { get; }
        public string MessageId { get; }
        public string Detail { get; }

        public override string ToString() =>
            $"{Severity} {Code} catalog={CatalogId} message={MessageId}: {Detail}";
    }

    public sealed class ProtocolCatalogValidationResult
    {
        internal ProtocolCatalogValidationResult(IReadOnlyList<ProtocolCatalogDiagnostic> diagnostics)
        {
            Diagnostics = diagnostics;
        }

        public IReadOnlyList<ProtocolCatalogDiagnostic> Diagnostics { get; }

        public bool IsValid
        {
            get
            {
                for (var i = 0; i < Diagnostics.Count; i++)
                {
                    if (Diagnostics[i].Severity == ProtocolCatalogDiagnosticSeverity.Error)
                        return false;
                }

                return true;
            }
        }
    }

    public static class ProtocolCatalogValidator
    {
        public static ProtocolCatalogValidationResult Validate(ProtocolCatalogDefinition? catalog)
        {
            var diagnostics = new List<ProtocolCatalogDiagnostic>();
            if (catalog == null)
            {
                diagnostics.Add(Error("AKP000", string.Empty, string.Empty, "Catalog is null."));
                return new ProtocolCatalogValidationResult(diagnostics);
            }

            ValidateHeader(catalog, diagnostics);
            ValidateMessages(catalog, diagnostics);
            return new ProtocolCatalogValidationResult(diagnostics);
        }

        public static ProtocolCatalogValidationResult Validate(
            IReadOnlyList<ProtocolCatalogDefinition?>? catalogs)
        {
            var diagnostics = new List<ProtocolCatalogDiagnostic>();
            var catalogIds = new HashSet<string>(StringComparer.Ordinal);

            if (catalogs == null)
            {
                diagnostics.Add(Error("AKP000", string.Empty, string.Empty, "Catalog collection is null."));
                return new ProtocolCatalogValidationResult(diagnostics);
            }

            for (var i = 0; i < catalogs.Count; i++)
            {
                var catalog = catalogs[i];
                if (catalog == null)
                {
                    diagnostics.Add(Error("AKP000", string.Empty, string.Empty, $"Catalog at index {i} is null."));
                    continue;
                }

                if (!catalogIds.Add(catalog.CatalogId))
                {
                    diagnostics.Add(Error(
                        "AKP002",
                        catalog.CatalogId,
                        string.Empty,
                        "Catalog id must be unique across all projects."));
                }

                var result = Validate(catalog);
                for (var d = 0; d < result.Diagnostics.Count; d++)
                    diagnostics.Add(result.Diagnostics[d]);
            }

            return new ProtocolCatalogValidationResult(diagnostics);
        }

        private static void ValidateHeader(
            ProtocolCatalogDefinition catalog,
            ICollection<ProtocolCatalogDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(catalog.CatalogId))
                diagnostics.Add(Error("AKP001", catalog.CatalogId, string.Empty, "Catalog id is required."));
            if (string.IsNullOrWhiteSpace(catalog.ProjectId))
                diagnostics.Add(Error("AKP003", catalog.CatalogId, string.Empty, "Project id is required."));
            if (string.IsNullOrWhiteSpace(catalog.Domain))
                diagnostics.Add(Error("AKP004", catalog.CatalogId, string.Empty, "Domain is required."));
            if (catalog.Revision <= 0)
                diagnostics.Add(Error("AKP005", catalog.CatalogId, string.Empty, "Revision must be greater than zero."));
            if (string.IsNullOrWhiteSpace(catalog.DefaultCodec))
                diagnostics.Add(Error("AKP006", catalog.CatalogId, string.Empty, "Default codec is required."));
        }

        private static void ValidateMessages(
            ProtocolCatalogDefinition catalog,
            ICollection<ProtocolCatalogDiagnostic> diagnostics)
        {
            var ids = new Dictionary<string, ProtocolMessageDefinition>(StringComparer.Ordinal);
            var keys = new HashSet<ProtocolMessageKey>();

            for (var i = 0; i < catalog.Messages.Count; i++)
            {
                var message = catalog.Messages[i];
                if (message == null)
                {
                    diagnostics.Add(Error("AKP010", catalog.CatalogId, string.Empty, $"Message at index {i} is null."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(message.Id))
                    diagnostics.Add(Error("AKP011", catalog.CatalogId, message.Id, "Message id is required."));
                else if (!ids.TryAdd(message.Id, message))
                    diagnostics.Add(Error("AKP012", catalog.CatalogId, message.Id, "Message id must be unique within a catalog."));

                if (message.OpCode == 0)
                    diagnostics.Add(Error("AKP013", catalog.CatalogId, message.Id, "OpCode zero is reserved."));
                if (string.IsNullOrWhiteSpace(message.PayloadType))
                    diagnostics.Add(Error("AKP014", catalog.CatalogId, message.Id, "Payload type is required."));
                if (string.IsNullOrWhiteSpace(message.Codec))
                    diagnostics.Add(Error("AKP015", catalog.CatalogId, message.Id, "Codec is required."));
                if (message.MinimumSchemaVersion <= 0 ||
                    message.MaximumSchemaVersion < message.MinimumSchemaVersion)
                {
                    diagnostics.Add(Error("AKP016", catalog.CatalogId, message.Id, "Schema version range is invalid."));
                }
                if (message.MaximumPayloadBytes <= 0)
                    diagnostics.Add(Error("AKP017", catalog.CatalogId, message.Id, "Maximum payload bytes must be greater than zero."));
                if (double.IsNaN(message.CaptureSampleRate) ||
                    message.CaptureSampleRate < 0d ||
                    message.CaptureSampleRate > 1d)
                {
                    diagnostics.Add(Error("AKP018", catalog.CatalogId, message.Id, "Capture sample rate must be within 0..1."));
                }

                if (!keys.Add(message.CreateKey(catalog.CatalogId)))
                    diagnostics.Add(Error("AKP019", catalog.CatalogId, message.Id, "Message transport key is duplicated."));

                ValidateDirection(catalog.CatalogId, message, diagnostics);
                ValidateSensitiveFields(catalog.CatalogId, message, diagnostics);
            }

            foreach (var pair in ids)
            {
                var message = pair.Value;
                if (message.Kind != ProtocolPacketKind.Request || string.IsNullOrWhiteSpace(message.ResponseId))
                    continue;

                if (!ids.TryGetValue(message.ResponseId, out var response))
                {
                    diagnostics.Add(Error("AKP023", catalog.CatalogId, message.Id, $"Response '{message.ResponseId}' does not exist."));
                }
                else if (response.Kind != ProtocolPacketKind.Response)
                {
                    diagnostics.Add(Error("AKP024", catalog.CatalogId, message.Id, $"Response '{message.ResponseId}' is not a response message."));
                }
            }
        }

        private static void ValidateDirection(
            string catalogId,
            ProtocolMessageDefinition message,
            ICollection<ProtocolCatalogDiagnostic> diagnostics)
        {
            if (message.Kind == ProtocolPacketKind.Request &&
                message.Direction == ProtocolDirection.ServerToClient)
            {
                diagnostics.Add(Error("AKP020", catalogId, message.Id, "A request cannot be server-to-client."));
            }
            else if ((message.Kind == ProtocolPacketKind.Response || message.Kind == ProtocolPacketKind.Push) &&
                     message.Direction == ProtocolDirection.ClientToServer)
            {
                diagnostics.Add(Error("AKP021", catalogId, message.Id, $"A {message.Kind} cannot be client-to-server."));
            }
        }

        private static void ValidateSensitiveFields(
            string catalogId,
            ProtocolMessageDefinition message,
            ICollection<ProtocolCatalogDiagnostic> diagnostics)
        {
            var fields = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < message.SensitiveFields.Count; i++)
            {
                var field = message.SensitiveFields[i];
                if (string.IsNullOrWhiteSpace(field) || !fields.Add(field))
                {
                    diagnostics.Add(Error("AKP022", catalogId, message.Id, "Sensitive field paths must be non-empty and unique."));
                }
            }
        }

        private static ProtocolCatalogDiagnostic Error(
            string code,
            string catalogId,
            string messageId,
            string detail) =>
            new ProtocolCatalogDiagnostic(
                ProtocolCatalogDiagnosticSeverity.Error,
                code,
                catalogId,
                messageId,
                detail);
    }
}
