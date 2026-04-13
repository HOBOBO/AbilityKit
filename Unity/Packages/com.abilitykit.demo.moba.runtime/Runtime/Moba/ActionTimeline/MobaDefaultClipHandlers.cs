namespace AbilityKit.Demo.Moba.ActionTimeline
{
    public static class MobaDefaultClipHandlers
    {
        public static MobaClipHandlerRegistry CreateRegistry()
        {
            var registry = new MobaClipHandlerRegistry();

            registry.Register(TriggerLogHandler.ClipType, new TriggerLogHandler());

            return registry;
        }
    }
}
