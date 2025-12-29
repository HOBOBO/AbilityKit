using System;

namespace NBC
{
    public static class Log
    {
        public static bool Open = false;

        static Log()
        {
#if UNITY_EDITOR
            Open = true;
#endif
        }

        public static void I(object message)
        {
            if (Open)
                UnityEngine.Debug.Log(message);
        }

        public static void W(object message)
        {
            if (Open)
                UnityEngine.Debug.LogWarning(message);
        }

        public static void E(object message)
        {
            if (Open)
                UnityEngine.Debug.LogError(message);
        }

        public static void Exception(Exception exception)
        {
            if (Open)
                UnityEngine.Debug.LogException(exception);
        }

        public static void Exception(Exception exception, UnityEngine.Object context)
        {
            if (Open)
                UnityEngine.Debug.LogException(exception, context);
        }
    }
}