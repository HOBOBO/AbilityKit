namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>Platform-neutral presentation contract for an area lifecycle event.</summary>
    public readonly struct AreaEventData
    {
        public AreaPresentationEventKind Kind { get; }
        public int AreaId { get; }
        public int OwnerActorId { get; }
        public int TemplateId { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float Radius { get; }

        public AreaEventData(
            AreaPresentationEventKind kind,
            int areaId,
            int ownerActorId,
            int templateId,
            float x,
            float y,
            float z,
            float radius)
        {
            Kind = kind;
            AreaId = areaId;
            OwnerActorId = ownerActorId;
            TemplateId = templateId;
            X = x;
            Y = y;
            Z = z;
            Radius = radius;
        }
    }

    public enum AreaPresentationEventKind
    {
        Unknown = 0,
        Spawn = 1,
        Expire = 2,
    }
}
