namespace AbilityKit.Coordinator
{
    /// <summary>
    /// 玩家输入命令。demos 用原始 4 参构造 + 读 Frame/PlayerId/OpCode/Payload；
    /// 载荷编解码由各玩法自带 codec 处理（内置的 Move/Skill payload 类型与 codec 已移除，避免与玩法自带协议重复）。
    /// </summary>
    public struct PlayerInput
    {
        /// <summary>输入帧。</summary>
        public int Frame;

        /// <summary>玩家标识。</summary>
        public int PlayerId;

        /// <summary>操作码（玩法自定义）。</summary>
        public int OpCode;

        /// <summary>序列化后的载荷数据。</summary>
        public byte[] Payload;

        public PlayerInput(int frame, int playerId, int opCode, byte[] payload)
        {
            Frame = frame;
            PlayerId = playerId;
            OpCode = opCode;
            Payload = payload;
        }
    }
}
