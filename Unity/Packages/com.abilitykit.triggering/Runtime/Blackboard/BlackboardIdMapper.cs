using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Triggering.Blackboard
{
    public static class BlackboardIdMapper
    {
        public static int BoardId(string boardName)
        {
            return StableStringId.Get($"bb.board:{BlackboardNameUtil.Normalize(boardName)}");
        }

        public static int KeyId(string keyName)
        {
            return StableStringId.Get($"bb.key:{BlackboardNameUtil.Normalize(keyName)}");
        }
    }
}
