#nullable enable

using System;
using System.Threading.Tasks;

namespace AbilityKit.Ability.Host.Extensions.Client.StateSync
{
    /// <summary>
    /// Client-side host extension primitive for submitting locally accepted inputs to a remote authority.
    /// It keeps a bounded number of remote requests in flight and one latest local result queued for backpressure.
    /// </summary>
    public sealed class RemoteClientInputSubmitQueue<TLocalSubmitResult, TRemoteSubmitResult>
    {
        private readonly Func<TLocalSubmitResult, TimeSpan, Task<TRemoteSubmitResult>> _submitAsync;
        private readonly Func<TRemoteSubmitResult, bool>? _shouldRequestResync;
        private readonly Func<TLocalSubmitResult, TLocalSubmitResult, TLocalSubmitResult>? _mergeQueued;
        private readonly TimeSpan _timeout;
        private readonly Task<TRemoteSubmitResult>?[] _pending;
        private readonly long[] _pendingSequences;
        private TLocalSubmitResult _queuedInput = default!;
        private bool _hasQueuedInput;
        private int _pendingCount;
        private long _nextSequence;
        private long _lastOutcomeSequence;
        private TRemoteSubmitResult _lastResult = default!;
        private Exception? _lastError;
        private long _submittedCount;
        private long _queuedCount;
        private long _replacedCount;
        private long _completedCount;
        private long _failedCount;
        private long _resyncRequestedCount;

        public RemoteClientInputSubmitQueue(
            Func<TLocalSubmitResult, TimeSpan, Task<TRemoteSubmitResult>> submitAsync,
            TimeSpan timeout,
            Func<TRemoteSubmitResult, bool>? shouldRequestResync = null,
            Func<TLocalSubmitResult, TLocalSubmitResult, TLocalSubmitResult>? mergeQueued = null,
            int maxInFlight = 1)
        {
            if (maxInFlight <= 0) throw new ArgumentOutOfRangeException(nameof(maxInFlight));
            _submitAsync = submitAsync ?? throw new ArgumentNullException(nameof(submitAsync));
            _timeout = timeout;
            _shouldRequestResync = shouldRequestResync;
            _mergeQueued = mergeQueued;
            _pending = new Task<TRemoteSubmitResult>?[maxInFlight];
            _pendingSequences = new long[maxInFlight];
        }

        public bool HasPending => _pendingCount > 0;
        public int PendingCount => _pendingCount;
        public int MaxInFlight => _pending.Length;
        public bool HasQueued => _hasQueuedInput;
        public TRemoteSubmitResult LastResult => _lastResult;
        public Exception? LastError => _lastError;
        public long SubmittedCount => _submittedCount;
        public long QueuedCount => _queuedCount;
        public long ReplacedCount => _replacedCount;
        public long CompletedCount => _completedCount;
        public long FailedCount => _failedCount;
        public long ResyncRequestedCount => _resyncRequestedCount;

        public bool SubmitOrQueue(TLocalSubmitResult local)
        {
            CompleteIfFinished();
            if (_pendingCount < _pending.Length)
            {
                Start(local);
                return true;
            }

            if (_hasQueuedInput)
            {
                _replacedCount++;
                local = _mergeQueued == null
                    ? local
                    : _mergeQueued(_queuedInput, local);
            }
            else
            {
                _queuedCount++;
            }

            _queuedInput = local;
            _hasQueuedInput = true;
            return false;
        }

        public void CompleteIfFinished()
        {
            var madeProgress = true;
            while (madeProgress)
            {
                madeProgress = false;
                for (var i = 0; i < _pending.Length; i++)
                {
                    var pending = _pending[i];
                    if (pending == null || !pending.IsCompleted)
                    {
                        continue;
                    }

                    var sequence = _pendingSequences[i];
                    _pending[i] = null;
                    _pendingSequences[i] = 0L;
                    _pendingCount--;
                    madeProgress = true;
                    try
                    {
                        var result = pending.GetAwaiter().GetResult();
                        _completedCount++;
                        if (sequence >= _lastOutcomeSequence)
                        {
                            _lastResult = result;
                            _lastError = null;
                            _lastOutcomeSequence = sequence;
                        }

                        if (_shouldRequestResync != null && _shouldRequestResync(result))
                        {
                            _resyncRequestedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _failedCount++;
                        if (sequence >= _lastOutcomeSequence)
                        {
                            _lastError = ex;
                            _lastOutcomeSequence = sequence;
                        }
                    }
                }

                if (_hasQueuedInput && _pendingCount < _pending.Length)
                {
                    var next = _queuedInput;
                    _queuedInput = default!;
                    _hasQueuedInput = false;
                    Start(next);
                    madeProgress = true;
                }
            }
        }

        public void Reset()
        {
            Array.Clear(_pending, 0, _pending.Length);
            Array.Clear(_pendingSequences, 0, _pendingSequences.Length);
            _queuedInput = default!;
            _hasQueuedInput = false;
            _pendingCount = 0;
            _nextSequence = 0L;
            _lastOutcomeSequence = 0L;
            _lastResult = default!;
            _lastError = null;
            _submittedCount = 0;
            _queuedCount = 0;
            _replacedCount = 0;
            _completedCount = 0;
            _failedCount = 0;
            _resyncRequestedCount = 0;
        }

        private void Start(TLocalSubmitResult local)
        {
            var slot = FindAvailableSlot();
            if (slot < 0)
            {
                throw new InvalidOperationException("Remote input submit window is full.");
            }

            _lastError = null;
            var sequence = ++_nextSequence;
            try
            {
                _pending[slot] = _submitAsync(local, _timeout);
                _pendingSequences[slot] = sequence;
                _pendingCount++;
                _submittedCount++;
            }
            catch (Exception ex)
            {
                _pending[slot] = null;
                _pendingSequences[slot] = 0L;
                if (sequence >= _lastOutcomeSequence)
                {
                    _lastError = ex;
                    _lastOutcomeSequence = sequence;
                }
                _failedCount++;
            }
        }

        private int FindAvailableSlot()
        {
            for (var i = 0; i < _pending.Length; i++)
            {
                if (_pending[i] == null)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
