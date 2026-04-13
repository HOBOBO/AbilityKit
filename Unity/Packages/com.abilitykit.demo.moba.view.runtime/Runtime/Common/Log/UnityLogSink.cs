using System;
using AbilityKit.Core.Common.Log;
using UnityEngine;

namespace AbilityKit.Examples.Common.Log
{
    public sealed class UnityLogSink : ILogSink
    {
        public void Info(string message)
        {
            Debug.Log($"[Info] {message}");
        }

        public void Warning(string message)
        {
            Debug.LogWarning($"[Warning] {message}");
        }

        public void Error(string message)
        {
            Debug.LogError($"[Error] {message}");
        }

        public void Exception(Exception exception, string message = null)
        {
            if (string.IsNullOrEmpty(message))
            {
                Debug.LogException(exception);
            }
            else
            {
                Debug.LogException(exception);
                Debug.LogError($"[Exception] {message}");
            }
        }
    }
}
