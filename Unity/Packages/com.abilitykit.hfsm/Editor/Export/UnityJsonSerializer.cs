using UnityEngine;

namespace UnityHFSM.Editor.Export
{
    /// <summary>
    /// Unity 内置 JsonUtility 序列化器
    /// </summary>
    public class UnityJsonSerializer : IJsonSerializer
    {
        public string Name => "Unity JsonUtility";

        public string Serialize<T>(T obj, bool prettyPrint = false) where T : class
        {
            if (obj == null)
                return "null";

            return JsonUtility.ToJson(obj, prettyPrint);
        }

        public T Deserialize<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json))
                return null;

            return JsonUtility.FromJson<T>(json);
        }
    }
}
