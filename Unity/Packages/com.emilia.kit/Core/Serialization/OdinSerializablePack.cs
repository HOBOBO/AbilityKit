using System;
using System.Collections.Generic;
using Sirenix.Serialization;
using UnityEngine;
using Object = UnityEngine.Object;
using SerializationUtility = Sirenix.Serialization.SerializationUtility;

namespace Emilia.Kit
{
    [Serializable]
    public class OdinSerializablePack<T> : ISerializationCallbackReceiver
    {
        [SerializeField]
        private string byteString;

        [SerializeField]
        private List<Object> unityObjects = new List<Object>();

        [NonSerialized]
        public T serializableObject;

        public void OnBeforeSerialize()
        {
            if (this.serializableObject == null) return;
            unityObjects.Clear();
            byte[] bytes = TagSerializationUtility.IgnoreTagSerializeValue(serializableObject, DataFormat.Binary, out unityObjects, SerializeTagDefine.DefaultIgnoreTag);
            byteString = Convert.ToBase64String(bytes);
        }

        public void OnAfterDeserialize()
        {
            if (string.IsNullOrEmpty(byteString)) return;
            byte[] bytes = Convert.FromBase64String(byteString);
            serializableObject = SerializationUtility.DeserializeValue<T>(bytes, DataFormat.Binary, unityObjects);
        }
    }

    public static class OdinSerializablePackUtility
    {
        public static string ToJson<V>(V value)
        {
            OdinSerializablePack<V> pack = new OdinSerializablePack<V>();
            pack.serializableObject = value;
            return JsonUtility.ToJson(pack);
        }

        public static V FromJson<V>(string json)
        {
            OdinSerializablePack<V> pack = JsonUtility.FromJson<OdinSerializablePack<V>>(json);
            return pack.serializableObject;
        }
    }
}