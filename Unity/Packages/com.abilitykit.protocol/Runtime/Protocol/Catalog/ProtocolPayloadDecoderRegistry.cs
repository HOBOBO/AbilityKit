#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Protocol.Catalog
{
    public delegate object? ProtocolPayloadDecoder(ArraySegment<byte> payload);

    public enum ProtocolDecodeFailureKind
    {
        None = 0,
        InvalidRequest = 1,
        DecoderNotRegistered = 2,
        UnsupportedSchemaVersion = 3,
        PayloadTooLarge = 4,
        DecoderException = 5,
        UnknownMessage = 6,
        AmbiguousMessage = 7,
        PayloadPreviewTruncated = 8,
        MalformedPayload = 9
    }

    public readonly struct ProtocolDecodeResult
    {
        private ProtocolDecodeResult(
            bool success,
            object? value,
            string error,
            ProtocolDecodeFailureKind failureKind)
        {
            Success = success;
            Value = value;
            Error = error ?? string.Empty;
            FailureKind = failureKind;
        }

        public bool Success { get; }
        public object? Value { get; }
        public string Error { get; }
        public ProtocolDecodeFailureKind FailureKind { get; }

        public static ProtocolDecodeResult Decoded(object? value) =>
            new ProtocolDecodeResult(true, value, string.Empty, ProtocolDecodeFailureKind.None);

        public static ProtocolDecodeResult Failed(
            string error,
            ProtocolDecodeFailureKind failureKind = ProtocolDecodeFailureKind.InvalidRequest) =>
            new ProtocolDecodeResult(false, null, error, failureKind);
    }

    public sealed class ProtocolPayloadDecoderRegistry
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, ProtocolPayloadDecoder> _decoders =
            new Dictionary<string, ProtocolPayloadDecoder>(StringComparer.Ordinal);

        public void Register(string catalogId, string messageId, ProtocolPayloadDecoder decoder)
        {
            if (string.IsNullOrWhiteSpace(catalogId))
                throw new ArgumentException("Catalog id is required.", nameof(catalogId));
            if (string.IsNullOrWhiteSpace(messageId))
                throw new ArgumentException("Message id is required.", nameof(messageId));
            if (decoder == null) throw new ArgumentNullException(nameof(decoder));

            var key = Qualify(catalogId, messageId);
            lock (_gate)
            {
                if (_decoders.ContainsKey(key))
                    throw new InvalidOperationException($"A payload decoder for '{key}' is already registered.");
                _decoders.Add(key, decoder);
            }
        }

        /// <summary>
        /// Registers a decoder unless the same catalog/message has already been installed.
        /// This is the composition-friendly entry point for protocol modules: application
        /// startup may call several module registration methods more than once without making
        /// initialization order part of the protocol contract.
        /// </summary>
        public bool TryRegister(string catalogId, string messageId, ProtocolPayloadDecoder decoder)
        {
            if (string.IsNullOrWhiteSpace(catalogId))
                throw new ArgumentException("Catalog id is required.", nameof(catalogId));
            if (string.IsNullOrWhiteSpace(messageId))
                throw new ArgumentException("Message id is required.", nameof(messageId));
            if (decoder == null) throw new ArgumentNullException(nameof(decoder));

            var key = Qualify(catalogId, messageId);
            lock (_gate)
            {
                if (_decoders.ContainsKey(key)) return false;
                _decoders.Add(key, decoder);
                return true;
            }
        }

        /// <summary>Returns whether a decoder is registered without invoking it.</summary>
        public bool IsRegistered(string catalogId, string messageId)
        {
            if (string.IsNullOrWhiteSpace(catalogId)) return false;
            if (string.IsNullOrWhiteSpace(messageId)) return false;

            lock (_gate)
            {
                return _decoders.ContainsKey(Qualify(catalogId, messageId));
            }
        }

        public ProtocolDecodeResult Decode(
            string catalogId,
            string messageId,
            ArraySegment<byte> payload)
        {
            return DecodeRegistered(catalogId, messageId, payload);
        }

        /// <summary>
        /// Decodes a payload after enforcing the catalog message's schema-version range and
        /// maximum payload size. Use this overload at transport and inspection boundaries so
        /// malformed or oversized input is rejected before a decoder can allocate or parse it.
        /// A null schema version skips only the version check, which is useful for captures whose
        /// transport header does not carry a protocol version; the payload limit is always applied.
        /// </summary>
        public ProtocolDecodeResult Decode(
            string catalogId,
            ProtocolMessageDefinition definition,
            ArraySegment<byte> payload,
            int? schemaVersion = null)
        {
            if (string.IsNullOrWhiteSpace(catalogId))
                return ProtocolDecodeResult.Failed(
                    "Catalog id is required.",
                    ProtocolDecodeFailureKind.InvalidRequest);
            if (definition == null)
                return ProtocolDecodeResult.Failed(
                    "Protocol message definition is required.",
                    ProtocolDecodeFailureKind.InvalidRequest);
            if (payload.Count < 0)
                return ProtocolDecodeResult.Failed(
                    "Payload segment is invalid.",
                    ProtocolDecodeFailureKind.InvalidRequest);
            if (schemaVersion.HasValue &&
                (schemaVersion.Value < definition.MinimumSchemaVersion ||
                 schemaVersion.Value > definition.MaximumSchemaVersion))
            {
                return ProtocolDecodeResult.Failed(
                    $"Schema version {schemaVersion.Value} is outside the supported range " +
                    $"{definition.MinimumSchemaVersion}..{definition.MaximumSchemaVersion}.",
                    ProtocolDecodeFailureKind.UnsupportedSchemaVersion);
            }
            if (payload.Count > definition.MaximumPayloadBytes)
            {
                return ProtocolDecodeResult.Failed(
                    $"Payload length {payload.Count} exceeds the maximum " +
                    $"{definition.MaximumPayloadBytes} bytes.",
                    ProtocolDecodeFailureKind.PayloadTooLarge);
            }

            return DecodeRegistered(catalogId, definition.Id, payload);
        }

        private ProtocolDecodeResult DecodeRegistered(
            string catalogId,
            string messageId,
            ArraySegment<byte> payload)
        {
            ProtocolPayloadDecoder? decoder;
            lock (_gate)
            {
                if (!_decoders.TryGetValue(Qualify(catalogId, messageId), out decoder))
                    return ProtocolDecodeResult.Failed(
                        "No payload decoder is registered.",
                        ProtocolDecodeFailureKind.DecoderNotRegistered);
            }

            try
            {
                return ProtocolDecodeResult.Decoded(decoder(payload));
            }
            catch (Exception exception)
            {
                return ProtocolDecodeResult.Failed(
                    exception.Message,
                    ProtocolDecodeFailureKind.DecoderException);
            }
        }

        private static string Qualify(string catalogId, string messageId) =>
            $"{catalogId ?? string.Empty}/{messageId ?? string.Empty}";
    }
}
