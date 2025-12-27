namespace AbilityKit.Game
{
    public sealed class GameManager
    {
        public bool IsInGame { get; private set; }

        public void EnterGame()
        {
            IsInGame = true;
        }

        public void LeaveGame()
        {
            IsInGame = false;
        }
    }
}
