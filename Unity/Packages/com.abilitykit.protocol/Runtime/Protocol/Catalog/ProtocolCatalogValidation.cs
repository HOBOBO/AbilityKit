#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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

    /// <summary>
    /// Tunable validation policy. The default instance accepts the codec names shipped
    /// by the AbilityKit protocol packages; callers that host additional project codecs
    /// register them by passing an explicit whitelist so that unknown or mistyped codec
    /// names remain a validation error.
    /// </summary>
    public sealed class ProtocolCatalogValidationOptions
    {
        public static ProtocolCatalogValidationOptions Default { get; } = CreateDefault();

        public ProtocolCatalogValidationOptions(IReadOnlyCollection<string>? allowedCodecs = null)
        {
            AllowedCodecs = BuildCodecSet(allowedCodecs);
        }

        public IReadOnlyCollection<string> AllowedCodecs { get; }

        /// <summary>大小写不敏感成员判定（避免 IReadOnlyCollection&lt;string&gt;.Contains 在 Unity netstandard2.1 下的扩展解析歧义）。</summary>
        public bool ContainsCodec(string codec)
        {
            if (string.IsNullOrEmpty(codec)) return false;
            foreach (var allowed in AllowedCodecs)
            {
                if (string.Equals(allowed, codec, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static ProtocolCatalogValidationOptions CreateDefault() =>
            new ProtocolCatalogValidationOptions(new[] { "memorypack", "custom-binary", "protobuf" });

        private static IReadOnlyCollection<string> BuildCodecSet(IReadOnlyCollection<string>? allowedCodecs)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (allowedCodecs == null)
            {
                set.Add("memorypack");
                set.Add("custom-binary");
                set.Add("protobuf");
                return set;
            }

            foreach (var codec in allowedCodecs)
            {
                if (!string.IsNullOrWhiteSpace(codec))
                    set.Add(codec.Trim());
            }

            return set;
        }
    }

    public static class ProtocolCatalogValidator
    {
        private static readonly Regex PayloadTypePattern = new Regex(
            @"^[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*(?:\[\])?$",
            RegexOptions.CultureInvariant);

        public static ProtocolCatalogValidationResult Validate(ProtocolCatalogDefinition? catalog) =>
            Validate(catalog, ProtocolCatalogValidationOptions.Default);

        public static ProtocolCatalogValidationResult Validate(
            ProtocolCatalogDefinition? catalog,
            ProtocolCatalogValidationOptions? options)
        {
            options ??= ProtocolCatalogValidationOptions.Default;

            var diagnostics = new List<ProtocolCatalogDiagnostic>();
            if (catalog == null)
            {
                diagnostics.Add(Error("AKP000", string.Empty, string.Empty, "Catalog is null."));
                return new ProtocolCatalogValidationResult(diagnostics);
            }

            ValidateHeader(catalog, options, diagnostics);
            ValidateMessages(catalog, options, diagnostics);
            return new ProtocolCatalogValidationResult(diagnostics);
        }

        public static ProtocolCatalogValidationResult Validate(
            IReadOnlyList<ProtocolCatalogDefinition?>? catalogs) =>
            Validate(catalogs, ProtocolCatalogValidationOptions.Default);

        public static ProtocolCatalogValidationResult Validate(
            IReadOnlyList<ProtocolCatalogDefinition?>? catalogs,
            ProtocolCatalogValidationOptions? options)
        {
            options ??= ProtocolCatalogValidationOptions.Default;

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

                var result = Validate(catalog, options);
                for (var d = 0; d < result.Diagnostics.Count; d++)
                    diagnostics.Add(result.Diagnostics[d]);
            }

            ValidateCrossCatalogOpCodeConflicts(catalogs, diagnostics);
            return new ProtocolCatalogValidationResult(diagnostics);
        }

        private static void ValidateHeader(
            ProtocolCatalogDefinition catalog,
            ProtocolCatalogValidationOptions options,
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
            else if (!options.ContainsCodec(catalog.DefaultCodec))
                diagnostics.Add(Error("AKP031", catalog.CatalogId, string.Empty, $"Default codec '{catalog.DefaultCodec}' is not in the allowed codec set."));
        }

        private static void ValidateMessages(
            ProtocolCatalogDefinition catalog,
            ProtocolCatalogValidationOptions options,
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
                else if (!PayloadTypePattern.IsMatch(message.PayloadType))
                    diagnostics.Add(Error("AKP033", catalog.CatalogId, message.Id, $"Payload type '{message.PayloadType}' is not a well-formed .NET type name."));
                if (string.IsNullOrWhiteSpace(message.Codec))
                    diagnostics.Add(Error("AKP015", catalog.CatalogId, message.Id, "Codec is required."));
                else if (!options.ContainsCodec(message.Codec))
                    diagnostics.Add(Error("AKP032", catalog.CatalogId, message.Id, $"Codec '{message.Codec}' is not in the allowed codec set."));
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
                ValidateResponseIdOwnership(catalog.CatalogId, message, diagnostics);
                ValidateSensitiveFields(catalog.CatalogId, message, diagnostics);
            }

            ValidateResponseLinks(catalog, ids, diagnostics);
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

        private static void ValidateResponseIdOwnership(
            string catalogId,
            ProtocolMessageDefinition message,
            ICollection<ProtocolCatalogDiagnostic> diagnostics)
        {
            if (message.Kind != ProtocolPacketKind.Request && !string.IsNullOrWhiteSpace(message.ResponseId))
            {
                diagnostics.Add(Error(
                    "AKP025",
                    catalogId,
                    message.Id,
                    $"Only a request may declare a response id; {message.Kind} cannot reference '{message.ResponseId}'."));
            }
        }

        private static void ValidateResponseLinks(
            ProtocolCatalogDefinition catalog,
            IReadOnlyDictionary<string, ProtocolMessageDefinition> ids,
            ICollection<ProtocolCatalogDiagnostic> diagnostics)
        {
            var responseRefCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var pair in ids)
            {
                var message = pair.Value;
                if (message.Kind != ProtocolPacketKind.Request || string.IsNullOrWhiteSpace(message.ResponseId))
                    continue;

                if (!ids.TryGetValue(message.ResponseId, out var response))
                {
                    diagnostics.Add(Error("AKP023", catalog.CatalogId, message.Id, $"Response '{message.ResponseId}' does not exist."));
                    continue;
                }

                if (response.Kind != ProtocolPacketKind.Response)
                {
                    diagnostics.Add(Error("AKP024", catalog.CatalogId, message.Id, $"Response '{message.ResponseId}' is not a response message."));
                    continue;
                }

                if (!Overlaps(
                        message.MinimumSchemaVersion,
                        message.MaximumSchemaVersion,
                        response.MinimumSchemaVersion,
                        response.MaximumSchemaVersion))
                {
                    diagnostics.Add(Error(
                        "AKP027",
                        catalog.CatalogId,
                        message.Id,
                        $"Request and response '{message.ResponseId}' have non-overlapping schema version ranges."));
                }

                responseRefCounts[message.ResponseId] =
                    responseRefCounts.TryGetValue(message.ResponseId, out var count) ? count + 1 : 1;
            }

            foreach (var pair in ids)
            {
                var message = pair.Value;
                if (message.Kind != ProtocolPacketKind.Response)
                    continue;

                var refCount = responseRefCounts.TryGetValue(message.Id, out var count) ? count : 0;
                if (refCount == 0)
                {
                    diagnostics.Add(Error("AKP026", catalog.CatalogId, message.Id, "Response is not referenced by any request."));
                }
                else if (refCount > 1)
                {
                    diagnostics.Add(Error("AKP026", catalog.CatalogId, message.Id, $"Response is referenced by {refCount} requests; a response must be referenced by exactly one request."));
                }
            }
        }

        private static void ValidateCrossCatalogOpCodeConflicts(
            IReadOnlyList<ProtocolCatalogDefinition?> catalogs,
            ICollection<ProtocolCatalogDiagnostic> diagnostics)
        {
            // The wire header carries only the opcode, so within one project the
            // (opCode, direction, kind) identity must be unambiguous across catalogs
            // that could share a connection. Different projects are isolated.
            var claimed = new Dictionary<
                (string ProjectId, uint OpCode, ProtocolDirection Direction, ProtocolPacketKind Kind),
                string>();

            for (var i = 0; i < catalogs.Count; i++)
            {
                var catalog = catalogs[i];
                if (catalog == null)
                    continue;

                for (var m = 0; m < catalog.Messages.Count; m++)
                {
                    var message = catalog.Messages[m];
                    if (message == null || message.OpCode == 0)
                        continue;

                    var key = (catalog.ProjectId, message.OpCode, message.Direction, message.Kind);
                    if (claimed.TryGetValue(key, out var existingCatalogId))
                    {
                        if (!string.Equals(existingCatalogId, catalog.CatalogId, StringComparison.Ordinal))
                        {
                            diagnostics.Add(Error(
                                "AKP030",
                                catalog.CatalogId,
                                message.Id,
                                $"OpCode {message.OpCode} ({message.Direction}, {message.Kind}) conflicts with catalog '{existingCatalogId}' in project '{catalog.ProjectId}'."));
                        }
                    }
                    else
                    {
                        claimed.Add(key, catalog.CatalogId);
                    }
                }
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

        private static bool Overlaps(int minimumA, int maximumA, int minimumB, int maximumB) =>
            minimumA <= maximumB && minimumB <= maximumA;

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
