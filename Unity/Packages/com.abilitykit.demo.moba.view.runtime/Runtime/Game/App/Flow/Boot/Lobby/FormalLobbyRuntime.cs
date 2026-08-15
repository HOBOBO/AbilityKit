using System;
using System.Threading;
using System.Threading.Tasks;

namespace AbilityKit.Game.Flow
{
    internal readonly struct LobbyOperationContext
    {
        public LobbyOperationContext(
            int attachGeneration,
            int operationGeneration,
            CancellationToken cancellationToken)
        {
            AttachGeneration = attachGeneration;
            OperationGeneration = operationGeneration;
            CancellationToken = cancellationToken;
        }

        public int AttachGeneration { get; }
        public int OperationGeneration { get; }
        public CancellationToken CancellationToken { get; }
    }

    internal sealed class FormalLobbyRuntime : IDisposable
    {
        private CancellationTokenSource _lifetime;
        private Task _operationTask = Task.CompletedTask;
        private int _attachGeneration;
        private int _operationGeneration;

        public bool IsAttached => _lifetime != null;
        public bool IsOperationBusy => _operationTask != null && !_operationTask.IsCompleted;
        public string OperationLabel { get; private set; } = string.Empty;
        public string OperationError { get; private set; } = string.Empty;
        public string PreparedRoomId { get; private set; } = string.Empty;
        public string AutomaticStartRoomId { get; private set; } = string.Empty;
        public bool InitializationStarted { get; private set; }
        public bool AutomaticCreateAttempted { get; private set; }

        public void Attach()
        {
            DetachCore();
            _attachGeneration++;
            _operationGeneration = 0;
            _operationTask = Task.CompletedTask;
            OperationLabel = string.Empty;
            OperationError = string.Empty;
            ResetCommandState();
            _lifetime = new CancellationTokenSource();
        }

        public void Detach()
        {
            DetachCore();
            _attachGeneration++;
            _operationGeneration++;
            _operationTask = Task.CompletedTask;
            OperationLabel = string.Empty;
            OperationError = string.Empty;
            ResetCommandState();
        }

        public bool StartOperation(
            string label,
            Func<LobbyOperationContext, Task> operation)
        {
            var lifetime = _lifetime;
            if (IsOperationBusy || operation == null || lifetime == null) return false;

            var operationContext = new LobbyOperationContext(
                _attachGeneration,
                ++_operationGeneration,
                lifetime.Token);
            OperationLabel = label ?? string.Empty;
            OperationError = string.Empty;
            _operationTask = RunOperationAsync(operation, operationContext);
            return true;
        }

        public bool IsCurrent(in LobbyOperationContext operationContext)
        {
            return _lifetime != null &&
                   _attachGeneration == operationContext.AttachGeneration &&
                   _operationGeneration == operationContext.OperationGeneration;
        }

        public int CaptureAttachmentGeneration()
        {
            return _attachGeneration;
        }

        public bool IsCurrentAttachment(int attachGeneration)
        {
            return _lifetime != null && _attachGeneration == attachGeneration;
        }

        public bool TryBeginInitialization()
        {
            if (InitializationStarted) return false;
            InitializationStarted = true;
            return true;
        }

        public void MarkPrepared(string roomId)
        {
            PreparedRoomId = roomId ?? string.Empty;
        }

        public void ClearPrepared()
        {
            PreparedRoomId = string.Empty;
        }

        public void MarkAutomaticStart(string roomId)
        {
            AutomaticStartRoomId = roomId ?? string.Empty;
        }

        public void ClearAutomaticStart()
        {
            AutomaticStartRoomId = string.Empty;
        }

        public void MarkAutomaticCreateAttempted()
        {
            AutomaticCreateAttempted = true;
        }

        public void CancelLifetime()
        {
            DetachCore();
            _attachGeneration++;
            _operationGeneration++;
            _operationTask = Task.CompletedTask;
            OperationLabel = string.Empty;
            OperationError = string.Empty;
        }

        public void Dispose()
        {
            Detach();
        }

        private async Task RunOperationAsync(
            Func<LobbyOperationContext, Task> operation,
            LobbyOperationContext operationContext)
        {
            try
            {
                await operation(operationContext);
            }
            catch (OperationCanceledException)
                when (operationContext.CancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                if (IsCurrent(operationContext))
                {
                    OperationError = ex.Message;
                }
            }
            finally
            {
                if (IsCurrent(operationContext))
                {
                    OperationLabel = string.Empty;
                }
            }
        }

        private void DetachCore()
        {
            var lifetime = _lifetime;
            _lifetime = null;
            lifetime?.Cancel();
            lifetime?.Dispose();
        }

        private void ResetCommandState()
        {
            PreparedRoomId = string.Empty;
            AutomaticStartRoomId = string.Empty;
            InitializationStarted = false;
            AutomaticCreateAttempted = false;
        }
    }
}
