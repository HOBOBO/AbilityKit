#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Protocol.Catalog
{
    public delegate object? ProtocolPayloadDecoder(ArraySegment<byte> payload);

    public readonly struct ProtocolDecodeResult
    {
        private ProtocolDecodeResult(bool success, object? value, string error)
        {
            Success = success;
            Value = value;
            Error = error ?? string.Empty;
        }

        public bool Success { get; }
        public object? Value { get; }
        public string Error { get; }

        public static ProtocolDecodeResult Decoded(object? value) =>
            new ProtocolDecodeResult(true, value, string.Empty);

        public static ProtocolDecodeResult Failed(string error) =>
            new ProtocolDecodeResult(false, null, error);
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
            ProtocolPayloadDecoder? decoder;
            lock (_gate)
            {
                if (!_decoders.TryGetValue(Qualify(catalogId, messageId), out decoder))
                    return ProtocolDecodeResult.Failed("No payload decoder is registered.");
            }

            try
            {
                return ProtocolDecodeResult.Decoded(decoder(payload));
            }
            catch (Exception exception)
            {
                return ProtocolDecodeResult.Failed(exception.Message);
            }
        }

        private static string Qualify(string catalogId, string messageId) =>
            $"{catalogId ?? string.Empty}/{messageId ?? string.Empty}";
    }
}
