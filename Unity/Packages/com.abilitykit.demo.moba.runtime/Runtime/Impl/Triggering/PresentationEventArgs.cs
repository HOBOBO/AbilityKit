using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Impl.Triggering
{
    public sealed class PresentationEventArgs
    {
        public string EventId;

        public int TemplateId;
        public string RequestKey;
        public int DurationMsOverride;

        public int[] Targets;
        public Vec3[] Positions;

        public long SourceContextId;

        public object Scale;
        public object Radius;
        public object Color;
    }
}
