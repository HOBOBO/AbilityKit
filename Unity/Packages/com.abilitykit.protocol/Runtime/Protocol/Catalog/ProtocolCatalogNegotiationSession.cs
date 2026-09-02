#nullable enable

using System;

namespace AbilityKit.Protocol.Catalog
{
    public enum ProtocolCatalogNegotiationState
    {
        Pending = 0,
        Negotiated = 1,
        Failed = 2
    }

    /// <summary>
    /// Connection-scoped holder for catalog negotiation. A new physical connection must call
    /// <see cref="Reset"/> before receiving packets; applying a remote advertisement transitions
    /// the session to either Negotiated or Failed.
    /// </summary>
    public sealed class ProtocolCatalogNegotiationSession
    {
        private readonly object _gate = new object();
        private readonly ProtocolCatalogDefinition _localCatalog;
        private ProtocolCatalogNegotiationResult? _result;
        private int _connectionGeneration;
        private ProtocolCatalogNegotiationState _state = ProtocolCatalogNegotiationState.Pending;

        public ProtocolCatalogNegotiationSession(ProtocolCatalogDefinition localCatalog)
        {
            _localCatalog = localCatalog ?? throw new ArgumentNullException(nameof(localCatalog));
        }

        public ProtocolCatalogNegotiationState State
        {
            get { lock (_gate) return _state; }
        }

        public int ConnectionGeneration
        {
            get { lock (_gate) return _connectionGeneration; }
        }

        public ProtocolCatalogNegotiationResult? Result
        {
            get { lock (_gate) return _result; }
        }

        public bool IsNegotiated
        {
            get { lock (_gate) return _state == ProtocolCatalogNegotiationState.Negotiated; }
        }

        public void Reset(int connectionGeneration = 0)
        {
            lock (_gate)
            {
                _connectionGeneration = connectionGeneration;
                _state = ProtocolCatalogNegotiationState.Pending;
                _result = null;
            }
        }

        public ProtocolCatalogNegotiationResult ApplyRemoteCatalog(
            ProtocolCatalogDefinition remoteCatalog)
        {
            if (remoteCatalog == null) throw new ArgumentNullException(nameof(remoteCatalog));

            var result = ProtocolCatalogNegotiator.Negotiate(_localCatalog, remoteCatalog);
            lock (_gate)
            {
                _result = result;
                _state = result.IsCompatible
                    ? ProtocolCatalogNegotiationState.Negotiated
                    : ProtocolCatalogNegotiationState.Failed;
            }
            return result;
        }
    }
}
