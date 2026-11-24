namespace AbilityKit.Coordinator
{
    /// <summary>
    /// 游戏玩法层的门控，用于决定逻辑世界是否可以推进。
    /// </summary>
    public interface ILogicWorldDriveGate
    {
        bool CanDriveLogicWorld(float deltaTime);
    }
}
