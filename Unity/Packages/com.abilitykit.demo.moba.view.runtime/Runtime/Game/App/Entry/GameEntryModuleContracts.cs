using AbilityKit.Game.Flow;
using AbilityKit.Game.View.Modules;
using AbilityKit.World.ECS;

namespace AbilityKit.Game
{
    public readonly struct GameEntryModuleContext
    {
        public readonly IGameHost Host;
        public readonly IEntity Root;

        public GameEntryModuleContext(IGameHost host, IEntity root)
        {
            Host = host;
            Root = root;
        }
    }

    public interface IGameEntryModule : IGameModule<GameEntryModuleContext>, IGameModuleId
    {
    }
}
