using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    public sealed class RollbackCommandHandlerRegistry
    {
        private readonly Dictionary<int, IRollbackCommandHandler> _handlers =
            new Dictionary<int, IRollbackCommandHandler>(16);

        private bool _sealed;

        public bool IsSealed => _sealed;

        public void Register(IRollbackCommandHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (_sealed)
            {
                throw new InvalidOperationException(
                    $"Rollback command handler registry is sealed. commandType={handler.CommandType}");
            }

            if (_handlers.TryGetValue(handler.CommandType, out var existing))
            {
                if (ReferenceEquals(existing, handler)) return;

                throw new InvalidOperationException(
                    $"Rollback command handler type already registered: {handler.CommandType}");
            }

            _handlers.Add(handler.CommandType, handler);
        }

        public void Seal()
        {
            _sealed = true;
        }

        public bool TryGet(int commandType, out IRollbackCommandHandler handler)
        {
            return _handlers.TryGetValue(commandType, out handler);
        }

        internal IRollbackCommandHandler GetRequired(int commandType, int payloadVersion)
        {
            if (!_sealed)
            {
                throw new InvalidOperationException(
                    "Rollback command handler registry must be sealed before restoring.");
            }

            if (!_handlers.TryGetValue(commandType, out var handler) || handler == null)
            {
                throw new InvalidOperationException(
                    $"Rollback command handler not found. commandType={commandType}");
            }

            if (!handler.CanRollback(payloadVersion))
            {
                throw new InvalidOperationException(
                    $"Rollback command payload version is not supported. commandType={commandType} payloadVersion={payloadVersion}");
            }

            return handler;
        }
    }
}
