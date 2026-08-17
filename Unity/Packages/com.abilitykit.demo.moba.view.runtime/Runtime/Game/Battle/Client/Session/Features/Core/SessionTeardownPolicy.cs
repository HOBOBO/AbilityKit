using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow
{
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
    }
}
