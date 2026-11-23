namespace AbilityKit.Ability.StateSync
{
    /// <summary>
    /// 现有服务器状态描述值；只记录调用方传入的信息，不进行准入、应用或基线验证。
    /// 复制结构不会复制状态内容数组。
    /// </summary>
    public readonly struct ServerGameState
    {
        /// <summary>
        /// 调用方提供的帧号，不表示客户端已确认的输入。
        /// </summary>
        public int Frame { get; }
        /// <summary>
        /// 调用方提供的时间值；本类型不决定时钟来源或单位。
        /// </summary>
        public long Timestamp { get; }
        /// <summary>
        /// 调用方提供的原始状态内容引用，可能为空；共享期间应保持只读。
        /// </summary>
        public byte[] StateData { get; }
        /// <summary>
        /// 调用方提供的状态哈希，不在本类型内计算或校验。
        /// </summary>
        public StateHash Hash { get; }
        /// <summary>
        /// 调用方声明的关键帧标志，不证明内容具有完整重建资格。
        /// </summary>
        public bool IsKeyFrame { get; }
        /// <summary>
        /// 调用方提供的玩家数量，未指定时为零。
        /// </summary>
        public int PlayerCount { get; }

        /// <summary>
        /// 直接保存给定元数据和状态引用，不复制数组，也不校验输入。
        /// </summary>
        public ServerGameState(
            int frame,
            long timestamp,
            byte[] stateData,
            StateHash hash,
            bool isKeyFrame,
            int playerCount = 0)
        {
            Frame = frame;
            Timestamp = timestamp;
            StateData = stateData;
            Hash = hash;
            IsKeyFrame = isKeyFrame;
            PlayerCount = playerCount;
        }
    }
}
