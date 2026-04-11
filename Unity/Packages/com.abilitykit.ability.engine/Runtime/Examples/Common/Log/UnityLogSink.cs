using System;
using AbilityKit.Core.Common.Log;
using UnityEngine;

namespace AbilityKit.Examples.Common.Log
{
    public sealed class UnityLogSink : ILogSink
    {
        public void Info(string message) => Debug.Log(message);

        public void Warning(string message) => Debug.LogWarning(message);

        public void Error(string message) => Debug.LogError(message);

        public void Exception(Exception exception, string message = null)
        {
            if (!string.IsNullOrEmpty(message))
            {
                Debug.LogError($"{message}:{exception}");
            }

            Debug.LogException(exception);
        }
    }
}
