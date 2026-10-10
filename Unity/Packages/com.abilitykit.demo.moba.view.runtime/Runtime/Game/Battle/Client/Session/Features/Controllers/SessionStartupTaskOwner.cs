using System;
using System.Threading.Tasks;

namespace AbilityKit.Game.Flow
{
    internal sealed class SessionStartupTaskOwner
    {
        private readonly object _gate = new object();
        private Task _startupTask = Task.CompletedTask;
        private Task _planAttachTask = Task.CompletedTask;
        private int _generation;
        private bool _attached;
        private bool _requested;
        private bool _planRequested;

        internal void Attach()
        {
            lock (_gate)
            {
                if (!_startupTask.IsCompleted || !_planAttachTask.IsCompleted)
                    throw new InvalidOperationException("Cannot attach while session startup is running.");
                _generation++;
                _attached = true;
                _requested = false;
                _planRequested = false;
            }
        }

        internal Task TrackPlanAttach(Func<int, Task> attach)
        {
            if (attach == null) throw new ArgumentNullException(nameof(attach));
            TaskCompletionSource<bool> completion;
            int generation;
            lock (_gate)
            {
                if (!_attached) return Task.CompletedTask;
                if (_planRequested) return _planAttachTask;
                _planRequested = true;
                generation = _generation;
                completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _planAttachTask = completion.Task;
            }
            _ = CompleteStartAsync(attach, generation, completion);
            return completion.Task;
        }

        internal Task RequestStart(Func<int, Task> start)
        {
            if (start == null) throw new ArgumentNullException(nameof(start));
            TaskCompletionSource<bool> completion;
            int generation;
            lock (_gate)
            {
                if (!_attached) return Task.CompletedTask;
                if (_requested) return _startupTask;
                _requested = true;
                generation = _generation;
                completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _startupTask = completion.Task;
            }
            _ = CompleteStartAsync(start, generation, completion);
            return completion.Task;
        }

        private static async Task CompleteStartAsync(
            Func<int, Task> start,
            int generation,
            TaskCompletionSource<bool> completion)
        {
            try
            {
                await (start(generation) ?? Task.CompletedTask);
                completion.TrySetResult(true);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }

        internal bool IsCurrent(int generation)
        {
            lock (_gate) return _attached && _generation == generation;
        }

        internal Task BeginDetachAsync()
        {
            lock (_gate)
            {
                _attached = false;
                _generation++;
                return Task.WhenAll(
                    AwaitCompletionIgnoringFailureAsync(_planAttachTask),
                    AwaitCompletionIgnoringFailureAsync(_startupTask));
            }
        }

        private static async Task AwaitCompletionIgnoringFailureAsync(Task task)
        {
            try { await task; }
            catch { }
        }
    }
}
