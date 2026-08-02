namespace AbilityKit.Game.Flow
{
    public enum MobaRootState
    {
        Boot = 0,
        Lobby = 1,
        Battle = 2
    }

    internal enum MobaRootEvent
    {
        BootCompleted = 0,
        EnterBattle = 1,
        ReturnLobby = 2
    }

    public enum MobaBattleState
    {
        Prepare = 0,
        Connect = 1,
        CreateOrJoinWorld = 2,
        LoadAssets = 3,
        InMatch = 4,
        End = 5
    }

    internal enum MobaBattleEvent
    {
        PrepareDone = 0,
        Connected = 1,
        JoinedWorld = 2,
        Ended = 4,
        // 真实资源加载完成信号（manifest barrier）。首帧不代表资源加载完成。
        AssetsLoadCompleted = 5
    }
}
