using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services.EntityConstruction
{
    public interface IMobaActorSpawnCoordinator : IService
    {
        bool TryPrepareBatch(
            IReadOnlyList<MobaActorSpawnRequest> requests,
            out MobaActorSpawnBatchResult result);

        void PublishBatch(IReadOnlyList<MobaActorSpawnResult> actors);

        void RollbackBatch(IReadOnlyList<MobaActorSpawnResult> actors);

        bool TrySpawnBatch(
            IReadOnlyList<MobaActorSpawnRequest> requests,
            out MobaActorSpawnBatchResult result);
    }

    public readonly struct MobaActorSpawnBatchResult
    {
        public readonly bool Success;
        public readonly MobaActorSpawnResult[] Actors;
        public readonly int FailedIndex;
        public readonly string Error;

        public MobaActorSpawnBatchResult(
            bool success,
            MobaActorSpawnResult[] actors,
            int failedIndex,
            string error)
        {
            Success = success;
            Actors = actors ?? Array.Empty<MobaActorSpawnResult>();
            FailedIndex = failedIndex;
            Error = error;
        }

        public static MobaActorSpawnBatchResult Failed(int failedIndex, string error)
        {
            return new MobaActorSpawnBatchResult(
                false,
                Array.Empty<MobaActorSpawnResult>(),
                failedIndex,
                error);
        }
    }

    [WorldService(typeof(MobaActorSpawnCoordinator))]
    [WorldService(typeof(IMobaActorSpawnCoordinator))]
    public sealed class MobaActorSpawnCoordinator : IMobaActorSpawnCoordinator
    {
        [WorldInject] private IMobaActorSpawnTransactionService _spawns = null;

        public MobaActorSpawnCoordinator()
        {
        }

        public MobaActorSpawnCoordinator(IMobaActorSpawnTransactionService spawns)
        {
            _spawns = spawns ?? throw new ArgumentNullException(nameof(spawns));
        }

        public bool TryPrepareBatch(
            IReadOnlyList<MobaActorSpawnRequest> requests,
            out MobaActorSpawnBatchResult result)
        {
            if (_spawns == null)
            {
                result = MobaActorSpawnBatchResult.Failed(-1, "actor spawn transaction service is required");
                return false;
            }
            if (requests == null || requests.Count == 0)
            {
                result = MobaActorSpawnBatchResult.Failed(-1, "spawn requests are required");
                return false;
            }

            var actors = new MobaActorSpawnResult[requests.Count];
            var builtCount = 0;
            for (var i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                var actor = default(MobaActorSpawnResult);
                var spawned = false;
                string exceptionError = null;
                try
                {
                    spawned = request != null &&
                        _spawns.TrySpawnUnpublished(in request, out actor) &&
                        actor.Success;
                }
                catch (Exception ex)
                {
                    exceptionError = ex.Message;
                }

                if (!spawned)
                {
                    var error = request == null
                        ? "spawn request is required"
                        : exceptionError ?? actor.Error;
                    var rollbackError = Rollback(actors, builtCount);
                    if (!string.IsNullOrEmpty(rollbackError))
                    {
                        error = $"{error}; rollback failed: {rollbackError}";
                    }
                    result = MobaActorSpawnBatchResult.Failed(i, error ?? $"actor spawn failed at index {i}");
                    return false;
                }

                actors[builtCount++] = actor;
            }

            result = new MobaActorSpawnBatchResult(true, actors, -1, null);
            return true;
        }

        public void PublishBatch(IReadOnlyList<MobaActorSpawnResult> actors)
        {
            if (_spawns == null) throw new InvalidOperationException("actor spawn transaction service is required");
            if (actors == null) return;
            for (var i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                _spawns.Publish(in actor);
            }
        }

        public void RollbackBatch(IReadOnlyList<MobaActorSpawnResult> actors)
        {
            if (_spawns == null || actors == null) return;
            for (var i = actors.Count - 1; i >= 0; i--)
            {
                var actor = actors[i];
                _spawns.Rollback(in actor);
            }
        }

        public bool TrySpawnBatch(
            IReadOnlyList<MobaActorSpawnRequest> requests,
            out MobaActorSpawnBatchResult result)
        {
            if (!TryPrepareBatch(requests, out result)) return false;

            try
            {
                PublishBatch(result.Actors);
                return true;
            }
            catch (Exception ex)
            {
                var error = $"actor spawn publish failed: {ex.Message}";
                var rollbackError = Rollback(result.Actors, result.Actors.Length);
                if (!string.IsNullOrEmpty(rollbackError))
                {
                    error = $"{error}; rollback failed: {rollbackError}";
                }
                result = MobaActorSpawnBatchResult.Failed(result.Actors.Length, error);
                return false;
            }
        }

        private string Rollback(MobaActorSpawnResult[] actors, int count)
        {
            string firstError = null;
            for (var i = count - 1; i >= 0; i--)
            {
                try
                {
                    _spawns.Rollback(in actors[i]);
                }
                catch (Exception ex)
                {
                    if (string.IsNullOrEmpty(firstError)) firstError = ex.Message;
                }
            }
            return firstError;
        }

        public void Dispose()
        {
        }
    }
}
