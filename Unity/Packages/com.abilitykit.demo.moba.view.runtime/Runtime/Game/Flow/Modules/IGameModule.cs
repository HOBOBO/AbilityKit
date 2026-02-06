using System.Collections.Generic;

namespace AbilityKit.Game.Flow.Modules
{
    public interface IGameModule<TContext>
    {
        void OnAttach(in TContext ctx);
        void OnDetach(in TContext ctx);
    }

    public interface IGameModuleTick<TContext>
    {
        void Tick(in TContext ctx, float deltaTime);
    }

    public interface IGameModuleRebind<TContext>
    {
        void RebindAll(in TContext ctx);
    }

    public interface IGameModuleId
    {
        string Id { get; }
    }

    public interface IGameModuleDependencies
    {
        IEnumerable<string> Dependencies { get; }
    }
}
