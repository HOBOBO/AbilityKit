//------------------------------------------------------------
//        File:  BTLogger.cs
//       Brief:  BTLogger
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-09-29
//============================================================

using System;

namespace BTCore.Runtime
{
    public static class BTLogger
    {
        public static Action<string, BTLogType> OnLogReceived;

        public static void Debug(string text, params object[] args) {
            var message = string.Format(text, args);
            OnLogReceived?.Invoke(message, BTLogType.Debug);
        }
        
        public static void Warning(string text, params object[] args) {
            var message = string.Format(text, args);
            OnLogReceived?.Invoke(message, BTLogType.Warning);
        }
        
        public static void Error(string text, params object[] args) {
            var message = string.Format(text, args);
            OnLogReceived?.Invoke(message, BTLogType.Error);
        }
    }
}
