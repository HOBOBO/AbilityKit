namespace AbilityKit.Triggering.Editor.IdMapping
{
    public static class BlackboardIdMapper
    {
        public static int BoardId(string boardId)
        {
            return StableStringId.Get($"bb.board:{boardId}");
        }

        public static int KeyId(string key)
        {
            return StableStringId.Get($"bb.key:{key}");
        }
    }
}
