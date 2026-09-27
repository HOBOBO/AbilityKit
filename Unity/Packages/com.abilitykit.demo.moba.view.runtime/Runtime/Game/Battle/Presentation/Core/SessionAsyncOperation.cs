using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace AbilityKit.Game.Flow
{
    public static class SessionAsyncOperation
    {
        public static void RequireCompleted(Task task, string operationName)
        {
            if (task == null) return;
            if (!task.IsCompleted)
            {
                throw new InvalidOperationException(
                    $"{operationName} is asynchronous. Await its async API instead of using the synchronous compatibility entry point.");
            }

            if (task.IsCanceled)
            {
                throw new TaskCanceledException(task);
            }

            if (!task.IsFaulted) return;

            var exception = task.Exception;
            if (exception.InnerExceptions.Count == 1)
            {
                ExceptionDispatchInfo.Capture(exception.InnerExceptions[0]).Throw();
            }

            throw exception;
        }
    }
}
