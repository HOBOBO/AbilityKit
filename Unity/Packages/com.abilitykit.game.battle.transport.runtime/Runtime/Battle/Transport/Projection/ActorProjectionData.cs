namespace AbilityKit.Game.Battle.Transport.Projection
{
    /// <summary>
    /// 逻辑层 actor 状态的标准投影数据。
    ///
    /// 规范（2026-07-24）：
    /// 这是逻辑 world → 表现层 / 网关层的**唯一标准中间格式**。
    /// 无论数据走哪条管道（snapshot 通道 / 预测通道 / 网关转发 / 录像），
    /// 逻辑层的状态都先提取为 <see cref="ActorProjectionData"/>，
    /// 再由各消费方适配为自己的格式。
    ///
    /// 字段分层：
    /// - 核心层（所有投影必须填充）：ActorId + Position + Rotation
    /// - 扩展层（状态同步/哈希校验时填充）：Scale + Hp + TeamId + Velocity
    /// - Spawn 层（仅创建时填充）：Kind + Code + OwnerNetId
    ///
    /// 消费方通过 <see cref="FieldMask"/> 判断哪些字段有效，
    /// 或直接读全部字段（无效字段为 default）。
    /// </summary>
    public readonly struct ActorProjectionData
    {
        // === 核心层 ===
        public readonly int ActorId;
        public readonly float PosX, PosY, PosZ;
        public readonly float RotX, RotY, RotZ, RotW;

        // === 扩展层 ===
        public readonly float ScaleX, ScaleY, ScaleZ;
        public readonly float Hp;
        public readonly float HpMax;
        public readonly int TeamId;
        public readonly float VelX, VelZ;

        // === Spawn 层 ===
        public readonly int Kind;       // SpawnEntityKind.Character=1 / Projectile=2；更新时为 0
        public readonly int Code;       // 配置 ID（heroId / projectileCode）；更新时为 0
        public readonly int OwnerNetId; // 投射物的拥有者；角色为 0

        // === 字段掩码 ===
        public readonly ActorProjectionFields Fields;

        public ActorProjectionData(
            int actorId,
            float posX, float posY, float posZ,
            float rotX, float rotY, float rotZ, float rotW,
            float scaleX, float scaleY, float scaleZ,
            float hp, float hpMax,
            int teamId,
            float velX, float velZ,
            int kind, int code, int ownerNetId,
            ActorProjectionFields fields)
        {
            ActorId = actorId;
            PosX = posX; PosY = posY; PosZ = posZ;
            RotX = rotX; RotY = rotY; RotZ = rotZ; RotW = rotW;
            ScaleX = scaleX; ScaleY = scaleY; ScaleZ = scaleZ;
            Hp = hp; HpMax = hpMax;
            TeamId = teamId;
            VelX = velX; VelZ = velZ;
            Kind = kind; Code = code; OwnerNetId = ownerNetId;
            Fields = fields;
        }

        /// <summary>是否包含指定字段层。</summary>
        public bool Has(ActorProjectionFields field) => (Fields & field) != 0;
    }

    /// <summary>
    /// 投影字段掩码。消费方用它判断哪些字段有效。
    /// </summary>
    [System.Flags]
    public enum ActorProjectionFields
    {
        None      = 0,
        Core      = 1 << 0,  // ActorId + Position + Rotation
        Scale     = 1 << 1,
        Hp        = 1 << 2,
        TeamId    = 1 << 3,
        Velocity  = 1 << 4,
        Spawn     = 1 << 5,  // Kind + Code + OwnerNetId

        /// <summary>全量投影（状态同步/哈希校验用）。</summary>
        FullState = Core | Scale | Hp | TeamId | Velocity,

        /// <summary>Spawn 投影（创建时用）。</summary>
        SpawnInfo = Core | Spawn | Hp | TeamId,

        /// <summary>所有字段。</summary>
        All       = FullState | Spawn,
    }
}
