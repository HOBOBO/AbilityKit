using System;
using BTCore.Runtime;
using Newtonsoft.Json;

namespace BTCore.Editor
{
    [Serializable]
    public class BehaviorSource
    {
        public string TreeJson;
        public BTree Tree { get; private set; }
        
        public void Load()
        {
            if (string.IsNullOrEmpty(TreeJson))
            {
                return;
            }
            Tree = JsonConvert.DeserializeObject<BTree>(TreeJson, BTDef.SerializerSettingsAuto);
        }
    }
}