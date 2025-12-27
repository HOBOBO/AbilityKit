//------------------------------------------------------------
//        File:  Blackboard.cs
//       Brief:  Blackboard
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-09
//============================================================

using System.Collections.Generic;

namespace BTCore.Runtime.Blackboards
{
    public class Blackboard
    {
        public List<BlackboardValue> Values { get; set; } = new();

        public BlackboardValue<T> Find<T>(string name) {
            var foundKey = Values.Find(key => key.Name == name);

            if (foundKey == null) {
                BTLogger.Error($"Find blackboard value failed, value Name: {name}");
                return null;
            }

            if (foundKey is not BlackboardValue<T> blackboardValue) {
                BTLogger.Error($"Find blackboard value failed, expected: {typeof(T)} got: {foundKey.Type}");
                return null;
            }

            return blackboardValue;
        }

        public T GetValue<T>(string name) {
            var blackboardValue = Find<T>(name);
            return blackboardValue != null ? blackboardValue.Value : default;
        }

        public void SetValue<T>(string name, T value) {
            var foundValue = Find<T>(name);
            if (foundValue != null) {
                foundValue.Value = value;
                return;
            }

            var blackboardKey = new BlackboardValue<T>(name);
            blackboardKey.Value = value;
            Values.Add(blackboardKey);
        }
    }
}