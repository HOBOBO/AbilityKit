using System.Threading.Tasks;

namespace AbilityKit.Game.View.Flow
{
    public readonly struct PhaseContext<TEntry, TRoot>
    {
        public readonly TEntry Entry;
        public readonly TRoot Root;

        public PhaseContext(TEntry entry, TRoot root)
        {
            Entry = entry;
            Root = root;
        }
    }

    public interface IPhase<TContext>
    {
        void Enter(in TContext ctx);
        void Exit(in TContext ctx);
        void Tick(in TContext ctx, float deltaTime);
    }

    public interface IPhaseFeature<TContext>
    {
        void OnAttach(in TContext ctx);
        void OnDetach(in TContext ctx);
        void Tick(in TContext ctx, float deltaTime);
    }

    public interface IAsyncPhaseFeature<TContext> : IPhaseFeature<TContext>
    {
        Task DetachAsync(TContext ctx);
    }

    public interface IPhaseGuiFeature<TContext>
    {
        void OnGUI(in TContext ctx);
    }
}
