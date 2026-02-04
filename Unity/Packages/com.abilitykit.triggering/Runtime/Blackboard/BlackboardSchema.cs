using System;

namespace AbilityKit.Triggering.Blackboard
{
    [Serializable]
    public sealed class BlackboardSchema
    {
        public int Version;
        public BlackboardKeySchemaEntry[] Keys;
        public BlackboardBoardSchemaEntry[] Boards;
    }

    [Serializable]
    public struct BlackboardKeySchemaEntry
    {
        public int Id;
        public string Name;
        public BlackboardKeyType Type;
        public bool Readable;
        public bool Writable;
    }

    [Serializable]
    public struct BlackboardBoardSchemaEntry
    {
        public int Id;
        public string Name;
    }
}
