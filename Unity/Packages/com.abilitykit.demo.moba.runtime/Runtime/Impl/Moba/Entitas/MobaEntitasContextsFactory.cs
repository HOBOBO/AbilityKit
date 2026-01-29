using AbilityKit.Ability.World.Entitas;

namespace AbilityKit.Ability.Share.Impl.Moba.EntitasAdapters
{
    public sealed class MobaEntitasContextsFactory : IEntitasContextsFactory
    {
        public global::Entitas.IContexts Create()
        {
            return new global::Contexts();
        }

        public void Release(global::Entitas.IContexts contexts)
        {
            try
            {
                (contexts as global::Contexts)?.Reset();
            }
            catch
            {
            }
        }
    }
}
