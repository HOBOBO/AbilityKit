#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace Emilia.Kit.Editor
{
    [Serializable]
    public class UnityAssetSerializationPack
    {
        public Type type;
        public byte[] data;
        public List<Object> unityObjects;

        public List<UnityAssetSerializationPack> children;
    }
}
#endif