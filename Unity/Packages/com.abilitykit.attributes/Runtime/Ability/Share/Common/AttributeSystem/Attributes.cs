namespace AbilityKit.Core.Common.AttributeSystem
{
    public static class Attributes
    {
        public static AttributeId Attr(string name)
        {
            return AttributeRegistry.Instance.Request(name);
        }

        public static bool TryAttr(string name, out AttributeId id)
        {
            return AttributeRegistry.Instance.TryRequest(name, out id);
        }

        public static void FreezeRegistry()
        {
            AttributeRegistry.Instance.Freeze();
        }

        public static AttributeId Register(AttributeDef def)
        {
            return AttributeRegistry.Instance.Register(def);
        }
    }
}
