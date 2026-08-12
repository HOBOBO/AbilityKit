using System;
using AbilityKit.Core.Logging;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// Small synchronous rollback scope for temporary-entity spawn flows.
    /// Entries are stored inline and executed in reverse registration order.
    /// </summary>
    internal sealed class MobaTemporaryEntitySpawnTransaction : IDisposable
    {
        private const int MaxCompensations = 8;

        private RollbackEntry _entry0;
        private RollbackEntry _entry1;
        private RollbackEntry _entry2;
        private RollbackEntry _entry3;
        private RollbackEntry _entry4;
        private RollbackEntry _entry5;
        private RollbackEntry _entry6;
        private RollbackEntry _entry7;
        private readonly Action<string, Exception> _rollbackDiagnostic;
        private int _count;
        private bool _completed;

        public MobaTemporaryEntitySpawnTransaction()
            : this(LogRollbackFailure)
        {
        }

        internal MobaTemporaryEntitySpawnTransaction(Action<string, Exception> rollbackDiagnostic)
        {
            _rollbackDiagnostic = rollbackDiagnostic;
        }

        public Exception PrimaryException { get; private set; }
        public Exception FirstRollbackException { get; private set; }
        public int RollbackExceptionCount { get; private set; }

        public void Enlist(string name, Action compensation)
        {
            if (compensation == null) throw new ArgumentNullException(nameof(compensation));
            if (_completed) throw new InvalidOperationException("Cannot enlist compensation after the spawn transaction has completed.");
            if (_count >= MaxCompensations)
            {
                throw new InvalidOperationException($"Temporary entity spawn transaction supports at most {MaxCompensations} compensations.");
            }

            SetEntry(_count++, new RollbackEntry(name, compensation));
        }

        public void Commit()
        {
            if (_completed) return;
            _completed = true;
            ClearEntries();
        }

        public Exception Rollback(Exception primaryException = null)
        {
            if (primaryException != null && PrimaryException == null)
            {
                PrimaryException = primaryException;
            }

            if (_completed) return PrimaryException ?? FirstRollbackException;
            _completed = true;

            for (var i = _count - 1; i >= 0; i--)
            {
                var entry = GetEntry(i);
                try
                {
                    entry.Compensation();
                }
                catch (Exception ex)
                {
                    if (FirstRollbackException == null) FirstRollbackException = ex;
                    RollbackExceptionCount++;
                    try
                    {
                        _rollbackDiagnostic?.Invoke(entry.Name, ex);
                    }
                    catch
                    {
                        // Rollback diagnostics are best-effort and must not stop later compensations.
                    }
                }
            }

            ClearEntries();
            return PrimaryException ?? FirstRollbackException;
        }

        public void Dispose()
        {
            Rollback();
        }

        private RollbackEntry GetEntry(int index)
        {
            switch (index)
            {
                case 0: return _entry0;
                case 1: return _entry1;
                case 2: return _entry2;
                case 3: return _entry3;
                case 4: return _entry4;
                case 5: return _entry5;
                case 6: return _entry6;
                case 7: return _entry7;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        private void SetEntry(int index, in RollbackEntry entry)
        {
            switch (index)
            {
                case 0: _entry0 = entry; break;
                case 1: _entry1 = entry; break;
                case 2: _entry2 = entry; break;
                case 3: _entry3 = entry; break;
                case 4: _entry4 = entry; break;
                case 5: _entry5 = entry; break;
                case 6: _entry6 = entry; break;
                case 7: _entry7 = entry; break;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        private void ClearEntries()
        {
            _entry0 = default;
            _entry1 = default;
            _entry2 = default;
            _entry3 = default;
            _entry4 = default;
            _entry5 = default;
            _entry6 = default;
            _entry7 = default;
            _count = 0;
        }

        private static void LogRollbackFailure(string name, Exception exception)
        {
            Log.Exception(exception, $"[MobaTemporaryEntitySpawnTransaction] rollback compensation failed. step={name ?? "<unnamed>"}");
        }

        private readonly struct RollbackEntry
        {
            public RollbackEntry(string name, Action compensation)
            {
                Name = name;
                Compensation = compensation;
            }

            public string Name { get; }
            public Action Compensation { get; }
        }
    }
}
