using System;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Triggering.Blackboard
{
    public static class BlackboardIdNameRegistryExtensions
    {
        public static int RegisterBoard(this IIdNameRegistry registry, string boardName)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var normalized = BlackboardNameUtil.Normalize(boardName);
            var id = BlackboardIdMapper.BoardId(normalized);
            registry.RegisterBoard(id, normalized);
            return id;
        }

        public static int RegisterKey(this IIdNameRegistry registry, string keyName)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var normalized = BlackboardNameUtil.Normalize(keyName);
            var id = BlackboardIdMapper.KeyId(normalized);
            registry.RegisterKey(id, normalized);
            return id;
        }

        public static void RegisterBoard(this IIdNameRegistry registry, int boardId, string boardName)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var normalized = BlackboardNameUtil.Normalize(boardName);
            registry.RegisterBoard(boardId, normalized);
        }

        public static void RegisterKey(this IIdNameRegistry registry, int keyId, string keyName)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var normalized = BlackboardNameUtil.Normalize(keyName);
            registry.RegisterKey(keyId, normalized);
        }
    }
}
