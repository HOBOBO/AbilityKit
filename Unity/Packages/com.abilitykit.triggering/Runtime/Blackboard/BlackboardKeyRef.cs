using System;

namespace AbilityKit.Triggering.Blackboard
{
    [Serializable]
    public struct BlackboardKeyRef
    {
        public int Id;
        public string Name;

        public BlackboardKeyRef(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public static BlackboardKeyRef FromName(string keyName)
        {
            var normalized = BlackboardNameUtil.Normalize(keyName);
            return new BlackboardKeyRef(BlackboardIdMapper.KeyId(normalized), normalized);
        }
    }
}
