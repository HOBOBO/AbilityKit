namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO
{
    public sealed class PresentationTemplateMO_Standalone
    {
        public int Id { get; }
        public string Name { get; }

        public int Kind { get; }
        public int AssetId { get; }
        public int DefaultDurationMs { get; }

        public int AttachMode { get; }
        public string Socket { get; }
        public bool Follow { get; }

        public int StackPolicy { get; }
        public int StopPolicy { get; }

        public float Scale { get; }
        public float ColorR { get; }
        public float ColorG { get; }
        public float ColorB { get; }
        public float ColorA { get; }
        public float Radius { get; }
        public float OffsetX { get; }
        public float OffsetY { get; }
        public float OffsetZ { get; }

        public PresentationTemplateMO_Standalone(PresentationTemplateDTO dto)
        {
            Id = dto != null ? dto.Id : 0;
            Name = dto != null ? dto.Name : null;

            Kind = dto != null ? dto.Kind : 0;
            AssetId = dto != null ? dto.AssetId : 0;
            DefaultDurationMs = dto != null ? dto.DefaultDurationMs : 0;

            AttachMode = dto != null ? dto.AttachMode : 0;
            Socket = dto != null ? dto.Socket : null;
            Follow = dto != null && dto.Follow;

            StackPolicy = dto != null ? dto.StackPolicy : 0;
            StopPolicy = dto != null ? dto.StopPolicy : 0;

            Scale = dto != null ? dto.Scale : 0f;
            ColorR = dto != null ? dto.ColorR : 0f;
            ColorG = dto != null ? dto.ColorG : 0f;
            ColorB = dto != null ? dto.ColorB : 0f;
            ColorA = dto != null ? dto.ColorA : 0f;
            Radius = dto != null ? dto.Radius : 0f;
            OffsetX = dto != null ? dto.OffsetX : 0f;
            OffsetY = dto != null ? dto.OffsetY : 0f;
            OffsetZ = dto != null ? dto.OffsetZ : 0f;
        }
    }
}
