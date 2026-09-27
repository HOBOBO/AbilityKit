using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace AbilityKit.Game.Flow
{
    public readonly struct AsyncSessionTeardownStep
    {
        public AsyncSessionTeardownStep(string name, Func<Task> cleanup)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Cleanup = cleanup ?? throw new ArgumentNullException(nameof(cleanup));
        }

        public AsyncSessionTeardownStep(string name, Action cleanup)
            : this(
                name,
                () =>
                {
                    cleanup?.Invoke();
                    return Task.CompletedTask;
                })
        {
            if (cleanup == null) throw new ArgumentNullException(nameof(cleanup));
        }

        public string Name { get; }

        public Func<Task> Cleanup { get; }
    }

    public readonly struct SessionTeardownStep
    {
        public SessionTeardownStep(string name, Action cleanup)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Cleanup = cleanup ?? throw new ArgumentNullException(nameof(cleanup));
        }

        public string Name { get; }

        public Action Cleanup { get; }
    }

    public sealed class SessionTeardownException : AggregateException
    {
        internal SessionTeardownException(
            AsyncSessionTeardownStep[] failedSteps,
            IReadOnlyCollection<Exception> failures)
            : base(
                "Session teardown completed with one or more failures.",
                failures)
        {
            FailedSteps = failedSteps ??
                throw new ArgumentNullException(nameof(failedSteps));
            FailedStepNames = Array.AsReadOnly(
                Array.ConvertAll(failedSteps, step => step.Name));
        }

        internal AsyncSessionTeardownStep[] FailedSteps { get; }

        public ReadOnlyCollection<string> FailedStepNames { get; }
    }

    public static class SessionTeardownPolicy
    {
        public static void Execute(
            Action<string, Exception> failureHandler,
            params SessionTeardownStep[] steps)
        {
            if (failureHandler == null)
            {
                throw new ArgumentNullException(nameof(failureHandler));
            }

            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            var failures =
                new List<KeyValuePair<string, Exception>>(steps.Length);
            for (var i = 0; i < steps.Length; i++)
            {
                var step = steps[i];
                try
                {
                    step.Cleanup();
                }
                catch (Exception exception)
                {
                    failures.Add(
                        new KeyValuePair<string, Exception>(
                            step.Name,
                            exception));
                }
            }

            for (var i = 0; i < failures.Count; i++)
            {
                var failure = failures[i];
                failureHandler(failure.Key, failure.Value);
            }
        }

        public static async Task ExecuteAsync(
            params AsyncSessionTeardownStep[] steps)
        {
            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            var failedSteps =
                new List<AsyncSessionTeardownStep>(steps.Length);
            var failures = new List<Exception>(steps.Length);
            for (var i = 0; i < steps.Length; i++)
            {
                var step = steps[i];
                try
                {
                    await (step.Cleanup() ?? Task.CompletedTask)
                        .ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    failedSteps.Add(step);
                    failures.Add(new InvalidOperationException(
                        $"Session teardown step failed: {step.Name}.",
                        exception));
                }
            }

            if (failures.Count > 0)
            {
                throw new SessionTeardownException(
                    failedSteps.ToArray(),
                    failures);
            }
        }
    }
}
