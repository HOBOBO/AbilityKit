using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AbilityKit.Game.Flow
{
    internal readonly struct AsyncSessionTeardownStep
    {
        internal AsyncSessionTeardownStep(string name, Func<Task> cleanup)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Cleanup = cleanup ?? throw new ArgumentNullException(nameof(cleanup));
        }

        internal AsyncSessionTeardownStep(string name, Action cleanup)
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

        internal string Name { get; }

        internal Func<Task> Cleanup { get; }
    }

    internal readonly struct SessionTeardownStep
    {
        internal SessionTeardownStep(string name, Action cleanup)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Cleanup = cleanup ?? throw new ArgumentNullException(nameof(cleanup));
        }

        internal string Name { get; }

        internal Action Cleanup { get; }
    }

    internal static class SessionTeardownPolicy
    {
        internal static void Execute(
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

        internal static async Task ExecuteAsync(params AsyncSessionTeardownStep[] steps)
        {
            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            var failures = new List<Exception>(steps.Length);
            for (var i = 0; i < steps.Length; i++)
            {
                var step = steps[i];
                try
                {
                    await (step.Cleanup() ?? Task.CompletedTask).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    failures.Add(new InvalidOperationException(
                        $"Session teardown step failed: {step.Name}.",
                        exception));
                }
            }

            if (failures.Count > 0)
            {
                throw new AggregateException(
                    "Session teardown completed with one or more failures.",
                    failures);
            }
        }
    }
}
