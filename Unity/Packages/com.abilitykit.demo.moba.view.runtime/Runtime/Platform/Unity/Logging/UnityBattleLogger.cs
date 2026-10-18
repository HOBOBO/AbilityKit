using System;
using UnityEngine;

namespace AbilityKit.Game.Battle.Shared.Logging
{
    public sealed class UnityBattleLogger : IBattleLogger
    {
        public void Info(string message)
        {
            Debug.Log($"[Battle][Info] {message}");
        }

        public void Warning(string message)
        {
            Debug.LogWarning($"[Battle][Warning] {message}");
        }

        public void Error(string message)
        {
            Debug.LogError($"[Battle][Error] {message}");
        }

        public void Exception(Exception exception, string message = null)
        {
            if (!string.IsNullOrEmpty(message))
            {
                Debug.LogError($"[Battle][Exception] {message}");
            }

            if (exception != null)
            {
                Debug.LogException(exception);
            }
        }
    }
}
