namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Platform-neutral data required to materialize an actor in a presentation runtime.
    /// </summary>
    public readonly struct ActorSpawnData
    {
        public ActorSpawnKind Kind { get; }
        public int ActorId { get; }
        public int OwnerActorId { get; }
        public int EntityCode { get; }
        public int CharacterId { get; }
        public string Name { get; }
        public float PositionX { get; }
        public float PositionY { get; }
        public float PositionZ { get; }
        public float RotationY { get; }
        public float Scale { get; }
        public int TeamId { get; }
        public float MaxHp { get; }
        public float Hp { get; }
        public string PlayerId { get; }
        public int EntityVersion { get; }

        public bool IsProjectile => Kind == ActorSpawnKind.Projectile;

        public ActorSpawnData(
            ActorSpawnKind kind,
            int actorId,
            int ownerActorId,
            int entityCode,
            int characterId,
            string name,
            float x,
            float y,
            float z,
            float rotationY,
            float scale,
            int teamId,
            float maxHp,
            float hp,
            string playerId = null,
            int entityVersion = 1)
        {
            Kind = kind;
            ActorId = actorId;
            OwnerActorId = ownerActorId;
            EntityCode = entityCode;
            CharacterId = characterId;
            Name = name;
            PositionX = x;
            PositionY = y;
            PositionZ = z;
            RotationY = rotationY;
            Scale = scale;
            TeamId = teamId;
            MaxHp = maxHp;
            Hp = hp;
            PlayerId = playerId ?? actorId.ToString();
            EntityVersion = entityVersion > 0 ? entityVersion : 1;
        }

        public ActorSpawnData(
            int actorId,
            int entityCode,
            int characterId,
            string name,
            float x,
            float y,
            float z,
            float rotationY,
            float scale,
            int teamId,
            float maxHp,
            float hp,
            string playerId = null,
            int entityVersion = 1)
            : this(
                ActorSpawnKind.Character,
                actorId,
                ownerActorId: 0,
                entityCode,
                characterId,
                name,
                x,
                y,
                z,
                rotationY,
                scale,
                teamId,
                maxHp,
                hp,
                playerId,
                entityVersion)
        {
        }
    }

    public enum ActorSpawnKind
    {
        None = 0,
        Character = 1,
        Projectile = 2,
    }
}
