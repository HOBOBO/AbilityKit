using System;
using System.Threading.Tasks;

namespace AbilityKit.Game.Flow
{
    public enum BattleSessionFeatureLifecycleState
    {
        Idle = 0,
        Attached = 1,
        Detaching = 2,
        DetachFailed = 3,
        Detached = 4,
    }

    public sealed class BattleSessionLifecycleController<TContext>
    {
        private readonly object _gate = new object();
        private readonly Func<TContext, AsyncSessionTeardownStep[]> _createTeardownSteps;
        private Task _detachTask = Task.CompletedTask;
        private AsyncSessionTeardownStep[] _pendingTeardownSteps;
        private BattleSessionFeatureLifecycleState _state;
        private int _generation;

        public BattleSessionLifecycleController(
            Func<TContext, AsyncSessionTeardownStep[]> createTeardownSteps)
        {
            _createTeardownSteps = createTeardownSteps ??
                throw new ArgumentNullException(nameof(createTeardownSteps));
        }

        public BattleSessionFeatureLifecycleState State
        {
            get
            {
                lock (_gate) return _state;
            }
        }

        public int Generation
        {
            get
            {
                lock (_gate) return _generation;
            }
        }

        public Task DetachTask
        {
            get
            {
                lock (_gate) return _detachTask;
            }
        }

        public void Attach()
        {
            lock (_gate)
            {
                if (_state == BattleSessionFeatureLifecycleState.Detaching)
                {
                    throw new InvalidOperationException(
                        "Battle session feature cannot attach while teardown is still running.");
                }

                if (_state == BattleSessionFeatureLifecycleState.DetachFailed)
                {
                    throw new InvalidOperationException(
                        "Battle session feature cannot attach until the failed teardown is retried successfully.");
                }

                if (_state == BattleSessionFeatureLifecycleState.Attached)
                {
                    throw new InvalidOperationException(
                        "Battle session feature is already attached.");
                }

                _generation++;
                _state = BattleSessionFeatureLifecycleState.Attached;
                _detachTask = Task.CompletedTask;
                _pendingTeardownSteps = null;
            }
        }

        public Task DetachAsync(TContext context)
        {
            TaskCompletionSource<bool> completion;
            AsyncSessionTeardownStep[] pendingSteps;
            int generation;
            lock (_gate)
            {
                if (_state == BattleSessionFeatureLifecycleState.Detaching ||
                    _state == BattleSessionFeatureLifecycleState.Detached)
                {
                    return _detachTask;
                }

                if (_state == BattleSessionFeatureLifecycleState.Idle)
                {
                    _state = BattleSessionFeatureLifecycleState.Detached;
                    return _detachTask;
                }

                _state = BattleSessionFeatureLifecycleState.Detaching;
                generation = _generation;
                pendingSteps = _pendingTeardownSteps;
                completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _detachTask = completion.Task;
            }

            _ = CompleteDetachAsync(context, generation, pendingSteps, completion);
            return completion.Task;
        }

        private async Task CompleteDetachAsync(
            TContext context,
            int generation,
            AsyncSessionTeardownStep[] pendingSteps,
            TaskCompletionSource<bool> completion)
        {
            try
            {
                var steps = pendingSteps ?? _createTeardownSteps(context);
                await SessionTeardownPolicy.ExecuteAsync(steps).ConfigureAwait(false);
                Complete(generation, completion, null, null);
            }
            catch (SessionTeardownException exception)
            {
                Complete(
                    generation,
                    completion,
                    exception,
                    exception.FailedSteps);
            }
            catch (Exception exception)
            {
                Complete(generation, completion, exception, pendingSteps);
            }
        }

        private void Complete(
            int generation,
            TaskCompletionSource<bool> completion,
            Exception failure,
            AsyncSessionTeardownStep[] pendingSteps)
        {
            lock (_gate)
            {
                if (_generation == generation)
                {
                    _state = failure == null
                        ? BattleSessionFeatureLifecycleState.Detached
                        : BattleSessionFeatureLifecycleState.DetachFailed;
                    _pendingTeardownSteps = pendingSteps;
                }

                if (failure == null)
                {
                    completion.TrySetResult(true);
                }
                else
                {
                    completion.TrySetException(failure);
                }
            }
        }
    }
}
