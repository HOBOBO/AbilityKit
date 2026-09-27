using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    /// <summary>
    /// In-process journal of serialized inverse commands. A snapshot stores only a checkpoint into
    /// this journal; command payloads remain in the journal until their rollback window is trimmed.
    /// </summary>
    public sealed class CommandRollbackLog
    {
        private readonly List<RollbackCommandRecord> _entries;
        private long _nextOrder;
        private long _rollbackFloorOrder;
        private int _epoch = 1;
        private bool _isProcessingRollback;
        private bool _isFaulted;

        public CommandRollbackLog(int capacity = 64)
        {
            _entries = new List<RollbackCommandRecord>(capacity > 0 ? capacity : 64);
        }

        public int Count => _entries.Count;

        public int Epoch => _epoch;

        public long NextOrder => _nextOrder;

        /// <summary>
        /// True when a handler threw after rollback execution had started. The world may be only
        /// partially restored; callers must rebuild it and clear the journal before reuse.
        /// </summary>
        public bool IsFaulted => _isFaulted;

        public CommandJournalCheckpoint CreateCheckpoint()
        {
            EnsureUsable();
            return new CommandJournalCheckpoint(
                CommandJournalCheckpoint.CurrentVersion,
                _epoch,
                _nextOrder);
        }

        public void Record(
            FrameIndex frame,
            int commandType,
            int payloadVersion,
            byte[] payload)
        {
            EnsureUsable();
            if (_isProcessingRollback)
            {
                throw new InvalidOperationException(
                    "Cannot record a rollback command while rollback validation or execution is in progress.");
            }

            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (payloadVersion < 0) throw new ArgumentOutOfRangeException(nameof(payloadVersion));
            if (_nextOrder == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "Rollback command order is exhausted. Clear the journal at a session boundary.");
            }

            var ownedPayload = payload.Length == 0 ? Array.Empty<byte>() : (byte[])payload.Clone();
            _entries.Add(new RollbackCommandRecord(
                frame,
                _nextOrder,
                commandType,
                payloadVersion,
                ownedPayload));
            _nextOrder++;
        }

        public RollbackCommandRecord GetRecord(int index)
        {
            return _entries[index];
        }

        /// <summary>
        /// Validates the complete rollback suffix without executing a command.
        /// </summary>
        public int PrepareRollback(
            in CommandJournalCheckpoint checkpoint,
            RollbackCommandHandlerRegistry handlers,
            FrameIndex targetFrame,
            IServiceProvider services = null)
        {
            if (handlers == null) throw new ArgumentNullException(nameof(handlers));
            EnsureCanProcess(checkpoint);
            if (_isProcessingRollback)
            {
                throw new InvalidOperationException("Rollback command processing is already in progress.");
            }

            _isProcessingRollback = true;
            try
            {
                return ValidateSuffix(checkpoint.NextOrder, handlers, targetFrame, services);
            }
            finally
            {
                _isProcessingRollback = false;
            }
        }

        /// <summary>
        /// Validates and executes the journal suffix in strict reverse order.
        /// </summary>
        public int RollbackTo(
            in CommandJournalCheckpoint checkpoint,
            RollbackCommandHandlerRegistry handlers,
            FrameIndex targetFrame,
            IServiceProvider services = null)
        {
            if (handlers == null) throw new ArgumentNullException(nameof(handlers));
            EnsureCanProcess(checkpoint);
            if (_isProcessingRollback)
            {
                throw new InvalidOperationException("Rollback command processing is already in progress.");
            }

            _isProcessingRollback = true;
            var executionStarted = false;
            try
            {
                var count = ValidateSuffix(checkpoint.NextOrder, handlers, targetFrame, services);
                executionStarted = count > 0;

                for (var i = _entries.Count - 1; i >= 0; i--)
                {
                    var record = _entries[i];
                    if (record.Order < checkpoint.NextOrder) break;

                    var handler = handlers.GetRequired(record.CommandType, record.PayloadVersion);
                    var context = CreateContext(targetFrame, record, services);
                    handler.Rollback(in context, record.PayloadVersion, record.Payload);
                    _entries.RemoveAt(i);
                }

                _nextOrder = checkpoint.NextOrder;
                return count;
            }
            catch
            {
                if (executionStarted) _isFaulted = true;
                throw;
            }
            finally
            {
                _isProcessingRollback = false;
            }
        }

        /// <summary>
        /// Drops commands older than the retained frame. Checkpoints crossing the removed range
        /// become invalid and are rejected instead of producing a partial rollback.
        /// </summary>
        public void TrimBefore(FrameIndex frame)
        {
            EnsureMutable();
            var newFloor = _rollbackFloorOrder;
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var record = _entries[i];
                if (record.Frame.Value >= frame.Value) continue;

                var candidateFloor = record.Order + 1;
                if (candidateFloor > newFloor) newFloor = candidateFloor;
                _entries.RemoveAt(i);
            }

            _rollbackFloorOrder = newFloor;
        }

        /// <summary>
        /// Starts a new journal epoch. All checkpoints from the previous epoch become invalid.
        /// </summary>
        public void Clear()
        {
            if (_isProcessingRollback)
            {
                throw new InvalidOperationException(
                    "Cannot clear the rollback command journal while rollback is in progress.");
            }

            _entries.Clear();
            _nextOrder = 0;
            _rollbackFloorOrder = 0;
            _isFaulted = false;
            unchecked
            {
                _epoch++;
                if (_epoch == 0) _epoch = 1;
            }
        }

        private int ValidateSuffix(
            long nextOrder,
            RollbackCommandHandlerRegistry handlers,
            FrameIndex targetFrame,
            IServiceProvider services)
        {
            var count = 0;
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var record = _entries[i];
                if (record.Order < nextOrder) break;

                var handler = handlers.GetRequired(record.CommandType, record.PayloadVersion);
                var context = CreateContext(targetFrame, record, services);
                handler.ValidateRollback(in context, record.PayloadVersion, record.Payload);
                count++;
            }

            return count;
        }

        private static RollbackCommandContext CreateContext(
            FrameIndex targetFrame,
            in RollbackCommandRecord record,
            IServiceProvider services)
        {
            return new RollbackCommandContext(
                targetFrame,
                record.Frame,
                record.Order,
                services);
        }

        private void EnsureCanProcess(in CommandJournalCheckpoint checkpoint)
        {
            EnsureUsable();
            if (checkpoint.Version != CommandJournalCheckpoint.CurrentVersion)
            {
                throw new NotSupportedException(
                    $"Unsupported command journal checkpoint version: {checkpoint.Version}");
            }

            if (checkpoint.Epoch != _epoch)
            {
                throw new InvalidOperationException(
                    $"Command journal checkpoint epoch mismatch. expected={_epoch} actual={checkpoint.Epoch}");
            }

            if (checkpoint.NextOrder < _rollbackFloorOrder || checkpoint.NextOrder > _nextOrder)
            {
                throw new InvalidOperationException(
                    $"Command journal checkpoint is outside the retained range. " +
                    $"retainedStart={_rollbackFloorOrder} nextOrder={_nextOrder} checkpoint={checkpoint.NextOrder}");
            }
        }

        private void EnsureMutable()
        {
            EnsureUsable();
            if (_isProcessingRollback)
            {
                throw new InvalidOperationException(
                    "Cannot mutate the rollback command journal while rollback is in progress.");
            }
        }

        private void EnsureUsable()
        {
            if (_isFaulted)
            {
                throw new InvalidOperationException(
                    "Rollback command journal is faulted after a partial restore. Rebuild the world and clear the journal before reuse.");
            }
        }
    }
}
