using System;
using System.Threading.Tasks;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleFlowExitCoordinator
    {
        private readonly Func<Task> _clearFeatures;
        private readonly Action _enterEnd;
        private readonly Action _returnLobby;
        private readonly Action<Exception> _cleanupFailed;
        private Task _pending;
        private Exception _failure;
        private bool _returnToLobby;
        private bool _cleanupFailedReported;

        public BattleFlowExitCoordinator(Func<Task> clearFeatures, Action enterEnd, Action returnLobby,
            Action<Exception> cleanupFailed = null)
        {
            _clearFeatures = clearFeatures ?? throw new ArgumentNullException(nameof(clearFeatures));
            _enterEnd = enterEnd ?? throw new ArgumentNullException(nameof(enterEnd));
            _returnLobby = returnLobby ?? throw new ArgumentNullException(nameof(returnLobby));
            _cleanupFailed = cleanupFailed;
        }

        public bool IsPending => _pending != null;
        public bool HasFailed => _failure != null;
        public Task PendingTask => _pending ?? (_failure != null ? Task.FromException(_failure) : Task.CompletedTask);

        public void RequestEnd()
        {
            if (_pending != null || _failure != null) return;
            try { _pending = _clearFeatures() ?? Task.CompletedTask; }
            catch (Exception ex)
            {
                _pending = Task.FromException(ex);
            }
        }

        public void RequestReturnLobby()
        {
            _returnToLobby = true;
            RequestEnd();
        }

        public bool Poll()
        {
            if (_pending == null) return false;
            if (!_pending.IsCompleted) return true;

            try { _pending.GetAwaiter().GetResult(); }
            catch (Exception ex)
            {
                _failure = ex;
                _pending = null;
                if (!_cleanupFailedReported)
                {
                    _cleanupFailedReported = true;
                    _cleanupFailed?.Invoke(ex);
                }
                return true;
            }
            var returnToLobby = _returnToLobby;
            _pending = null;
            _returnToLobby = false;
            if (returnToLobby) _returnLobby();
            else _enterEnd();
            return false;
        }
    }
}
