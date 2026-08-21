#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using AbilityKit.Protocol.Serialization;

namespace AbilityKit.Network.Sdk.Observability
{
    public sealed class NetworkTrafficExportOptions
    {
        public bool IncludeDecodedPayload { get; set; } = true;
        public bool IncludeRawPayloadPreview { get; set; }
        public bool AllowSensitiveRawPayloadPreview { get; set; }
        public bool PrettyPrint { get; set; } = true;
    }

    /// <summary>Produces portable JSON without exposing sensitive decoded fields by default.</summary>
    public sealed class NetworkTrafficJsonExporter
    {
        private const string RedactedValue = "[REDACTED]";

        public string Export(
            IReadOnlyList<NetworkTrafficInspectionRow> rows,
            NetworkTrafficExportOptions? options = null)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            options ??= new NetworkTrafficExportOptions();

            var exportedRows = new List<object?>(rows.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i] ?? throw new ArgumentException(
                    "Traffic exports cannot contain null rows.", nameof(rows));
                exportedRows.Add(ProjectRow(row, options));
            }

            var document = new Dictionary<string, object?>
            {
                ["schemaVersion"] = 1,
                ["exportedAtUtc"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                ["eventCount"] = exportedRows.Count,
                ["events"] = exportedRows
            };
            return WireSerializer.SerializeToText(document, options.PrettyPrint);
        }

        /// <summary>Formats one decoded value using the same redaction policy as file export.</summary>
        public string FormatDecodedPayload(NetworkTrafficInspectionRow row, bool prettyPrint = true)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (!row.Decode.Success) return row.Decode.Error;
            var projected = ProjectValue(row.Decode.Value, row.Message?.SensitiveFields);
            return WireSerializer.SerializeToText(projected, prettyPrint) ?? "null";
        }

        private static IDictionary<string, object?> ProjectRow(
            NetworkTrafficInspectionRow row,
            NetworkTrafficExportOptions options)
        {
            var traffic = row.Traffic;
            var result = new Dictionary<string, object?>
            {
                ["timestampUtc"] = traffic.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
                ["connectionId"] = traffic.ConnectionId,
                ["generation"] = traffic.Generation,
                ["role"] = traffic.Role,
                ["catalogId"] = traffic.CatalogId,
                ["endpoint"] = traffic.Endpoint,
                ["transport"] = traffic.Transport,
                ["direction"] = traffic.Direction.ToString(),
                ["opCode"] = traffic.OpCode,
                ["sequence"] = traffic.Sequence,
                ["flags"] = traffic.Flags.ToString(),
                ["payloadLength"] = traffic.PayloadLength,
                ["payloadPreviewTruncated"] = traffic.IsPayloadPreviewTruncated,
                ["messageId"] = row.Message?.Id,
                ["payloadType"] = row.Message?.PayloadType,
                ["resolution"] = row.IsAmbiguous ? "ambiguous" : row.IsKnown ? "known" : "unknown"
            };

            if (options.IncludeDecodedPayload)
            {
                result["decodeSuccess"] = row.Decode.Success;
                if (row.Decode.Success)
                    result["decodedPayload"] = ProjectValue(
                        row.Decode.Value,
                        row.Message?.SensitiveFields);
                else
                    result["decodeError"] = row.Decode.Error;
            }

            if (options.IncludeRawPayloadPreview)
            {
                var hasSensitiveFields = row.Message?.SensitiveFields.Count > 0;
                if (hasSensitiveFields && !options.AllowSensitiveRawPayloadPreview)
                {
                    throw new InvalidOperationException(
                        $"Raw payload preview for '{traffic.CatalogId}/{row.Message!.Id}' may contain " +
                        "sensitive fields. Keep raw export disabled or explicitly allow it for a controlled diagnostic workflow.");
                }

                result["payloadPreviewBase64"] = Convert.ToBase64String(traffic.PayloadPreview.ToArray());
            }

            return result;
        }

        private static object? ProjectValue(object? value, IReadOnlyList<string>? sensitiveFields)
        {
            var sensitive = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (sensitiveFields != null)
            {
                for (var i = 0; i < sensitiveFields.Count; i++)
                    sensitive.Add(sensitiveFields[i]);
            }

            return ProjectValue(value, sensitive, new HashSet<object>(ReferenceComparer.Instance), 0);
        }

        private static object? ProjectValue(
            object? value,
            ISet<string> sensitive,
            ISet<object> visited,
            int depth)
        {
            if (value == null) return null;
            if (depth > 16) return "[MAX_DEPTH]";

            var type = value.GetType();
            if (type.IsEnum || type.IsPrimitive || value is decimal || value is string ||
                value is DateTime || value is DateTimeOffset || value is Guid)
                return value;
            if (value is byte[] bytes) return Convert.ToBase64String(bytes);

            if (!type.IsValueType && !visited.Add(value)) return "[CYCLE]";
            try
            {
                if (value is IDictionary dictionary)
                {
                    var projected = new Dictionary<string, object?>(StringComparer.Ordinal);
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        var name = Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty;
                        projected[name] = sensitive.Contains(name)
                            ? RedactedValue
                            : ProjectValue(entry.Value, sensitive, visited, depth + 1);
                    }
                    return projected;
                }

                if (value is IEnumerable enumerable)
                {
                    var projected = new List<object?>();
                    foreach (var item in enumerable)
                        projected.Add(ProjectValue(item, sensitive, visited, depth + 1));
                    return projected;
                }

                var members = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (!property.CanRead || property.GetIndexParameters().Length != 0) continue;
                    members[property.Name] = sensitive.Contains(property.Name)
                        ? RedactedValue
                        : ProjectValue(ReadProperty(property, value), sensitive, visited, depth + 1);
                }
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (members.ContainsKey(field.Name)) continue;
                    members[field.Name] = sensitive.Contains(field.Name)
                        ? RedactedValue
                        : ProjectValue(field.GetValue(value), sensitive, visited, depth + 1);
                }
                return members;
            }
            finally
            {
                if (!type.IsValueType) visited.Remove(value);
            }
        }

        private static object? ReadProperty(PropertyInfo property, object owner)
        {
            try { return property.GetValue(owner, null); }
            catch (Exception exception) { return $"[READ_ERROR: {exception.GetType().Name}]"; }
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object? left, object? right) => ReferenceEquals(left, right);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
