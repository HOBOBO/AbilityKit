using System;
using System.Collections.Generic;
using System.IO;
using Sirenix.Serialization;

namespace Emilia.Kit.Editor
{
    public class OpenScriptCache
    {
        private static OpenScriptCache instance;

        [NonSerialized, OdinSerialize]
        public Dictionary<string, ScriptInfo> scriptInfos = new Dictionary<string, ScriptInfo>();

        [NonSerialized, OdinSerialize]
        public Dictionary<string, TypeInfo> typeInfos = new Dictionary<string, TypeInfo>();

        public static OpenScriptCache Get()
        {
            string path = $"{EditorAssetKit.dataParentPath}/Library/OpenScriptCache.bytes";

            if (instance != null) return instance;

            if (File.Exists(path) == false) instance = new OpenScriptCache();
            else
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    instance = SerializationUtility.DeserializeValue<OpenScriptCache>(bytes, DataFormat.Binary);
                }
                catch
                {
                    instance = new OpenScriptCache();
                }
            }

            return instance;
        }

        public static void Save()
        {
            string path = $"{EditorAssetKit.dataParentPath}/Library/OpenScriptCache.bytes";
            var bytes = SerializationUtility.SerializeValue(instance, DataFormat.Binary);
            File.WriteAllBytes(path, bytes);
        }
    }

    [Serializable]
    public class TypeInfo
    {
        public string typeFullName;
        public string guid;
        public int line;
    }

    [Serializable]
    public class ScriptInfo
    {
        public string guid;
        public int hash;
        public List<TypeInfo> typeInfos = new List<TypeInfo>();
    }
}