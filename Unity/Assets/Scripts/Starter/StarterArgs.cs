using System;

namespace AbilityKit.Starter
{
    public readonly struct StarterArgs
    {
        public readonly Action OnEnterGame;

        public StarterArgs(Action onEnterGame)
        {
            OnEnterGame = onEnterGame;
        }
    }
}
